using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IRIS.UI.Services
{
    public sealed class MonitoringBackgroundService
    {
        private readonly IPCDataCacheService _cache;
        private readonly ILogger<MonitoringBackgroundService> _logger;
        private Task? _runningTask;
        private CancellationTokenSource? _cts;

        public MonitoringBackgroundService(
            IPCDataCacheService cache,
            ILogger<MonitoringBackgroundService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _runningTask = ExecuteAsync(_cts.Token);
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _cts?.Cancel();
            if (_runningTask != null)
            {
                try { await _runningTask; }
                catch (OperationCanceledException) { }
            }
        }

        private async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Monitoring background service started");

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _cache.RefreshPCDataAsync();
                    await _cache.RefreshLiveAlertsAsync();
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Monitoring background refresh failed");
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
            }

            _logger.LogInformation("Monitoring background service stopped");
        }
    }
}
