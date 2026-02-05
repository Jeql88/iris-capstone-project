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
        private Timer? _timer;
        private bool _isMonitoring;
        private readonly int _heartbeatIntervalSeconds;
        private readonly int _metricsIntervalSeconds;

        public MonitoringController(IMonitoringService monitoringLogic, IConfiguration configuration)
        {
            _monitoringLogic = monitoringLogic;
            _configuration = configuration;
            _heartbeatIntervalSeconds = int.Parse(_configuration["AgentSettings:HeartbeatIntervalSeconds"] ?? "30");
            _metricsIntervalSeconds = int.Parse(_configuration["AgentSettings:MetricsIntervalSeconds"] ?? "30");
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

            // Start periodic tasks
            _timer = new Timer(async _ => await PerformMonitoringAsync(), null, TimeSpan.Zero,
                TimeSpan.FromSeconds(Math.Min(_heartbeatIntervalSeconds, _metricsIntervalSeconds)));
            
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

            try
            {
                var now = DateTime.UtcNow;

                // Send heartbeat
                await _monitoringLogic.SendHeartbeatAsync();

                // Capture metrics at configured interval
                if ((int)now.TimeOfDay.TotalSeconds % _metricsIntervalSeconds == 0)
                {
                    await _monitoringLogic.CaptureHardwareMetricsAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during monitoring cycle");
            }
        }
    }
}