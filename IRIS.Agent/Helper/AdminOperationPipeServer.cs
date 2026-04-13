using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Serilog;

namespace IRIS.Agent.Helper
{
    /// <summary>
    /// Listens on a local named pipe for admin-operation requests from the user-mode agent.
    /// Each request is a single JSON line; each response is a single JSON line.
    /// </summary>
    internal sealed class AdminOperationPipeServer : IAsyncDisposable
    {
        private readonly string _pipeName;
        private readonly string _token;
        private readonly CancellationTokenSource _cts = new();
        private Task? _acceptLoop;

        public AdminOperationPipeServer(string pipeName, string token)
        {
            _pipeName = pipeName;
            _token = token;
        }

        public void Start()
        {
            _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));
            Log.Information("AdminOperationPipeServer listening on pipe '{PipeName}'.", _pipeName);
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            if (_acceptLoop != null)
            {
                try { await _acceptLoop; } catch { /* ignore shutdown exceptions */ }
            }
            _cts.Dispose();
        }

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                NamedPipeServerStream? server = null;
                try
                {
                    server = CreateServerStream();
                    await server.WaitForConnectionAsync(ct).ConfigureAwait(false);

                    // Hand off handling to a background task so the next connection can be accepted.
                    var accepted = server;
                    server = null;
                    _ = Task.Run(() => HandleConnectionAsync(accepted, ct), ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Pipe accept loop error.");
                    // Brief backoff so we don't tight-loop on a persistent failure.
                    try { await Task.Delay(500, ct); } catch { break; }
                }
                finally
                {
                    server?.Dispose();
                }
            }
        }

        private NamedPipeServerStream CreateServerStream()
        {
            var security = new PipeSecurity();
            // SYSTEM: full control.
            security.AddAccessRule(new PipeAccessRule(
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                PipeAccessRights.FullControl,
                AccessControlType.Allow));
            // BUILTIN\Users: read/write, so standard-user agents can connect.
            security.AddAccessRule(new PipeAccessRule(
                new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
                PipeAccessRights.ReadWrite | PipeAccessRights.Synchronize,
                AccessControlType.Allow));

            return NamedPipeServerStreamAcl.Create(
                _pipeName,
                PipeDirection.InOut,
                maxNumberOfServerInstances: NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                inBufferSize: 4096,
                outBufferSize: 4096,
                pipeSecurity: security);
        }

        private async Task HandleConnectionAsync(NamedPipeServerStream server, CancellationToken ct)
        {
            using (server)
            {
                try
                {
                    var requestLine = await ReadLineAsync(server, ct).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(requestLine))
                    {
                        return;
                    }

                    HelperRequest? request;
                    try
                    {
                        request = JsonSerializer.Deserialize<HelperRequest>(requestLine);
                    }
                    catch (JsonException ex)
                    {
                        await WriteResponseAsync(server, new HelperResponse(false, $"Invalid JSON: {ex.Message}"), ct).ConfigureAwait(false);
                        return;
                    }

                    if (request == null)
                    {
                        await WriteResponseAsync(server, new HelperResponse(false, "Empty request."), ct).ConfigureAwait(false);
                        return;
                    }

                    if (!ConstantTimeEquals(request.Token, _token))
                    {
                        Log.Warning("Rejected admin-op request with invalid token (op={Op}).", request.Op);
                        await WriteResponseAsync(server, new HelperResponse(false, "Unauthorized."), ct).ConfigureAwait(false);
                        return;
                    }

                    var response = Dispatch(request);
                    await WriteResponseAsync(server, response, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Pipe connection handler error.");
                }
            }
        }

        private static HelperResponse Dispatch(HelperRequest request)
        {
            return request.Op switch
            {
                HelperOp.Ping => new HelperResponse(true),
                HelperOp.SetSleepTimeouts => AdminOperations.SetSleepTimeouts(
                    request.AcMinutes ?? 30,
                    request.DcMinutes ?? 15),
                HelperOp.ForceShutdown => AdminOperations.ForceShutdown(request.DelaySeconds ?? 0),
                HelperOp.ForceRestart => AdminOperations.ForceRestart(request.DelaySeconds ?? 0),
                _ => new HelperResponse(false, $"Unknown op: {request.Op}")
            };
        }

        private static async Task<string?> ReadLineAsync(Stream stream, CancellationToken ct)
        {
            var buffer = new byte[4096];
            var sb = new StringBuilder();
            while (!ct.IsCancellationRequested)
            {
                var read = await stream.ReadAsync(buffer, ct).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                var chunk = Encoding.UTF8.GetString(buffer, 0, read);
                var newlineIndex = chunk.IndexOf('\n');
                if (newlineIndex >= 0)
                {
                    sb.Append(chunk, 0, newlineIndex);
                    return sb.ToString().TrimEnd('\r');
                }
                sb.Append(chunk);

                if (sb.Length > 64 * 1024)
                {
                    throw new InvalidOperationException("Request exceeded 64 KB.");
                }
            }
            return sb.Length == 0 ? null : sb.ToString().TrimEnd('\r');
        }

        private static async Task WriteResponseAsync(Stream stream, HelperResponse response, CancellationToken ct)
        {
            var payload = JsonSerializer.Serialize(response) + "\n";
            var bytes = Encoding.UTF8.GetBytes(payload);
            await stream.WriteAsync(bytes, ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);
        }

        private static bool ConstantTimeEquals(string a, string b)
        {
            if (a.Length != b.Length) return false;
            var diff = 0;
            for (var i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }
            return diff == 0;
        }
    }
}
