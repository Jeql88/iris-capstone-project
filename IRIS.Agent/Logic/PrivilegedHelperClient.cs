using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Serilog;
using IRIS.Agent.Helper;

namespace IRIS.Agent.Logic
{
    /// <summary>
    /// Named-pipe client that sends admin-operation requests to the SYSTEM-level helper.
    /// </summary>
    public sealed class PrivilegedHelperClient : IPrivilegedHelperClient
    {
        private readonly string _pipeName;
        private readonly string _pipeToken;
        private const int ConnectTimeoutMs = 2000;

        public PrivilegedHelperClient(IConfiguration configuration)
        {
            _pipeName = configuration["HelperSettings:PipeName"] ?? "IRIS.Agent.Helper";
            _pipeToken = configuration["HelperSettings:PipeToken"] ?? string.Empty;
        }

        public async Task SetSleepTimeoutsAsync(int acMinutes, int dcMinutes)
        {
            var request = new HelperRequest(HelperOp.SetSleepTimeouts, _pipeToken, AcMinutes: acMinutes, DcMinutes: dcMinutes);
            var response = await SendRequestAsync(request);
            if (!response.Ok)
                Log.Warning("SetSleepTimeouts failed via helper: {Error}", response.Error);
        }

        public async Task ForceShutdownAsync(int delaySeconds = 0)
        {
            var request = new HelperRequest(HelperOp.ForceShutdown, _pipeToken, DelaySeconds: delaySeconds);
            var response = await SendRequestAsync(request);
            if (!response.Ok)
                throw new HelperOperationException($"ForceShutdown failed: {response.Error}");
        }

        public async Task ForceRestartAsync(int delaySeconds = 0)
        {
            var request = new HelperRequest(HelperOp.ForceRestart, _pipeToken, DelaySeconds: delaySeconds);
            var response = await SendRequestAsync(request);
            if (!response.Ok)
                throw new HelperOperationException($"ForceRestart failed: {response.Error}");
        }

        public async Task PingAsync()
        {
            var request = new HelperRequest(HelperOp.Ping, _pipeToken);
            var response = await SendRequestAsync(request);
            if (!response.Ok)
                throw new HelperOperationException($"Ping failed: {response.Error}");
        }

        private async Task<HelperResponse> SendRequestAsync(HelperRequest request)
        {
            try
            {
                using var pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                await pipe.ConnectAsync(ConnectTimeoutMs);

                // Send JSON-line request.
                var payload = JsonSerializer.Serialize(request) + "\n";
                var bytes = Encoding.UTF8.GetBytes(payload);
                await pipe.WriteAsync(bytes);
                await pipe.FlushAsync();

                // Read JSON-line response.
                var responseLine = await ReadLineAsync(pipe);
                if (string.IsNullOrWhiteSpace(responseLine))
                    return new HelperResponse(false, "Empty response from helper.");

                return JsonSerializer.Deserialize<HelperResponse>(responseLine)
                       ?? new HelperResponse(false, "Null deserialized response.");
            }
            catch (TimeoutException)
            {
                Log.Warning("Helper pipe connection timed out (pipe={PipeName}).", _pipeName);
                throw new HelperUnavailableException("Helper pipe connection timed out.");
            }
            catch (IOException ex)
            {
                Log.Warning(ex, "Helper pipe I/O error (pipe={PipeName}).", _pipeName);
                throw new HelperUnavailableException("Helper pipe I/O error.", ex);
            }
        }

        private static async Task<string?> ReadLineAsync(Stream stream)
        {
            var buffer = new byte[4096];
            var sb = new StringBuilder();

            while (true)
            {
                var read = await stream.ReadAsync(buffer);
                if (read == 0) break;

                var chunk = Encoding.UTF8.GetString(buffer, 0, read);
                var newlineIndex = chunk.IndexOf('\n');
                if (newlineIndex >= 0)
                {
                    sb.Append(chunk, 0, newlineIndex);
                    return sb.ToString().TrimEnd('\r');
                }
                sb.Append(chunk);

                if (sb.Length > 64 * 1024)
                    throw new InvalidOperationException("Response exceeded 64 KB.");
            }

            return sb.Length == 0 ? null : sb.ToString().TrimEnd('\r');
        }
    }

    /// <summary>Thrown when the helper pipe is unreachable.</summary>
    public sealed class HelperUnavailableException : Exception
    {
        public HelperUnavailableException(string message) : base(message) { }
        public HelperUnavailableException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>Thrown when the helper returns a failure response.</summary>
    public sealed class HelperOperationException : Exception
    {
        public HelperOperationException(string message) : base(message) { }
    }
}
