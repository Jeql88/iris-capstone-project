using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serilog;
using IRIS.Core.Data;
using IRIS.Core.Models;
using IRIS.Agent.Interfaces;

namespace IRIS.Agent.Logic
{
    public class MonitoringLogic : IMonitoringLogic
    {
        private readonly IRISDbContext _context;
        private readonly string _macAddress;

        public MonitoringLogic(IRISDbContext context, string macAddress)
        {
            _context = context;
            _macAddress = macAddress;
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