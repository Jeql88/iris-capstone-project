using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Serilog;

namespace IRIS.Agent.Logic
{
    // Minimal HTTP/1.1 server over a raw TcpListener. Exists to sidestep
    // http.sys behavior changes that caused response-write RSTs on
    // Windows 11 24H2 under the UI's concurrent-poll pattern. Speaks
    // exactly enough HTTP to serve the agent's JSON/JPEG endpoints:
    //   * One request per connection, Connection: close always
    //   * Content-Length framing only (no chunked, no TE)
    //   * No keep-alive, no TLS, no trailers, no pipelining
    //   * Response is fully buffered in memory then written in one shot,
    //     so the handler cannot observe a mid-body write failure
    public sealed class RawHttpServer : IDisposable
    {
        public delegate Task RequestHandler(RawHttpContext context, CancellationToken ct);

        private readonly int _port;
        private readonly RequestHandler _handler;
        private TcpListener? _listener;
        private CancellationTokenSource? _cts;
        private Task? _acceptTask;

        private const int MaxRequestHeaderBytes = 16 * 1024;
        private static readonly TimeSpan HeaderReadTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan ResponseWriteTimeout = TimeSpan.FromSeconds(10);

        public RawHttpServer(int port, RequestHandler handler)
        {
            _port = port;
            _handler = handler;
        }

        public bool IsListening => _listener != null;

        public Task StartAsync()
        {
            if (_listener != null) return Task.CompletedTask;

            _cts = new CancellationTokenSource();
            // IPv4-only listener. The lab is IPv4 (192.168.5.0/21) and Windows 11
            // 24H2 has been observed dropping IPv4 SYNs to dual-stack IPv6Any
            // sockets, which manifests as ~21s curl connect failures. Plain IPv4
            // sidesteps that entirely with no functional loss.
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            _acceptTask = Task.Run(() => AcceptLoopAsync(_cts.Token));
            Log.Information("Raw HTTP server listening on TCP :{Port}", _port);
            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            if (_listener == null) return;

            _cts?.Cancel();
            try { _listener.Stop(); } catch { }
            if (_acceptTask != null)
            {
                try { await _acceptTask; } catch { }
            }
            _listener = null;
            Log.Information("Raw HTTP server stopped (port {Port})", _port);
        }

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            var listener = _listener!;
            while (!ct.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync(ct);
                }
                catch (OperationCanceledException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Raw HTTP accept loop error; continuing.");
                    continue;
                }

                _ = Task.Run(() => HandleClientAsync(client, ct));
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
        {
            var remote = (client.Client.RemoteEndPoint as IPEndPoint);
            try
            {
                client.NoDelay = true;
                client.ReceiveTimeout = (int)HeaderReadTimeout.TotalMilliseconds;
                client.SendTimeout = (int)ResponseWriteTimeout.TotalMilliseconds;

                using var stream = client.GetStream();

                var request = await ReadRequestAsync(stream, ct);
                if (request == null)
                {
                    return; // bad/truncated request; drop silently
                }

                var response = new RawHttpResponse();
                var context = new RawHttpContext(request, response, remote);

                try
                {
                    await _handler(context, ct);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Raw HTTP handler threw for {Ip}", remote?.Address);
                    response.Reset();
                    response.StatusCode = 500;
                    response.ContentType = "text/plain";
                    response.SetBody(Encoding.ASCII.GetBytes("Internal Server Error\n"));
                }

                if (response.Aborted)
                {
                    // Handler wanted to drop the connection without writing a
                    // response (e.g. client is known-dead).
                    return;
                }

                await WriteResponseAsync(stream, response, ct);
            }
            catch (IOException)      { /* client disconnect during read/write — expected */ }
            catch (SocketException)  { /* same class */ }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Log.Warning(ex, "Raw HTTP per-connection handler error ({Ip}).", remote?.Address);
            }
            finally
            {
                try { client.Close(); } catch { }
            }
        }

        private static async Task<RawHttpRequest?> ReadRequestAsync(NetworkStream stream, CancellationToken ct)
        {
            // Read until CRLFCRLF with a strict byte cap and 5s timeout.
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(HeaderReadTimeout);

            var buf = new byte[4096];
            var accum = new MemoryStream();
            int endMarker = -1;

            while (endMarker < 0)
            {
                int n;
                try
                {
                    n = await stream.ReadAsync(buf, cts.Token);
                }
                catch { return null; }

                if (n == 0) return null;
                accum.Write(buf, 0, n);

                if (accum.Length > MaxRequestHeaderBytes) return null;

                var arr = accum.GetBuffer();
                int len = (int)accum.Length;
                int searchFrom = Math.Max(0, len - n - 3);
                for (int i = searchFrom; i < len - 3; i++)
                {
                    if (arr[i] == '\r' && arr[i + 1] == '\n' && arr[i + 2] == '\r' && arr[i + 3] == '\n')
                    {
                        endMarker = i;
                        break;
                    }
                }
            }

            var headerBytes = accum.GetBuffer();
            var headerText = Encoding.ASCII.GetString(headerBytes, 0, endMarker);
            var lines = headerText.Split("\r\n");
            if (lines.Length == 0) return null;

            var requestLine = lines[0].Split(' ');
            if (requestLine.Length < 3) return null;

            var method = requestLine[0];
            var target = requestLine[1];

            string path;
            var query = new NameValueCollection(StringComparer.OrdinalIgnoreCase);
            var qIdx = target.IndexOf('?');
            if (qIdx >= 0)
            {
                path = target[..qIdx];
                var queryStr = target[(qIdx + 1)..];
                foreach (var kvp in queryStr.Split('&', StringSplitOptions.RemoveEmptyEntries))
                {
                    var eq = kvp.IndexOf('=');
                    if (eq >= 0)
                    {
                        query.Add(
                            Uri.UnescapeDataString(kvp[..eq]),
                            Uri.UnescapeDataString(kvp[(eq + 1)..]));
                    }
                    else
                    {
                        query.Add(Uri.UnescapeDataString(kvp), string.Empty);
                    }
                }
            }
            else
            {
                path = target;
            }

            var headers = new NameValueCollection(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrEmpty(line)) continue;
                var colon = line.IndexOf(':');
                if (colon <= 0) continue;
                var name = line[..colon].Trim();
                var value = line[(colon + 1)..].Trim();
                headers.Add(name, value);
            }

            return new RawHttpRequest(method, path, headers, query);
        }

        private static async Task WriteResponseAsync(NetworkStream stream, RawHttpResponse response, CancellationToken ct)
        {
            var body = response.BodyBytes;
            var bodyLen = body?.Length ?? 0;

            var sb = new StringBuilder(256);
            sb.Append("HTTP/1.1 ").Append(response.StatusCode).Append(' ')
              .Append(ReasonPhrase(response.StatusCode)).Append("\r\n");
            sb.Append("Connection: close\r\n");
            sb.Append("Content-Length: ").Append(bodyLen).Append("\r\n");
            if (!string.IsNullOrEmpty(response.ContentType))
            {
                sb.Append("Content-Type: ").Append(response.ContentType).Append("\r\n");
            }
            foreach (string? name in response.ExtraHeaders.AllKeys)
            {
                if (name == null) continue;
                sb.Append(name).Append(": ").Append(response.ExtraHeaders[name]).Append("\r\n");
            }
            sb.Append("\r\n");

            var head = Encoding.ASCII.GetBytes(sb.ToString());

            // Single buffered send: headers + body. Avoids the UI seeing a
            // half-written response if something races underneath us.
            var combined = new byte[head.Length + bodyLen];
            Buffer.BlockCopy(head, 0, combined, 0, head.Length);
            if (bodyLen > 0) Buffer.BlockCopy(body!, 0, combined, head.Length, bodyLen);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(ResponseWriteTimeout);
            await stream.WriteAsync(combined, cts.Token);
            await stream.FlushAsync(cts.Token);
        }

        private static string ReasonPhrase(int code) => code switch
        {
            200 => "OK",
            204 => "No Content",
            400 => "Bad Request",
            401 => "Unauthorized",
            403 => "Forbidden",
            404 => "Not Found",
            500 => "Internal Server Error",
            _ => "OK"
        };

        public void Dispose()
        {
            try { StopAsync().GetAwaiter().GetResult(); } catch { }
            _cts?.Dispose();
        }
    }

    public sealed class RawHttpRequest
    {
        public string Method { get; }
        public string Path { get; }
        public NameValueCollection Headers { get; }
        public NameValueCollection QueryString { get; }

        public RawHttpRequest(string method, string path, NameValueCollection headers, NameValueCollection query)
        {
            Method = method;
            Path = path;
            Headers = headers;
            QueryString = query;
        }
    }

    public sealed class RawHttpResponse
    {
        public int StatusCode { get; set; } = 200;
        public string? ContentType { get; set; }
        public NameValueCollection ExtraHeaders { get; } = new(StringComparer.OrdinalIgnoreCase);
        public byte[]? BodyBytes { get; private set; }
        public bool Aborted { get; private set; }

        public void SetBody(byte[] body) => BodyBytes = body;

        public void Reset()
        {
            StatusCode = 200;
            ContentType = null;
            ExtraHeaders.Clear();
            BodyBytes = null;
            Aborted = false;
        }

        public void Abort() => Aborted = true;
    }

    public sealed class RawHttpContext
    {
        public RawHttpRequest Request { get; }
        public RawHttpResponse Response { get; }
        public IPEndPoint? RemoteEndPoint { get; }

        public RawHttpContext(RawHttpRequest request, RawHttpResponse response, IPEndPoint? remote)
        {
            Request = request;
            Response = response;
            RemoteEndPoint = remote;
        }
    }
}
