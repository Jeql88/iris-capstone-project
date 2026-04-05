using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using IRIS.Core.Data;
using IRIS.Core.Services.Contracts;
using IRIS.UI.Services.Contracts;

namespace IRIS.UI.Services
{
    /// <summary>
    /// Periodically checks for PCs that should be auto-shut-down but are sleeping
    /// (no heartbeat beyond the idle threshold). Sends Wake-on-LAN to wake them,
    /// then queues a shutdown command via the existing power command queue.
    /// </summary>
    public sealed class AutoShutdownEnforcementService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IPowerCommandQueueService _powerCommandQueue;
        private readonly IWakeOnLanService _wakeOnLanService;
        private readonly ILogger<AutoShutdownEnforcementService> _logger;

        private readonly bool _enabled;
        private readonly int _checkIntervalSeconds;
        private readonly int _wolCooldownMinutes;
        private readonly int _maxWolPerCycle;
        private readonly int _maxOfflineMinutes;

        private readonly ConcurrentDictionary<string, DateTime> _wolCooldown = new();
        private Task? _runningTask;

        public AutoShutdownEnforcementService(
            IServiceScopeFactory scopeFactory,
            IPowerCommandQueueService powerCommandQueue,
            IWakeOnLanService wakeOnLanService,
            IConfiguration configuration,
            ILogger<AutoShutdownEnforcementService> logger)
        {
            _scopeFactory = scopeFactory;
            _powerCommandQueue = powerCommandQueue;
            _wakeOnLanService = wakeOnLanService;
            _logger = logger;

            _enabled = bool.TryParse(configuration["AutoShutdownEnforcement:Enabled"], out var en) && en;
            _checkIntervalSeconds = int.TryParse(configuration["AutoShutdownEnforcement:CheckIntervalSeconds"], out var ci) ? ci : 60;
            _wolCooldownMinutes = int.TryParse(configuration["AutoShutdownEnforcement:WolCooldownMinutes"], out var wc) ? wc : 10;
            _maxWolPerCycle = int.TryParse(configuration["AutoShutdownEnforcement:MaxWolPerCycle"], out var mw) ? mw : 5;
            _maxOfflineMinutes = int.TryParse(configuration["AutoShutdownEnforcement:MaxOfflineMinutes"], out var mo) ? mo : 120;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (!_enabled)
            {
                _logger.LogInformation("Auto-shutdown enforcement is disabled");
                return Task.CompletedTask;
            }

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
            _logger.LogInformation("Auto-shutdown enforcement service started (interval: {Interval}s, cooldown: {Cooldown}m, max WoL/cycle: {Max})",
                _checkIntervalSeconds, _wolCooldownMinutes, _maxWolPerCycle);

            // Let the app finish starting up
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await EnforceAutoShutdownAsync();
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Auto-shutdown enforcement cycle failed");
                }

                await Task.Delay(TimeSpan.FromSeconds(_checkIntervalSeconds), stoppingToken);
            }

            _logger.LogInformation("Auto-shutdown enforcement service stopped");
        }

        private async Task EnforceAutoShutdownAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IRISDbContext>();

            var roomsWithPolicy = await context.Rooms
                .Include(r => r.PCs)
                .Include(r => r.Policies)
                .Where(r => r.Policies.Any(p => p.IsActive && p.AutoShutdownIdleMinutes != null))
                .AsNoTracking()
                .ToListAsync();

            var now = DateTime.UtcNow;
            var wolsSentThisCycle = 0;
            CleanupCooldownMap(now);

            foreach (var room in roomsWithPolicy)
            {
                var policy = room.Policies.First(p => p.IsActive && p.AutoShutdownIdleMinutes.HasValue);
                var idleThreshold = TimeSpan.FromMinutes(policy.AutoShutdownIdleMinutes!.Value);
                var maxOffline = TimeSpan.FromMinutes(Math.Min(_maxOfflineMinutes, policy.AutoShutdownIdleMinutes.Value * 2));

                foreach (var pc in room.PCs)
                {
                    if (wolsSentThisCycle >= _maxWolPerCycle)
                        return;

                    if (string.IsNullOrWhiteSpace(pc.MacAddress))
                        continue;

                    var sinceLastSeen = now - pc.LastSeen;

                    // PC is still actively reporting — agent-side idle detection handles this
                    if (sinceLastSeen < idleThreshold)
                        continue;

                    // PC has been offline too long — likely powered off, not sleeping
                    if (sinceLastSeen > maxOffline)
                        continue;

                    var normalizedMac = NormalizeMac(pc.MacAddress);

                    // Cooldown check — don't WoL the same PC too frequently
                    if (_wolCooldown.TryGetValue(normalizedMac, out var lastWol) &&
                        (now - lastWol).TotalMinutes < _wolCooldownMinutes)
                        continue;

                    // Send WoL to wake the sleeping PC
                    var sent = await _wakeOnLanService.SendWakeOnLanAsync(pc.MacAddress);
                    if (!sent)
                        continue;

                    // Queue shutdown command — the agent will pick it up after waking
                    await _powerCommandQueue.QueueCommandAsync(pc.MacAddress, "Shutdown");
                    _wolCooldown[normalizedMac] = now;
                    wolsSentThisCycle++;

                    _logger.LogInformation(
                        "Auto-shutdown enforcement: WoL + shutdown queued for PC {Hostname} ({Mac}) in {Room} (idle {Minutes:F0} min)",
                        pc.Hostname ?? "unknown", pc.MacAddress, room.RoomNumber, sinceLastSeen.TotalMinutes);
                }
            }
        }

        private void CleanupCooldownMap(DateTime now)
        {
            var expiry = TimeSpan.FromMinutes(_wolCooldownMinutes * 2);
            foreach (var kvp in _wolCooldown)
            {
                if (now - kvp.Value > expiry)
                    _wolCooldown.TryRemove(kvp.Key, out _);
            }
        }

        private static string NormalizeMac(string mac) =>
            mac.Replace(":", "").Replace("-", "").Replace(" ", "").Trim().ToUpperInvariant();
    }
}
