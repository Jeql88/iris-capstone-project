using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using IRIS.Agent.Services.Contracts;

namespace IRIS.Agent.Controllers
{
    public class MonitoringController
    {
        private readonly IMonitoringService _monitoringLogic;
        private readonly IPCService? _pcService;
        private readonly IConfiguration _configuration;
        private System.Threading.Timer? _timer;
        private bool _isMonitoring;
        private readonly int _heartbeatIntervalSeconds;
        private readonly int _metricsIntervalSeconds;
        private readonly int _commandPollIntervalSeconds;
        private DateTime _lastMetricsRunUtc = DateTime.MinValue;
        private DateTime _lastCommandPollRunUtc = DateTime.MinValue;
        // Hardware/OS info refresh: re-register PC hourly to catch Windows
        // feature updates, GPU/RAM swaps, IP/subnet changes, and to recover
        // from a failed startup register (transient DB outage at boot).
        private DateTime _lastRegisterRunUtc = DateTime.MinValue;
        private static readonly TimeSpan RegisterInterval = TimeSpan.FromHours(1);
        // True if the agent's startup register failed and we should retry it
        // on the next successful heartbeat instead of waiting an hour.
        private bool _registerPending;
        private int _isCycleRunning = 0;

        public MonitoringController(IMonitoringService monitoringLogic, IConfiguration configuration, IPCService? pcService = null)
        {
            _monitoringLogic = monitoringLogic;
            _pcService = pcService;
            _configuration = configuration;
            _heartbeatIntervalSeconds = int.Parse(_configuration["AgentSettings:HeartbeatIntervalSeconds"] ?? "30");
            _metricsIntervalSeconds = int.Parse(_configuration["AgentSettings:MetricsIntervalSeconds"] ?? "30");
            _commandPollIntervalSeconds = int.Parse(_configuration["AgentSettings:CommandPollIntervalSeconds"] ?? "5");
        }

        // Called by the host when the startup RegisterPCAsync threw, so the
        // monitoring loop will retry on its next successful heartbeat instead
        // of leaving the PC's hardware/OS info stale until the hourly tick.
        public void MarkRegisterPending() => _registerPending = true;

        public Task StartMonitoringAsync()
        {
            if (_isMonitoring)
            {
                Log.Warning("Monitoring is already running.");
                return Task.CompletedTask;
            }

            _isMonitoring = true;
            Log.Information("Starting monitoring loop with heartbeat interval {Heartbeat}s and metrics interval {Metrics}s",
                _heartbeatIntervalSeconds, _metricsIntervalSeconds);

            var loopIntervalSeconds = Math.Min(Math.Min(_heartbeatIntervalSeconds, _metricsIntervalSeconds), _commandPollIntervalSeconds);

            // Start periodic tasks
            _timer = new System.Threading.Timer(async _ => await PerformMonitoringAsync(), null, TimeSpan.Zero,
                TimeSpan.FromSeconds(loopIntervalSeconds));

            return Task.CompletedTask;
        }

        public Task StopMonitoringAsync()
        {
            if (!_isMonitoring)
            {
                Log.Warning("Monitoring is not running.");
                return Task.CompletedTask;
            }

            _isMonitoring = false;
            _timer?.Dispose();
            Log.Information("Monitoring loop stopped.");
            return Task.CompletedTask;
        }

        private async Task PerformMonitoringAsync()
        {
            if (!_isMonitoring) return;

            if (Interlocked.Exchange(ref _isCycleRunning, 1) == 1)
            {
                return;
            }

            try
            {
                var now = DateTime.UtcNow;

                // Send heartbeat
                await _monitoringLogic.SendHeartbeatAsync();

                // Heartbeat just succeeded, so the DB is reachable. If the
                // startup register failed (transient outage at boot) or the
                // hourly tick has elapsed, re-run RegisterOrUpdatePCAsync to
                // refresh hostname/IP/subnet/gateway/OS and detect hardware
                // changes (e.g. a GPU swap, RAM upgrade, OS feature update).
                if (_pcService != null && (_registerPending || (now - _lastRegisterRunUtc) >= RegisterInterval))
                {
                    try
                    {
                        await _pcService.RegisterOrUpdatePCAsync();
                        _lastRegisterRunUtc = now;
                        if (_registerPending)
                        {
                            Log.Information("Pending RegisterOrUpdatePCAsync recovered after startup failure.");
                            _registerPending = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Periodic RegisterOrUpdatePCAsync failed; will retry on next cycle.");
                    }
                }

                // Capture metrics at configured interval
                if ((now - _lastMetricsRunUtc).TotalSeconds >= _metricsIntervalSeconds)
                {
                    await _monitoringLogic.CaptureHardwareMetricsAsync();
                    await _monitoringLogic.CaptureNetworkMetricsAsync();
                    _lastMetricsRunUtc = now;
                }

                if ((now - _lastCommandPollRunUtc).TotalSeconds >= _commandPollIntervalSeconds)
                {
                    await _monitoringLogic.ProcessPendingPowerCommandAsync();
                    _lastCommandPollRunUtc = now;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during monitoring cycle");
            }
            finally
            {
                Interlocked.Exchange(ref _isCycleRunning, 0);
            }
        }
    }
}