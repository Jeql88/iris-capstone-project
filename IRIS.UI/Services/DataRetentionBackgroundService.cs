using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using IRIS.Core.Services.Contracts;

namespace IRIS.UI.Services
{
    /// <summary>
    /// Long-running background service that purges stale hardware metrics,
    /// network metrics, and old alerts once per day at the configured hour.
    /// Uses IServiceScopeFactory so it owns its own DbContext per run.
    /// </summary>
    public sealed class DataRetentionBackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DataRetentionBackgroundService> _logger;
        private Task? _runningTask;

        public DataRetentionBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<DataRetentionBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _runningTask = ExecuteAsync(cancellationToken);
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_runningTask != null)
            {
                try { await _runningTask; }
                catch (OperationCanceledException) { }
            }
        }

        private async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Data retention cleanup service started");

            // Short initial delay to let the app finish starting up
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var targetHour = await GetCleanupHourAsync();
                    var delay = CalculateDelayUntilNextRun(targetHour);

                    _logger.LogInformation(
                        "Next data retention cleanup scheduled in {Hours:F1} hours (at {Hour}:00 UTC)",
                        delay.TotalHours, targetHour);

                    await Task.Delay(delay, stoppingToken);

                    if (stoppingToken.IsCancellationRequested) break;

                    await RunCleanupAsync();
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Data retention cleanup failed; will retry in 1 hour");
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
            }

            _logger.LogInformation("Data retention cleanup service stopped");
        }

        private async Task RunCleanupAsync()
        {
            _logger.LogInformation("Starting data retention cleanup...");

            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IDataRetentionService>();

            var result = await service.PurgeOldDataAsync();

            _logger.LogInformation(
                "Data retention cleanup completed: {HwDeleted} hardware metrics, " +
                "{NetDeleted} network metrics, {AlertDeleted} alerts deleted " +
                "(total: {Total})",
                result.HardwareMetricsDeleted,
                result.NetworkMetricsDeleted,
                result.AlertsDeleted,
                result.TotalDeleted);
        }

        private async Task<int> GetCleanupHourAsync()
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IDataRetentionService>();
                return await service.GetCleanupHourAsync();
            }
            catch
            {
                return 2; // default 2 AM UTC
            }
        }

        private static TimeSpan CalculateDelayUntilNextRun(int targetHourUtc)
        {
            var now = DateTime.UtcNow;
            var todayRun = new DateTime(now.Year, now.Month, now.Day, targetHourUtc, 0, 0, DateTimeKind.Utc);

            if (todayRun <= now)
            {
                todayRun = todayRun.AddDays(1);
            }

            var delay = todayRun - now;

            // Minimum 1-minute delay to prevent a tight loop at the exact target second
            return delay < TimeSpan.FromMinutes(1) ? TimeSpan.FromMinutes(1) : delay;
        }
    }
}
