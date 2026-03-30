using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Serilog;
using System.Windows.Forms;

namespace IRIS.Agent.Logic
{
    public sealed class ScreenSnapshotServer : IDisposable
    {

        private readonly int _port;
        private readonly int _maxWidth;
        private readonly long _jpegQuality;
        private readonly string? _accessToken;
        private readonly HashSet<string> _allowedSourceIps;
        private readonly bool _allowLocalSubnet;
        private readonly bool _controllerDiscoveryMode;
        private readonly List<(IPAddress Network, IPAddress Mask)> _localSubnets;
        private readonly HashSet<string> _controllerIps = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _controllerLock = new();
        private readonly HttpListener _listener = new();
        private CancellationTokenSource? _cts;
        private Task? _serverTask;

        private readonly object _frameLock = new();
        private byte[]? _cachedFrame;
        private DateTime _cachedAtUtc = DateTime.MinValue;

        public ScreenSnapshotServer(
            int port,
            int maxWidth = 640,
            long jpegQuality = 55,
            string? accessToken = null,
            IEnumerable<string>? allowedSourceIps = null,
            bool allowLocalSubnet = false,
            bool controllerDiscoveryMode = false)
        {
            _port = port;
            _maxWidth = maxWidth;
            _jpegQuality = jpegQuality;
            _accessToken = string.IsNullOrWhiteSpace(accessToken) ? null : accessToken.Trim();
            _allowedSourceIps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _allowLocalSubnet = allowLocalSubnet;
            _controllerDiscoveryMode = controllerDiscoveryMode;
            _localSubnets = new List<(IPAddress Network, IPAddress Mask)>();

            if (allowedSourceIps != null)
            {
                foreach (var ip in allowedSourceIps)
                {
                    if (!string.IsNullOrWhiteSpace(ip))
                    {
                        _allowedSourceIps.Add(ip.Trim());
                    }
                }
            }

            if (_allowLocalSubnet)
            {
                _localSubnets = GetLocalSubnets();
            }
        }

        public Task StartAsync()
        {
            if (_listener.IsListening)
            {
                return Task.CompletedTask;
            }

            _cts = new CancellationTokenSource();
            foreach (var prefix in GetListenerPrefixes())
            {
                _listener.Prefixes.Add(prefix);
            }

            try
            {
                _listener.Start();
            }
            catch (HttpListenerException ex) when (ex.ErrorCode == 5)
            {
                Log.Error(ex,
                    "Access denied while starting snapshot listener on port {Port}. " +
                    "Run the agent as Administrator once or reserve URL ACL with: netsh http add urlacl url=http://+:{Port}/ user={User}",
                    _port,
                    Environment.UserDomainName + "\\" + Environment.UserName);
                throw;
            }

            _serverTask = Task.Run(() => ListenLoopAsync(_cts.Token));
            Log.Information("Screen snapshot server started on port {Port}", _port);
            return Task.CompletedTask;
        }

        private IEnumerable<string> GetListenerPrefixes()
        {
            var prefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                $"http://localhost:{_port}/",
                $"http://127.0.0.1:{_port}/"
            };

            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                var ipProperties = nic.GetIPProperties();
                foreach (var unicast in ipProperties.UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        prefixes.Add($"http://{unicast.Address}:{_port}/");
                    }
                }
            }

            return prefixes;
        }

        public async Task StopAsync()
        {
            if (!_listener.IsListening)
            {
                return;
            }

            _cts?.Cancel();
            _listener.Stop();
            _listener.Close();
            if (_serverTask != null)
            {
                await _serverTask;
            }
            Log.Information("Screen snapshot server stopped");
        }

        private async Task ListenLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                HttpListenerContext? context = null;
                try
                {
                    context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequestAsync(context), cancellationToken);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Screen snapshot listener error");
                    if (context != null)
                    {
                        try { context.Response.StatusCode = 500; context.Response.Close(); } catch { }
                    }
                }
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            var path = context.Request.Url?.AbsolutePath?.ToLowerInvariant() ?? "/";

            if (path == "/health")
            {
                context.Response.StatusCode = 200;
                await context.Response.OutputStream.WriteAsync("ok"u8.ToArray());
                context.Response.Close();
                return;
            }

            if (path != "/snapshot")
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
                return;
            }

            if (!HasValidToken(context))
            {
                context.Response.StatusCode = 401;
                context.Response.Close();
                return;
            }

            RegisterControllerIpIfNeeded(context);

            if (!IsAllowedSource(context))
            {
                context.Response.StatusCode = 403;
                context.Response.Close();
                return;
            }

            var frame = GetOrCaptureFrame();
            if (frame == null || frame.Length == 0)
            {
                context.Response.StatusCode = 204;
                context.Response.Close();
                return;
            }

            context.Response.StatusCode = 200;
            context.Response.ContentType = "image/jpeg";
            context.Response.ContentLength64 = frame.Length;
            await context.Response.OutputStream.WriteAsync(frame, 0, frame.Length);
            context.Response.Close();
        }

        private bool IsAllowedSource(HttpListenerContext context)
        {
            if (_allowedSourceIps.Count == 0 && !_allowLocalSubnet && !_controllerDiscoveryMode)
            {
                return true;
            }

            var remoteAddress = context.Request.RemoteEndPoint?.Address;
            if (remoteAddress == null)
            {
                return false;
            }

            if (remoteAddress.IsIPv4MappedToIPv6)
            {
                remoteAddress = remoteAddress.MapToIPv4();
            }

            if (remoteAddress.AddressFamily != AddressFamily.InterNetwork)
            {
                return false;
            }

            var remoteIpString = remoteAddress.ToString();
            if (_allowedSourceIps.Contains(remoteIpString))
            {
                return true;
            }

            if (_allowLocalSubnet && IsFromLocalSubnet(remoteAddress))
            {
                return true;
            }

            if (_controllerDiscoveryMode)
            {
                lock (_controllerLock)
                {
                    if (_controllerIps.Count == 0)
                    {
                        return true;
                    }

                    return _controllerIps.Contains(remoteIpString);
                }
            }

            return false;
        }

        private void RegisterControllerIpIfNeeded(HttpListenerContext context)
        {
            if (!_controllerDiscoveryMode)
            {
                return;
            }

            var remoteAddress = context.Request.RemoteEndPoint?.Address;
            if (remoteAddress == null)
            {
                return;
            }

            if (remoteAddress.IsIPv4MappedToIPv6)
            {
                remoteAddress = remoteAddress.MapToIPv4();
            }

            if (remoteAddress.AddressFamily != AddressFamily.InterNetwork)
            {
                return;
            }

            var remoteIpString = remoteAddress.ToString();
            lock (_controllerLock)
            {
                if (_controllerIps.Add(remoteIpString))
                {
                    Log.Information("Registered controller IP for snapshot access: {ControllerIp}", remoteIpString);
                }
            }
        }

        private bool IsFromLocalSubnet(IPAddress remoteAddress)
        {
            foreach (var (network, mask) in _localSubnets)
            {
                if (IsInSameSubnet(remoteAddress, network, mask))
                {
                    return true;
                }
            }

            return false;
        }

        private static List<(IPAddress Network, IPAddress Mask)> GetLocalSubnets()
        {
            var subnets = new List<(IPAddress Network, IPAddress Mask)>();

            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                var ipProperties = nic.GetIPProperties();
                foreach (var unicast in ipProperties.UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily != AddressFamily.InterNetwork || unicast.IPv4Mask == null)
                    {
                        continue;
                    }

                    var network = GetNetworkAddress(unicast.Address, unicast.IPv4Mask);
                    subnets.Add((network, unicast.IPv4Mask));
                }
            }

            return subnets;
        }

        private static bool IsInSameSubnet(IPAddress remoteAddress, IPAddress localNetwork, IPAddress subnetMask)
        {
            var remoteNetwork = GetNetworkAddress(remoteAddress, subnetMask);
            return remoteNetwork.Equals(localNetwork);
        }

        private static IPAddress GetNetworkAddress(IPAddress ipAddress, IPAddress subnetMask)
        {
            var ipBytes = ipAddress.GetAddressBytes();
            var maskBytes = subnetMask.GetAddressBytes();
            var networkBytes = new byte[ipBytes.Length];

            for (var i = 0; i < ipBytes.Length; i++)
            {
                networkBytes[i] = (byte)(ipBytes[i] & maskBytes[i]);
            }

            return new IPAddress(networkBytes);
        }

        private bool HasValidToken(HttpListenerContext context)
        {
            if (string.IsNullOrWhiteSpace(_accessToken))
            {
                return true;
            }

            var headerToken = context.Request.Headers["X-IRIS-Snapshot-Token"];
            var queryToken = context.Request.QueryString["token"];
            var token = !string.IsNullOrWhiteSpace(headerToken) ? headerToken : queryToken;

            return string.Equals(token, _accessToken, StringComparison.Ordinal);
        }

        private byte[]? GetOrCaptureFrame()
        {
            lock (_frameLock)
            {
                if (_cachedFrame != null && (DateTime.UtcNow - _cachedAtUtc).TotalMilliseconds < 200)
                {
                    return _cachedFrame;
                }

                _cachedFrame = CaptureFrame();
                _cachedAtUtc = DateTime.UtcNow;
                return _cachedFrame;
            }
        }

        private byte[]? CaptureFrame()
        {
            try
            {
                var primaryScreen = Screen.PrimaryScreen;
                if (primaryScreen == null)
                {
                    return null;
                }

                var bounds = primaryScreen.Bounds;
                using var full = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
                using (var graphics = Graphics.FromImage(full))
                {
                    graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
                    
                    // Draw cursor
                    try
                    {
                        var cursorInfo = new NativeMethods.CURSORINFO { cbSize = Marshal.SizeOf(typeof(NativeMethods.CURSORINFO)) };
                        if (NativeMethods.GetCursorInfo(out cursorInfo) && cursorInfo.flags == NativeMethods.CURSOR_SHOWING)
                        {
                            var hdc = graphics.GetHdc();
                            try
                            {
                                if (NativeMethods.GetIconInfo(cursorInfo.hCursor, out var iconInfo))
                                {
                                    var x = cursorInfo.ptScreenPos.x - iconInfo.xHotspot - bounds.Left;
                                    var y = cursorInfo.ptScreenPos.y - iconInfo.yHotspot - bounds.Top;
                                    NativeMethods.DrawIcon(hdc, x, y, cursorInfo.hCursor);
                                }
                            }
                            finally
                            {
                                graphics.ReleaseHdc(hdc);
                            }
                        }
                    }
                    catch { /* Cursor capture failed, continue without it */ }
                }

                var targetWidth = Math.Min(_maxWidth, full.Width);
                var targetHeight = Math.Max(1, (int)Math.Round(full.Height * (targetWidth / (double)full.Width)));

                using var thumb = new Bitmap(targetWidth, targetHeight, PixelFormat.Format24bppRgb);
                using (var graphics = Graphics.FromImage(thumb))
                {
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.SmoothingMode = SmoothingMode.HighQuality;
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.DrawImage(full, 0, 0, targetWidth, targetHeight);
                }

                using var ms = new MemoryStream();
                var encoder = ImageCodecInfo.GetImageDecoders().FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
                if (encoder != null)
                {
                    using var encoderParams = new EncoderParameters(1);
                    encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, Math.Clamp(_jpegQuality, 20, 90));
                    thumb.Save(ms, encoder, encoderParams);
                }
                else
                {
                    thumb.Save(ms, ImageFormat.Jpeg);
                }

                return ms.ToArray();
            }
            catch
            {
                return null;
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            if (_listener.IsListening)
            {
                _listener.Stop();
            }
            _listener.Close();
            _cts?.Dispose();
        }
    }
}
