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
        // Raw TCP HTTP server sidesteps http.sys entirely. Windows 11 24H2
        // introduced a regression in http.sys response-write teardown under
        // concurrent short-lived Connection: close clients, which is exactly
        // what the UI's snapshot poller looks like. TcpListener + manual
        // HTTP framing is immune to that regression by construction.
        private RawHttpServer? _server;

        private readonly SemaphoreSlim _captureGate = new(1, 1);
        private byte[]? _cachedFrame;
        private DateTime _cachedAtUtc = DateTime.MinValue;
        private static readonly TimeSpan CacheFreshWindow = TimeSpan.FromMilliseconds(200);
        private static readonly TimeSpan CacheStaleAcceptable = TimeSpan.FromMilliseconds(2000);
        private byte[]? _placeholderFrame;

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

        public async Task StartAsync()
        {
            if (_server != null) return;

            _server = new RawHttpServer(_port, HandleRequestAsync);
            await _server.StartAsync();
            Log.Information("Screen snapshot server started on port {Port}", _port);

            // Pre-render the "Screen unavailable" placeholder now, while we
            // still have a healthy desktop context. If capture later breaks
            // (e.g. the 24H2 CopyFromScreen regression), serving the placeholder
            // becomes a pure byte-buffer write — no GDI, no GetHdc, no risk of
            // the fallback path itself throwing.
            try { _placeholderFrame = BuildPlaceholderBytes(); }
            catch (Exception ex) { Log.Warning(ex, "Failed to pre-render placeholder JPEG"); }
        }

        public async Task StopAsync()
        {
            if (_server == null) return;
            await _server.StopAsync();
            _server = null;
            Log.Information("Screen snapshot server stopped");
        }

        private async Task HandleRequestAsync(RawHttpContext context, CancellationToken ct)
        {
            var path = (context.Request.Path ?? "/").ToLowerInvariant();
            var remoteIp = context.RemoteEndPoint?.Address?.ToString() ?? "?";
            int status;

            if (path == "/health")
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                context.Response.SetBody("ok"u8.ToArray());
                LogRequestOutcome(path, remoteIp, 200, null);
                return;
            }

            if (path != "/snapshot")
            {
                context.Response.StatusCode = status = 404;
                LogRequestOutcome(path, remoteIp, status, "unknown path");
                return;
            }

            if (!HasValidToken(context))
            {
                context.Response.StatusCode = status = 401;
                var hasHeader = !string.IsNullOrWhiteSpace(context.Request.Headers["X-IRIS-Snapshot-Token"]);
                var hasQuery = !string.IsNullOrWhiteSpace(context.Request.QueryString["token"]);
                LogRequestOutcome(path, remoteIp, status,
                    $"bad token (header={hasHeader}, query={hasQuery})");
                return;
            }

            RegisterControllerIpIfNeeded(context);

            if (!IsAllowedSource(context))
            {
                context.Response.StatusCode = status = 403;
                LogRequestOutcome(path, remoteIp, status, "source not allowed");
                return;
            }

            var frame = await GetOrCaptureFrameAsync();
            if (frame == null || frame.Length == 0)
            {
                // Capture failed (likely non-interactive session). Serve a placeholder JPEG
                // so the UI keeps a visual for this agent and doesn't blacklist it.
                frame = GetPlaceholderFrame();
                if (frame == null || frame.Length == 0)
                {
                    context.Response.StatusCode = status = 204;
                    LogEmptyResponse();
                    LogRequestOutcome(path, remoteIp, status, "empty frame");
                    return;
                }
            }

            context.Response.StatusCode = status = 200;
            context.Response.ContentType = "image/jpeg";
            context.Response.SetBody(frame);
            LogRequestOutcome(path, remoteIp, status, $"{frame.Length} bytes");
        }

        private DateTime _lastRequestLogUtc = DateTime.MinValue;
        private int _requestLogSuppressed;

        private void LogRequestOutcome(string path, string remoteIp, int status, string? note)
        {
            // Unconditionally log non-200 outcomes; throttle 200/health to 1/10s.
            var now = DateTime.UtcNow;
            if (status == 200 || path == "/health")
            {
                if ((now - _lastRequestLogUtc).TotalSeconds < 10)
                {
                    Interlocked.Increment(ref _requestLogSuppressed);
                    return;
                }
                _lastRequestLogUtc = now;
                var suppressed = Interlocked.Exchange(ref _requestLogSuppressed, 0);
                var tail = suppressed > 0 ? $" (+{suppressed} similar suppressed)" : "";
                Log.Information("Snapshot req {Path} from {Ip} → {Status} {Note}{Tail}",
                    path, remoteIp, status, note ?? string.Empty, tail);
            }
            else
            {
                Log.Warning("Snapshot req {Path} from {Ip} → {Status} {Note}",
                    path, remoteIp, status, note ?? string.Empty);
            }
        }

        private bool IsAllowedSource(RawHttpContext context)
        {
            if (_allowedSourceIps.Count == 0 && !_allowLocalSubnet && !_controllerDiscoveryMode)
            {
                return true;
            }

            var remoteAddress = context.RemoteEndPoint?.Address;
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

        private void RegisterControllerIpIfNeeded(RawHttpContext context)
        {
            if (!_controllerDiscoveryMode)
            {
                return;
            }

            var remoteAddress = context.RemoteEndPoint?.Address;
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

        private bool HasValidToken(RawHttpContext context)
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

        private async Task<byte[]?> GetOrCaptureFrameAsync()
        {
            // Fast path: fresh cache, no lock.
            var cached = Volatile.Read(ref _cachedFrame);
            var age = DateTime.UtcNow - _cachedAtUtc;
            if (cached != null && age < CacheFreshWindow)
            {
                return cached;
            }

            // Only one capture at a time. If we can't enter quickly and we have a
            // reasonably fresh frame, return it instead of piling up captures.
            if (!await _captureGate.WaitAsync(0))
            {
                if (cached != null && age < CacheStaleAcceptable)
                {
                    return cached;
                }
                await _captureGate.WaitAsync();
            }

            try
            {
                // Re-check after acquiring the gate — another caller may have just refreshed.
                age = DateTime.UtcNow - _cachedAtUtc;
                cached = _cachedFrame;
                if (cached != null && age < CacheFreshWindow)
                {
                    return cached;
                }

                var captured = CaptureFrame();
                if (captured != null && captured.Length > 0)
                {
                    _cachedFrame = captured;
                    _cachedAtUtc = DateTime.UtcNow;
                    return captured;
                }

                // Capture failed but a recent cached frame exists — serve it.
                if (cached != null && age < CacheStaleAcceptable)
                {
                    return cached;
                }

                return null;
            }
            finally
            {
                _captureGate.Release();
            }
        }

        private byte[]? GetPlaceholderFrame()
        {
            var existing = Volatile.Read(ref _placeholderFrame);
            if (existing != null) return existing;

            try
            {
                var bytes = BuildPlaceholderBytes();
                _placeholderFrame = bytes;
                return bytes;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to build placeholder JPEG");
                return null;
            }
        }

        private byte[] BuildPlaceholderBytes()
        {
            var width = Math.Max(320, Math.Min(_maxWidth, 1280));
            var height = (int)Math.Round(width * 9.0 / 16.0);
            using var bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(25, 28, 36));
                using var titleFont = new Font("Segoe UI", Math.Max(12f, width / 40f), FontStyle.Bold);
                using var subFont = new Font("Segoe UI", Math.Max(9f, width / 70f));
                using var brush = new SolidBrush(Color.FromArgb(235, 235, 240));
                using var subBrush = new SolidBrush(Color.FromArgb(160, 165, 175));
                var title = "Screen unavailable";
                var sub = "Agent cannot capture the desktop right now.";
                var titleSize = g.MeasureString(title, titleFont);
                var subSize = g.MeasureString(sub, subFont);
                g.DrawString(title, titleFont, brush,
                    (width - titleSize.Width) / 2f, (height - titleSize.Height) / 2f - subSize.Height);
                g.DrawString(sub, subFont, subBrush,
                    (width - subSize.Width) / 2f, (height - titleSize.Height) / 2f + titleSize.Height / 2f);
            }

            using var ms = new MemoryStream();
            var encoder = ImageCodecInfo.GetImageDecoders().FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
            if (encoder != null)
            {
                using var encoderParams = new EncoderParameters(1);
                encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, 70L);
                bmp.Save(ms, encoder, encoderParams);
            }
            else
            {
                bmp.Save(ms, ImageFormat.Jpeg);
            }
            return ms.ToArray();
        }

        private DateTime _lastCaptureErrorLogUtc = DateTime.MinValue;
        private DateTime _lastEmptyResponseLogUtc = DateTime.MinValue;
        private int _emptyResponseCount;

        private void LogEmptyResponse()
        {
            Interlocked.Increment(ref _emptyResponseCount);
            var now = DateTime.UtcNow;
            if ((now - _lastEmptyResponseLogUtc).TotalSeconds < 30) return;
            _lastEmptyResponseLogUtc = now;
            var count = Interlocked.Exchange(ref _emptyResponseCount, 0);
            Log.Warning("Snapshot handler returned 204 No Content {Count} time(s) in last 30s.", count);
        }

        private byte[]? CaptureFrame()
        {
            try
            {
                var primaryScreen = Screen.PrimaryScreen;
                if (primaryScreen == null)
                {
                    LogCaptureFailure("Screen.PrimaryScreen is null — no interactive display available to this session.");
                    return null;
                }

                var bounds = primaryScreen.Bounds;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                using var full = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
                if (!TryCaptureDesktopInto(full, bounds))
                {
                    return null;
                }
                LogCaptureTiming(sw.ElapsedMilliseconds);

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
            catch (Exception ex)
            {
                LogCaptureFailure(ex);
                return null;
            }
        }

        // True when PrintWindow has been confirmed necessary on this host (e.g.
        // Windows 11 24H2 where CopyFromScreen returns ERROR_INVALID_HANDLE).
        // We sticky-flip to the fallback after the first confirmed failure to
        // avoid burning a GDI exception on every capture call.
        private volatile bool _preferPrintWindowPath;

        // True when even PrintWindow has failed and we've switched to the WinRT
        // Windows.Graphics.Capture path (final rung of the fallback ladder).
        private volatile bool _preferGraphicsCapturePath;
        private GraphicsCaptureLogic? _graphicsCapture;
        private readonly object _graphicsCaptureInitLock = new();
        // PrintWindow-too-slow detection: if consecutive captures run over this
        // budget, sticky-flip to the WinRT path. The UI's per-request budget
        // is 7s, and concurrent polls compound queueing delays, so a 2s ceiling
        // keeps headroom.
        private const int PrintWindowSlowBudgetMs = 2000;
        private int _printWindowSlowStreak;

        private bool TryCaptureDesktopInto(Bitmap target, Rectangle bounds)
        {
            if (_preferGraphicsCapturePath)
            {
                return TryCaptureViaGraphicsCapture(target, bounds);
            }

            if (!_preferPrintWindowPath)
            {
                try
                {
                    using var graphics = Graphics.FromImage(target);
                    graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
                    DrawCursorOnto(graphics, bounds);
                    return true;
                }
                catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 6)
                {
                    // ERROR_INVALID_HANDLE — the 24H2 regression. Stop trying
                    // CopyFromScreen on this process; it won't recover without
                    // a logout. Fall through to PrintWindow.
                    _preferPrintWindowPath = true;
                    Log.Information("CopyFromScreen failed with invalid-handle; switching this process to PrintWindow capture path.");
                }
                catch (Exception ex)
                {
                    LogCaptureFailure(ex);
                    return false;
                }
            }

            // Measure PrintWindow specifically so we can escalate to WinRT if
            // it's functional but too slow for the UI's request budget.
            var pwWatch = System.Diagnostics.Stopwatch.StartNew();
            var pwOk = TryCaptureViaPrintWindow(target, bounds);
            pwWatch.Stop();

            if (!pwOk)
            {
                Log.Warning("PrintWindow capture failed; escalating to Windows.Graphics.Capture (WinRT) path.");
                _preferGraphicsCapturePath = true;
                return TryCaptureViaGraphicsCapture(target, bounds);
            }

            if (pwWatch.ElapsedMilliseconds > PrintWindowSlowBudgetMs)
            {
                _printWindowSlowStreak++;
                if (_printWindowSlowStreak >= 3)
                {
                    Log.Warning("PrintWindow averaged >{Budget}ms for {Count} captures; escalating to Windows.Graphics.Capture.",
                        PrintWindowSlowBudgetMs, _printWindowSlowStreak);
                    _preferGraphicsCapturePath = true;
                }
            }
            else
            {
                _printWindowSlowStreak = 0;
            }

            return true;
        }

        private bool TryCaptureViaGraphicsCapture(Bitmap target, Rectangle bounds)
        {
            var capture = EnsureGraphicsCapture();
            if (capture == null)
            {
                return false;
            }

            try
            {
                var jpeg = capture.CaptureFrame();
                if (jpeg == null || jpeg.Length == 0)
                {
                    return false;
                }

                // The rest of the pipeline (resize + final JPEG encode) expects
                // a raw Bitmap in the `target` buffer, not JPEG bytes. Decode
                // the captured JPEG into the target's backing store.
                using var ms = new MemoryStream(jpeg);
                using var decoded = new Bitmap(ms);
                using (var graphics = Graphics.FromImage(target))
                {
                    graphics.Clear(Color.Black);
                    graphics.DrawImage(decoded, 0, 0, target.Width, target.Height);
                    DrawCursorOnto(graphics, bounds);
                }
                return true;
            }
            catch (Exception ex)
            {
                LogCaptureFailure(ex);
                return false;
            }
        }

        private GraphicsCaptureLogic? EnsureGraphicsCapture()
        {
            if (_graphicsCapture != null) return _graphicsCapture;

            lock (_graphicsCaptureInitLock)
            {
                if (_graphicsCapture != null) return _graphicsCapture;

                var candidate = new GraphicsCaptureLogic();
                if (!candidate.Initialize())
                {
                    candidate.Dispose();
                    return null;
                }

                _graphicsCapture = candidate;
                return _graphicsCapture;
            }
        }

        private bool TryCaptureViaPrintWindow(Bitmap target, Rectangle bounds)
        {
            // Ask DWM (via the desktop window) to composite its output into
            // our bitmap's DC. PW_RENDERFULLCONTENT includes UWP / hardware-
            // accelerated surfaces. Works on 24H2 where CopyFromScreen fails.
            try
            {
                using var graphics = Graphics.FromImage(target);
                var hdc = graphics.GetHdc();
                try
                {
                    var desktop = NativeMethods.GetDesktopWindow();
                    if (!NativeMethods.PrintWindow(desktop, hdc, NativeMethods.PW_RENDERFULLCONTENT))
                    {
                        LogCaptureFailure($"PrintWindow returned false (LastError={Marshal.GetLastWin32Error()}).");
                        return false;
                    }
                }
                finally
                {
                    graphics.ReleaseHdc(hdc);
                }

                // PrintWindow on the desktop HWND doesn't draw the cursor
                // (the cursor isn't part of the DWM-composited tree), so
                // overlay it ourselves — same code path as CopyFromScreen.
                using (var cursorGraphics = Graphics.FromImage(target))
                {
                    DrawCursorOnto(cursorGraphics, bounds);
                }
                return true;
            }
            catch (Exception ex)
            {
                LogCaptureFailure(ex);
                return false;
            }
        }

        private static void DrawCursorOnto(Graphics graphics, Rectangle bounds)
        {
            try
            {
                var cursorInfo = new NativeMethods.CURSORINFO { cbSize = Marshal.SizeOf(typeof(NativeMethods.CURSORINFO)) };
                if (!NativeMethods.GetCursorInfo(out cursorInfo) || cursorInfo.flags != NativeMethods.CURSOR_SHOWING)
                {
                    return;
                }

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
            catch { /* cursor overlay is best-effort */ }
        }

        private DateTime _lastCaptureTimingLogUtc = DateTime.MinValue;
        private void LogCaptureTiming(long elapsedMs)
        {
            // Throttle to one line per 10s so we don't flood the log under normal polling,
            // but still know at a glance which path is active and roughly how fast it is.
            var now = DateTime.UtcNow;
            if ((now - _lastCaptureTimingLogUtc).TotalSeconds < 10) return;
            _lastCaptureTimingLogUtc = now;
            var path = _preferGraphicsCapturePath ? "GraphicsCapture"
                : _preferPrintWindowPath ? "PrintWindow"
                : "CopyFromScreen";
            Log.Information("Snapshot capture via {Path} took {Elapsed} ms.", path, elapsedMs);
        }

        private void LogCaptureFailure(Exception ex)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastCaptureErrorLogUtc).TotalSeconds < 10) return;
            _lastCaptureErrorLogUtc = now;
            Log.Warning(ex, "Screen capture failed. SessionId={SessionId}, Interactive={Interactive}",
                System.Diagnostics.Process.GetCurrentProcess().SessionId, Environment.UserInteractive);
        }

        private void LogCaptureFailure(string message)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastCaptureErrorLogUtc).TotalSeconds < 10) return;
            _lastCaptureErrorLogUtc = now;
            Log.Warning("Screen capture failed: {Message}. SessionId={SessionId}, Interactive={Interactive}",
                message, System.Diagnostics.Process.GetCurrentProcess().SessionId, Environment.UserInteractive);
        }

        public void Dispose()
        {
            _server?.Dispose();
            _server = null;
            _graphicsCapture?.Dispose();
        }
    }
}
