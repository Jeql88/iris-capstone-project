using System.IO;
using System.Net;
using System.Net.Sockets;
using IRIS.Core.Services.Contracts;
using IRIS.UI.Services.Contracts;
using Microsoft.Extensions.Configuration;

namespace IRIS.UI.Services
{
    public class PowerCommandPollingServer : IPowerCommandPollingServer
    {
        private readonly IPowerCommandQueueService _powerCommandQueueService;
        private readonly int _port;

        private TcpListener? _listener;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _serverTask;

        public PowerCommandPollingServer(IPowerCommandQueueService powerCommandQueueService, IConfiguration configuration)
        {
            _powerCommandQueueService = powerCommandQueueService;
            _port = int.TryParse(configuration["PowerCommandServer:Port"], out var configuredPort)
                ? configuredPort
                : 5091;
        }

        public void Start()
        {
            if (_listener != null)
            {
                return;
            }

            _cancellationTokenSource = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();

            _serverTask = Task.Run(() => AcceptLoopAsync(_cancellationTokenSource.Token));
        }

        public async Task StopAsync()
        {
            if (_listener == null)
            {
                return;
            }

            _cancellationTokenSource?.Cancel();

            try
            {
                _listener.Stop();
            }
            catch
            {
                // Ignore stop exceptions
            }

            if (_serverTask != null)
            {
                await _serverTask;
            }

            _serverTask = null;
            _listener = null;

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }

        private async Task AcceptLoopAsync(CancellationToken cancellationToken)
        {
            if (_listener == null)
            {
                return;
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                    _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch
                {
                    // Keep server alive and continue accepting clients
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            using var _ = client;

            try
            {
                using var networkStream = client.GetStream();
                using var reader = new StreamReader(networkStream);
                using var writer = new StreamWriter(networkStream) { AutoFlush = true };

                var macAddress = await reader.ReadLineAsync();
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(macAddress))
                {
                    await writer.WriteLineAsync("NONE");
                    return;
                }

                var command = await _powerCommandQueueService.DequeuePendingCommandAsync(macAddress);
                await writer.WriteLineAsync(string.IsNullOrWhiteSpace(command) ? "NONE" : command);
            }
            catch
            {
                // Ignore per-client errors
            }
        }
    }
}