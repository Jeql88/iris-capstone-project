using System;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serilog;
using IRIS.Core.Data;
using IRIS.Core.Models;
using IRIS.Agent.Services.Contracts;

namespace IRIS.Agent.Logic
{
    public class MonitoringLogic : IMonitoringService
    {
        private readonly IRISDbContext _context;
        private readonly string _macAddress;
        private readonly string _pingHost;
        private readonly int _pingTimeoutMs;

        private long _lastBytesSent = -1;
        private long _lastBytesReceived = -1;
        private DateTime _lastNetworkSample = DateTime.MinValue;

        public MonitoringLogic(IRISDbContext context, string macAddress, string pingHost, int pingTimeoutMs)
        {
            _context = context;
            _macAddress = macAddress;
            _pingHost = pingHost;
            _pingTimeoutMs = pingTimeoutMs;
        }

        public async Task SendHeartbeatAsync()
        {
            try
            {
                var pc = await _context.PCs.FirstOrDefaultAsync(p => p.MacAddress == _macAddress);
                if (pc != null)
                {
                    pc.Status = PCStatus.Online;
                    pc.LastSeen = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    Log.Information("Heartbeat sent for PC {MacAddress}", _macAddress);
                }
                else
                {
                    Log.Warning("PC with MAC {MacAddress} not found for heartbeat", _macAddress);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to send heartbeat for PC {MacAddress}", _macAddress);
                throw;
            }
        }

        public async Task CaptureHardwareMetricsAsync()
        {
            try
            {
                var pc = await _context.PCs.FirstOrDefaultAsync(p => p.MacAddress == _macAddress);
                if (pc == null)
                {
                    Log.Warning("PC with MAC {MacAddress} not found for metrics capture", _macAddress);
                    return;
                }

                // Capture CPU usage
                var cpuUsage = GetCpuUsage();

                // Capture RAM usage
                var ramUsage = GetRamUsage();

                // Capture Disk usage
                var diskUsage = GetDiskUsage();

                var metric = new HardwareMetric
                {
                    PCId = pc.Id,
                    CpuUsage = cpuUsage,
                    MemoryUsage = ramUsage,
                    DiskUsage = diskUsage,
                    Timestamp = DateTime.UtcNow
                };

                _context.HardwareMetrics.Add(metric);
                await _context.SaveChangesAsync();

                Log.Information("Hardware metrics captured for PC {MacAddress}: CPU={Cpu}%, RAM={Ram}%, Disk={Disk}%",
                    _macAddress, cpuUsage, ramUsage, diskUsage);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to capture hardware metrics for PC {MacAddress}", _macAddress);
                throw;
            }
        }

        public async Task CaptureNetworkMetricsAsync()
        {
            try
            {
                var pc = await _context.PCs.FirstOrDefaultAsync(p => p.MacAddress == _macAddress);
                if (pc == null)
                {
                    Log.Warning("PC with MAC {MacAddress} not found for network metrics", _macAddress);
                    return;
                }

                var now = DateTime.UtcNow;

                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(nic => nic.OperationalStatus == OperationalStatus.Up &&
                                  nic.NetworkInterfaceType != NetworkInterfaceType.Loopback);

                var primaryNic = interfaces.FirstOrDefault();
                if (primaryNic == null)
                {
                    Log.Warning("No active network interface found for {MacAddress}", _macAddress);
                    return;
                }

                long bytesSent = 0;
                long bytesReceived = 0;
                foreach (var nic in interfaces)
                {
                    var stats = nic.GetIPv4Statistics();
                    bytesSent += stats.BytesSent;
                    bytesReceived += stats.BytesReceived;
                }

                double? uploadMbps = null;
                double? downloadMbps = null;
                if (_lastNetworkSample != DateTime.MinValue && _lastBytesSent >= 0 && _lastBytesReceived >= 0)
                {
                    var seconds = (now - _lastNetworkSample).TotalSeconds;
                    if (seconds > 0)
                    {
                        uploadMbps = (bytesSent - _lastBytesSent) * 8.0 / (seconds * 1_000_000.0);
                        downloadMbps = (bytesReceived - _lastBytesReceived) * 8.0 / (seconds * 1_000_000.0);
                    }
                }

                // Simple latency/packet loss check via ping
                var (latency, packetLoss) = PingHost(_pingHost, 3, _pingTimeoutMs);

                var metric = new NetworkMetric
                {
                    PCId = pc.Id,
                    Timestamp = now,
                    UploadSpeed = uploadMbps,
                    DownloadSpeed = downloadMbps,
                    BytesSent = bytesSent,
                    BytesReceived = bytesReceived,
                    Latency = latency,
                    PacketLoss = packetLoss,
                    IsConnected = true,
                    NetworkInterface = primaryNic.Description
                };

                _context.NetworkMetrics.Add(metric);
                await _context.SaveChangesAsync();

                _lastBytesSent = bytesSent;
                _lastBytesReceived = bytesReceived;
                _lastNetworkSample = now;

                Log.Information("Network metrics captured for PC {MacAddress}: Up={Up:F2} Mbps, Down={Down:F2} Mbps, Latency={Lat:F1} ms, Loss={Loss:F1}%",
                    _macAddress,
                    uploadMbps ?? 0,
                    downloadMbps ?? 0,
                    latency ?? 0,
                    packetLoss ?? 0);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to capture network metrics for PC {MacAddress}", _macAddress);
                throw;
            }
        }

        private static (double? latencyMs, double? packetLossPercent) PingHost(string host, int attempts, int timeoutMs)
        {
            try
            {
                var ping = new Ping();
                int success = 0;
                double total = 0;

                for (int i = 0; i < attempts; i++)
                {
                    var reply = ping.Send(host, timeoutMs);
                    if (reply.Status == IPStatus.Success)
                    {
                        success++;
                        total += reply.RoundtripTime;
                    }
                }

                double? latency = success > 0 ? total / success : null;
                double? loss = attempts > 0 ? (1.0 - (double)success / attempts) * 100.0 : null;
                return (latency, loss);
            }
            catch
            {
                return (null, null);
            }
        }

        private float GetCpuUsage()
        {
            try
            {
                using var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                cpuCounter.NextValue();
                System.Threading.Thread.Sleep(1000); // Wait for accurate reading
                return cpuCounter.NextValue();
            }
            catch
            {
                return 0; // Fallback
            }
        }

        private float GetRamUsage()
        {
            try
            {
                using var ramCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use");
                return ramCounter.NextValue();
            }
            catch
            {
                return 0; // Fallback
            }
        }

        private float GetDiskUsage()
        {
            try
            {
                var drives = DriveInfo.GetDrives().Where(d => d.IsReady);
                if (!drives.Any()) return 0;

                var totalUsed = drives.Sum(d => d.TotalSize - d.AvailableFreeSpace);
                var totalSize = drives.Sum(d => d.TotalSize);
                return totalSize > 0 ? (float)(totalUsed * 100.0 / totalSize) : 0;
            }
            catch
            {
                return 0; // Fallback
            }
        }
    }
}