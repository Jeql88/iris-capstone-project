using IRIS.Core.Data;
using IRIS.Core.DTOs;
using IRIS.Core.Services.Contracts;
using IRIS.Core.Services.ServiceModels;
using Microsoft.EntityFrameworkCore;

namespace IRIS.Core.Services
{
    public class MonitoringService : IMonitoringService
    {
        private readonly IRISDbContext _context;

        public MonitoringService(IRISDbContext context)
        {
            _context = context;
        }

        public async Task<DashboardSummary> GetDashboardSummaryAsync(int? roomId = null)
        {
            var since = DateTime.UtcNow.AddHours(-24);

            var pcQuery = _context.PCs.AsQueryable();
            if (roomId.HasValue)
                pcQuery = pcQuery.Where(p => p.RoomId == roomId.Value);

            var totalPCs = await pcQuery.CountAsync();
            var onlinePCs = await pcQuery.CountAsync(p => p.Status == Models.PCStatus.Online);
            var offlinePCs = await pcQuery.CountAsync(p => p.Status == Models.PCStatus.Offline);
            var warningPCs = await pcQuery.CountAsync(p => p.Status == Models.PCStatus.Warning);

            var networkQuery = _context.NetworkMetrics
                .Where(nm => nm.Timestamp >= since);

            if (roomId.HasValue)
            {
                networkQuery = networkQuery.Where(nm => _context.PCs.Any(p => p.Id == nm.PCId && p.RoomId == roomId.Value));
            }

            var networkList = await networkQuery.ToListAsync();

            var avgLatency = networkList.Any() ? networkList.Average(n => n.Latency ?? 0) : 0;
            var avgPacketLoss = networkList.Any() ? networkList.Average(n => n.PacketLoss ?? 0) : 0;
            var currentBandwidth = networkList.Any() ? networkList.Where(n => n.Timestamp >= DateTime.UtcNow.AddHours(-1)).Average(n => (n.DownloadSpeed ?? 0) + (n.UploadSpeed ?? 0)) : 0;
            var peakBandwidth = networkList.Any() ? networkList.Max(n => (n.DownloadSpeed ?? 0) + (n.UploadSpeed ?? 0)) : 0;

            var labStatuses = await GetActiveLabPCsAsync();
            var heavyApps = await GetHeavyApplicationsAsync(roomId);

            return new DashboardSummary
            {
                AverageLatency = avgLatency,
                AveragePacketLoss = avgPacketLoss,
                CurrentBandwidth = currentBandwidth,
                PeakBandwidth = peakBandwidth,
                TotalPCs = totalPCs,
                OnlinePCs = onlinePCs,
                OfflinePCs = offlinePCs,
                WarningPCs = warningPCs,
                LabStatuses = labStatuses,
                HeavyApplications = heavyApps
            };
        }

        public async Task<DashboardMetrics> GetDashboardMetricsAsync(int? roomId = null)
        {
            var query = _context.PCs.AsQueryable();
            if (roomId.HasValue)
                query = query.Where(p => p.RoomId == roomId.Value);

            var totalPCs = await query.CountAsync();
            var onlinePCs = await query.CountAsync(p => p.Status == Models.PCStatus.Online);

            var latestMetrics = await _context.NetworkMetrics
                .Where(nm => roomId == null || _context.PCs.Any(p => p.Id == nm.PCId && p.RoomId == roomId.Value))
                .Where(nm => nm.Timestamp >= DateTime.UtcNow.AddHours(-1))
                .ToListAsync();

            return new DashboardMetrics
            {
                AverageLatency = latestMetrics.Any() ? latestMetrics.Average(m => m.Latency ?? 0) : 0,
                CurrentBandwidth = latestMetrics.Any() ? latestMetrics.Average(m => (m.DownloadSpeed ?? 0) + (m.UploadSpeed ?? 0)) : 0,
                PeakBandwidth = latestMetrics.Any() ? latestMetrics.Max(m => (m.DownloadSpeed ?? 0) + (m.UploadSpeed ?? 0)) : 0,
                TotalPCs = totalPCs,
                OnlinePCs = onlinePCs
            };
        }

        public async Task<List<LatencyDataPoint>> GetLatencyHistoryAsync(int hours = 24)
        {
            var since = DateTime.UtcNow.AddHours(-hours);
            return await _context.NetworkMetrics
                .Where(nm => nm.Timestamp >= since)
                .GroupBy(nm => new { nm.Timestamp.Hour, nm.Timestamp.Minute })
                .Select(g => new LatencyDataPoint
                {
                    Timestamp = g.Min(x => x.Timestamp),
                    Value = g.Average(x => x.Latency ?? 0)
                })
                .OrderBy(d => d.Timestamp)
                .ToListAsync();
        }

        public async Task<List<BandwidthDataPoint>> GetBandwidthHistoryAsync(int hours = 24)
        {
            var since = DateTime.UtcNow.AddHours(-hours);
            return await _context.NetworkMetrics
                .Where(nm => nm.Timestamp >= since)
                .GroupBy(nm => new { nm.Timestamp.Hour, nm.Timestamp.Minute })
                .Select(g => new BandwidthDataPoint
                {
                    Timestamp = g.Min(x => x.Timestamp),
                    Value = g.Average(x => (x.DownloadSpeed ?? 0) + (x.UploadSpeed ?? 0))
                })
                .OrderBy(d => d.Timestamp)
                .ToListAsync();
        }

        public async Task<List<PacketLossDataPoint>> GetPacketLossHistoryAsync(int hours = 24)
        {
            var since = DateTime.UtcNow.AddHours(-hours);
            return await _context.NetworkMetrics
                .Where(nm => nm.Timestamp >= since)
                .GroupBy(nm => new { nm.Timestamp.Hour, nm.Timestamp.Minute })
                .Select(g => new PacketLossDataPoint
                {
                    Timestamp = g.Min(x => x.Timestamp),
                    Value = g.Average(x => x.PacketLoss ?? 0)
                })
                .OrderBy(d => d.Timestamp)
                .ToListAsync();
        }

        public async Task<List<HeavyApplication>> GetHeavyApplicationsAsync(int? roomId = null)
        {
            var query = from suh in _context.SoftwareUsageHistory
                        join pc in _context.PCs on suh.PCId equals pc.Id
                        where roomId == null || pc.RoomId == roomId.Value
                        where suh.StartTime >= DateTime.UtcNow.AddHours(-24)
                        group suh by suh.ApplicationName into g
                        select new HeavyApplication
                        {
                            Name = g.Key,
                            Icon = g.Key.Contains("Photoshop") ? "PS" :
                                   g.Key.Contains("Visual Studio") ? "VS" :
                                   g.Key.Contains("Chrome") ? "CH" :
                                   g.Key.Contains("Blender") ? "BL" : "APP",
                            InstanceCount = g.Count(),
                            AverageRamUsage = 0
                        };

            return await query.OrderByDescending(a => a.InstanceCount).Take(6).ToListAsync();
        }

        public async Task<Dictionary<string, int>> GetActiveLabPCsAsync()
        {
            // Get all PCs with their room information
            var allPCs = await _context.PCs
                .Include(p => p.Room)
                .ToListAsync();

            // Get online PCs grouped by room number
            var onlinePCsByRoom = allPCs
                .Where(p => p.Status == Models.PCStatus.Online)
                .GroupBy(p => p.Room?.RoomNumber ?? "Unassigned")
                .ToDictionary(g => g.Key, g => g.Count());

            // Get all unique rooms from PCs in database
            var uniqueRoomNumbers = allPCs
                .Where(p => p.Room != null)
                .Select(p => p.Room!.RoomNumber)
                .Distinct()
                .ToList();

            // Build result dictionary with all rooms that have PCs
            var result = new Dictionary<string, int>();
            foreach (var roomNumber in uniqueRoomNumbers)
            {
                result[roomNumber] = onlinePCsByRoom.ContainsKey(roomNumber) ? onlinePCsByRoom[roomNumber] : 0;
            }

            // Add unassigned PCs if any exist
            if (onlinePCsByRoom.ContainsKey("Unassigned") && !result.ContainsKey("Unassigned"))
            {
                result["Unassigned"] = onlinePCsByRoom["Unassigned"];
            }

            return result;
        }

        public async Task<List<RoomDto>> GetRoomsAsync()
        {
            return await _context.Rooms
                .AsNoTracking()
                .OrderBy(r => r.RoomNumber)
                .Select(r => new RoomDto(
                    r.Id,
                    r.RoomNumber,
                    r.Description,
                    r.Capacity,
                    r.IsActive,
                    r.CreatedAt))
                .ToListAsync();
        }

        public async Task<List<PCMonitorInfo>> GetPCsForMonitorAsync(int? roomId = null)
        {
            var query = _context.PCs
                .Include(p => p.HardwareMetrics)
                .Include(p => p.NetworkMetrics)
                .Include(p => p.UserLogs)
                .Include(p => p.Room)
                .AsQueryable();

            if (roomId.HasValue)
                query = query.Where(p => p.RoomId == roomId.Value);

            var pcs = await query.ToListAsync();

            return pcs.Select(pc =>
            {
                var latestMetric = pc.HardwareMetrics
                    .OrderByDescending(m => m.Timestamp)
                    .FirstOrDefault();

                var latestUser = pc.UserLogs
                    .OrderByDescending(u => u.Timestamp)
                    .FirstOrDefault();

                var latestNetwork = pc.NetworkMetrics
                    .OrderByDescending(n => n.Timestamp)
                    .FirstOrDefault();

                var networkUsage = (latestNetwork?.DownloadSpeed ?? 0) + (latestNetwork?.UploadSpeed ?? 0);

                return new PCMonitorInfo
                {
                    Id = pc.Id,
                    Name = pc.Hostname ?? "Unknown",
                    IpAddress = pc.IpAddress ?? "N/A",
                    MacAddress = pc.MacAddress ?? "",
                    RoomName = pc.Room?.RoomNumber ?? "Unassigned",
                    OperatingSystem = pc.OperatingSystem ?? "Unknown",
                    Status = pc.Status.ToString(),
                    CpuUsage = latestMetric?.CpuUsage ?? 0,
                    CpuTemperature = latestMetric?.CpuTemperature,
                    GpuUsage = latestMetric?.GpuUsage,
                    GpuTemperature = latestMetric?.GpuTemperature,
                    RamUsage = latestMetric?.MemoryUsage ?? 0,
                    DiskUsage = latestMetric?.DiskUsage ?? 0,
                    NetworkUsage = networkUsage,
                    NetworkUploadMbps = latestNetwork?.UploadSpeed ?? 0,
                    NetworkDownloadMbps = latestNetwork?.DownloadSpeed ?? 0,
                    NetworkLatencyMs = latestNetwork?.Latency,
                    PacketLossPercent = latestNetwork?.PacketLoss,
                    LastMetricTimestamp = latestMetric?.Timestamp ?? latestNetwork?.Timestamp,
                    User = latestUser?.User?.Username ?? ""
                };
            }).ToList();
        }

        public async Task<PCStatusCounts> GetPCStatusCountsAsync(int? roomId = null)
        {
            var query = _context.PCs.AsQueryable();
            if (roomId.HasValue)
                query = query.Where(p => p.RoomId == roomId.Value);

            return new PCStatusCounts
            {
                OnlineCount = await query.CountAsync(p => p.Status == Models.PCStatus.Online),
                OfflineCount = await query.CountAsync(p => p.Status == Models.PCStatus.Offline),
                WarningCount = await query.CountAsync(p => p.Status == Models.PCStatus.Warning)
            };
        }

        public async Task<List<LiveAlertItem>> GetLiveAlertsAsync(int? roomId = null, int maxItems = 50)
        {
            var lookback = DateTime.UtcNow.AddMinutes(-5);

            var query = _context.PCs
                .Include(p => p.Room)
                .ThenInclude(r => r.Policies)
                .Include(p => p.HardwareMetrics)
                .Include(p => p.NetworkMetrics)
                .AsQueryable();

            if (roomId.HasValue)
                query = query.Where(p => p.RoomId == roomId.Value);

            var pcs = await query.ToListAsync();
            var alerts = new List<LiveAlertItem>();

            foreach (var pc in pcs)
            {
                var pcName = pc.Hostname ?? $"PC-{pc.Id}";
                var roomName = pc.Room?.RoomNumber ?? "Unassigned";
                var activePolicy = pc.Room?.Policies?.FirstOrDefault(p => p.IsActive);
                var latestHardware = pc.HardwareMetrics.OrderByDescending(x => x.Timestamp).FirstOrDefault();
                var latestNetwork = pc.NetworkMetrics.OrderByDescending(x => x.Timestamp).FirstOrDefault();
                var cpuHistory = pc.HardwareMetrics.Where(m => m.CpuUsage.HasValue).Select(m => (m.Timestamp, m.CpuUsage!.Value));
                var ramHistory = pc.HardwareMetrics.Where(m => m.MemoryUsage.HasValue).Select(m => (m.Timestamp, m.MemoryUsage!.Value));
                var diskHistory = pc.HardwareMetrics.Where(m => m.DiskUsage.HasValue).Select(m => (m.Timestamp, m.DiskUsage!.Value));
                var cpuTempHistory = pc.HardwareMetrics.Where(m => m.CpuTemperature.HasValue).Select(m => (m.Timestamp, m.CpuTemperature!.Value));
                var gpuTempHistory = pc.HardwareMetrics.Where(m => m.GpuTemperature.HasValue).Select(m => (m.Timestamp, m.GpuTemperature!.Value));
                var packetLossHistory = pc.NetworkMetrics.Where(m => m.PacketLoss.HasValue).Select(m => (m.Timestamp, m.PacketLoss!.Value));
                var latencyHistory = pc.NetworkMetrics.Where(m => m.Latency.HasValue).Select(m => (m.Timestamp, m.Latency!.Value));

                if (pc.Status == Models.PCStatus.Offline || pc.LastSeen < lookback)
                {
                    alerts.Add(CreateAlert(pc.Id, pcName, roomName, "Critical", "System", "PC is offline or heartbeat is stale", pc.LastSeen));
                }

                if (latestHardware != null)
                {
                    AddThresholdAlert(alerts, pc.Id, pcName, roomName, "Hardware", "CPU", latestHardware.CpuUsage,
                        cpuHistory,
                        activePolicy?.CpuUsageWarningThreshold ?? 85,
                        activePolicy?.CpuUsageCriticalThreshold ?? 95,
                        activePolicy?.WarningSustainSeconds ?? 30,
                        activePolicy?.CriticalSustainSeconds ?? 20,
                        latestHardware.Timestamp, "%");

                    AddThresholdAlert(alerts, pc.Id, pcName, roomName, "Hardware", "RAM", latestHardware.MemoryUsage,
                        ramHistory,
                        activePolicy?.RamUsageWarningThreshold ?? 85,
                        activePolicy?.RamUsageCriticalThreshold ?? 95,
                        activePolicy?.WarningSustainSeconds ?? 30,
                        activePolicy?.CriticalSustainSeconds ?? 20,
                        latestHardware.Timestamp, "%");

                    AddThresholdAlert(alerts, pc.Id, pcName, roomName, "Hardware", "Disk", latestHardware.DiskUsage,
                        diskHistory,
                        activePolicy?.DiskUsageWarningThreshold ?? 90,
                        activePolicy?.DiskUsageCriticalThreshold ?? 98,
                        activePolicy?.WarningSustainSeconds ?? 30,
                        activePolicy?.CriticalSustainSeconds ?? 20,
                        latestHardware.Timestamp, "%");

                    AddThresholdAlert(alerts, pc.Id, pcName, roomName, "Thermal", "CPU temperature", latestHardware.CpuTemperature,
                        cpuTempHistory,
                        activePolicy?.CpuTemperatureWarningThreshold ?? 80,
                        activePolicy?.CpuTemperatureCriticalThreshold ?? 90,
                        activePolicy?.WarningSustainSeconds ?? 30,
                        activePolicy?.CriticalSustainSeconds ?? 20,
                        latestHardware.Timestamp, " °C");

                    AddThresholdAlert(alerts, pc.Id, pcName, roomName, "Thermal", "GPU temperature", latestHardware.GpuTemperature,
                        gpuTempHistory,
                        activePolicy?.GpuTemperatureWarningThreshold ?? 80,
                        activePolicy?.GpuTemperatureCriticalThreshold ?? 90,
                        activePolicy?.WarningSustainSeconds ?? 30,
                        activePolicy?.CriticalSustainSeconds ?? 20,
                        latestHardware.Timestamp, " °C");
                }

                if (latestNetwork != null)
                {
                    AddThresholdAlert(alerts, pc.Id, pcName, roomName, "Network", "Packet loss", latestNetwork.PacketLoss,
                        packetLossHistory,
                        activePolicy?.PacketLossWarningThreshold ?? 3,
                        activePolicy?.PacketLossCriticalThreshold ?? 10,
                        activePolicy?.WarningSustainSeconds ?? 30,
                        activePolicy?.CriticalSustainSeconds ?? 20,
                        latestNetwork.Timestamp, "%");

                    AddThresholdAlert(alerts, pc.Id, pcName, roomName, "Network", "Latency", latestNetwork.Latency,
                        latencyHistory,
                        activePolicy?.LatencyWarningThreshold ?? 150,
                        activePolicy?.LatencyCriticalThreshold ?? 300,
                        activePolicy?.WarningSustainSeconds ?? 30,
                        activePolicy?.CriticalSustainSeconds ?? 20,
                        latestNetwork.Timestamp, " ms");
                }
            }

            return alerts
                .OrderByDescending(a => a.SeverityRank)
                .ThenByDescending(a => a.Timestamp)
                .Take(Math.Max(1, maxItems))
                .ToList();
        }

        public async Task<PCHardwareConfigDto?> GetPCHardwareConfigAsync(int pcId)
        {
            var config = await _context.PCHardwareConfigs
                .Where(c => c.PCId == pcId && c.IsActive)
                .OrderByDescending(c => c.AppliedAt)
                .FirstOrDefaultAsync();

            if (config == null) return null;

            return new PCHardwareConfigDto(
                Id: config.Id,
                PCId: config.PCId,
                Processor: config.Processor,
                GraphicsCard: config.GraphicsCard,
                Motherboard: config.Motherboard,
                RamCapacity: config.RamCapacity,
                StorageCapacity: config.StorageCapacity,
                StorageType: config.StorageType,
                AppliedAt: config.AppliedAt,
                IsActive: config.IsActive
            );
        }

        private static void AddThresholdAlert(
            List<LiveAlertItem> alerts,
            int pcId,
            string pcName,
            string roomName,
            string type,
            string metricName,
            double? value,
            IEnumerable<(DateTime Timestamp, double Value)> metricHistory,
            double warningThreshold,
            double criticalThreshold,
            int warningSustainSeconds,
            int criticalSustainSeconds,
            DateTime timestamp,
            string unit)
        {
            if (!value.HasValue)
            {
                return;
            }

            if (value.Value >= criticalThreshold && IsSustained(metricHistory, criticalThreshold, criticalSustainSeconds, timestamp))
            {
                alerts.Add(CreateAlert(pcId, pcName, roomName, "Critical", type, $"{metricName} is critical ({value.Value:F1}{unit}) for {criticalSustainSeconds}s", timestamp));
                return;
            }

            if (value.Value >= warningThreshold && IsSustained(metricHistory, warningThreshold, warningSustainSeconds, timestamp))
            {
                alerts.Add(CreateAlert(pcId, pcName, roomName, "High", type, $"{metricName} is high ({value.Value:F1}{unit}) for {warningSustainSeconds}s", timestamp));
            }
        }

        private static bool IsSustained(
            IEnumerable<(DateTime Timestamp, double Value)> history,
            double threshold,
            int sustainSeconds,
            DateTime referenceTimestamp)
        {
            if (sustainSeconds <= 0)
            {
                return true;
            }

            var windowStart = referenceTimestamp.AddSeconds(-sustainSeconds);
            var inWindow = history
                .Where(x => x.Timestamp >= windowStart && x.Timestamp <= referenceTimestamp)
                .OrderBy(x => x.Timestamp)
                .ToList();

            if (!inWindow.Any())
            {
                return false;
            }

            if (inWindow.Any(x => x.Value < threshold))
            {
                return false;
            }

            var earliest = inWindow.First().Timestamp;
            return earliest <= windowStart || (referenceTimestamp - earliest).TotalSeconds >= sustainSeconds * 0.8;
        }

        private static LiveAlertItem CreateAlert(
            int pcId,
            string pcName,
            string roomName,
            string severity,
            string type,
            string message,
            DateTime timestamp)
        {
            return new LiveAlertItem
            {
                PCId = pcId,
                PCName = pcName,
                RoomName = roomName,
                Severity = severity,
                Type = type,
                Message = message,
                Timestamp = timestamp,
                SeverityRank = severity switch
                {
                    "Critical" => 4,
                    "High" => 3,
                    "Medium" => 2,
                    _ => 1
                }
            };
        }
    }
}
