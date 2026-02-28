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
        private readonly IConfiguration _configuration;
        private System.Threading.Timer? _timer;
        private bool _isMonitoring;
        private readonly int _heartbeatIntervalSeconds;
        private readonly int _metricsIntervalSeconds;
        private readonly int _commandPollIntervalSeconds;
        private DateTime _lastMetricsRunUtc = DateTime.MinValue;
        private DateTime _lastCommandPollRunUtc = DateTime.MinValue;
        private int _isCycleRunning = 0;

        public MonitoringController(IMonitoringService monitoringLogic, IConfiguration configuration)
        {
            _monitoringLogic = monitoringLogic;
            _configuration = configuration;
            _heartbeatIntervalSeconds = int.Parse(_configuration["AgentSettings:HeartbeatIntervalSeconds"] ?? "30");
            _metricsIntervalSeconds = int.Parse(_configuration["AgentSettings:MetricsIntervalSeconds"] ?? "30");
            _commandPollIntervalSeconds = int.Parse(_configuration["AgentSettings:CommandPollIntervalSeconds"] ?? "5");
        }

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