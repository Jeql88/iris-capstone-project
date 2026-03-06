using IRIS.Core.Data;
using IRIS.Core.DTOs;
using IRIS.Core.Models;
using IRIS.Core.Services.Contracts;
using IRIS.Core.Services.ServiceModels;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace IRIS.Core.Services
{
    public class MonitoringService : IMonitoringService
    {
        private readonly IRISDbContext _context;
        private static readonly TimeSpan MonitorHeartbeatGrace = TimeSpan.FromMinutes(3);

        public MonitoringService(IRISDbContext context)
        {
            _context = context;
        }

        public async Task<DashboardSummary> GetDashboardSummaryAsync(int? roomId = null)
        {
            var since = DateTime.UtcNow.AddHours(-24);
            var nowUtc = DateTime.UtcNow;

            var pcQuery = _context.PCs.AsQueryable();
            if (roomId.HasValue)
                pcQuery = pcQuery.Where(p => p.RoomId == roomId.Value);

            var allPcs = await pcQuery.ToListAsync();
            var totalPCs = allPcs.Count;
            var onlinePCs = allPcs.Count(p => IsPcOnlineForMonitor(p, nowUtc));
            var offlinePCs = totalPCs - onlinePCs;
            var warningPCs = 0;

            var networkQuery = _context.NetworkMetrics
                .Where(nm => nm.Timestamp >= since);

            if (roomId.HasValue)
            {
                networkQuery = networkQuery.Where(nm => _context.PCs.Any(p => p.Id == nm.PCId && p.RoomId == roomId.Value));
            }

            var networkList = await networkQuery.ToListAsync();

            var avgLatency = networkList.Any() ? networkList.Average(n => n.Latency ?? 0) : 0;
            var avgPacketLoss = networkList.Any() ? networkList.Average(n => n.PacketLoss ?? 0) : 0;
            var recentNetwork = networkList.Where(n => n.Timestamp >= DateTime.UtcNow.AddHours(-1)).ToList();
            var currentBandwidth = recentNetwork.Any() ? recentNetwork.Average(n => (n.DownloadSpeed ?? 0) + (n.UploadSpeed ?? 0)) : 0;
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
            var nowUtc = DateTime.UtcNow;
            var query = _context.PCs.AsQueryable();
            if (roomId.HasValue)
                query = query.Where(p => p.RoomId == roomId.Value);

            var allPcs = await query.ToListAsync();
            var totalPCs = allPcs.Count;
            var onlinePCs = allPcs.Count(p => IsPcOnlineForMonitor(p, nowUtc));

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

        public async Task<List<LatencyDataPoint>> GetLatencyHistoryAsync(DateTime startUtc, DateTime endUtc, int? roomId = null)
        {
            var query = _context.NetworkMetrics
                .Where(nm => nm.Timestamp >= startUtc && nm.Timestamp <= endUtc);

            if (roomId.HasValue)
            {
                query = query.Where(nm => _context.PCs.Any(p => p.Id == nm.PCId && p.RoomId == roomId.Value));
            }

            return await query
                .GroupBy(nm => new { nm.Timestamp.Year, nm.Timestamp.Month, nm.Timestamp.Day, nm.Timestamp.Hour, nm.Timestamp.Minute })
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

        public async Task<List<BandwidthDataPoint>> GetBandwidthHistoryAsync(DateTime startUtc, DateTime endUtc, int? roomId = null)
        {
            var query = _context.NetworkMetrics
                .Where(nm => nm.Timestamp >= startUtc && nm.Timestamp <= endUtc);

            if (roomId.HasValue)
            {
                query = query.Where(nm => _context.PCs.Any(p => p.Id == nm.PCId && p.RoomId == roomId.Value));
            }

            return await query
                .GroupBy(nm => new { nm.Timestamp.Year, nm.Timestamp.Month, nm.Timestamp.Day, nm.Timestamp.Hour, nm.Timestamp.Minute })
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

        public async Task<List<PacketLossDataPoint>> GetPacketLossHistoryAsync(DateTime startUtc, DateTime endUtc, int? roomId = null)
        {
            var query = _context.NetworkMetrics
                .Where(nm => nm.Timestamp >= startUtc && nm.Timestamp <= endUtc);

            if (roomId.HasValue)
            {
                query = query.Where(nm => _context.PCs.Any(p => p.Id == nm.PCId && p.RoomId == roomId.Value));
            }

            return await query
                .GroupBy(nm => new { nm.Timestamp.Year, nm.Timestamp.Month, nm.Timestamp.Day, nm.Timestamp.Hour, nm.Timestamp.Minute })
                .Select(g => new PacketLossDataPoint
                {
                    Timestamp = g.Min(x => x.Timestamp),
                    Value = g.Average(x => x.PacketLoss ?? 0)
                })
                .OrderBy(d => d.Timestamp)
                .ToListAsync();
        }

        public async Task<byte[]> ExportNetworkAnalyticsCsvAsync(DateTime startUtc, DateTime endUtc, int? roomId = null)
        {
            var query = _context.NetworkMetrics
                .Include(n => n.PC)
                .ThenInclude(p => p.Room)
                .Where(n => n.Timestamp >= startUtc && n.Timestamp <= endUtc);

            if (roomId.HasValue)
            {
                query = query.Where(n => n.PC.RoomId == roomId.Value);
            }

            var rows = await query
                .OrderByDescending(n => n.Timestamp)
                .Take(5000)
                .ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("TimestampUtc,Room,PC,MacAddress,UploadMbps,DownloadMbps,LatencyMs,PacketLossPercent");

            foreach (var row in rows)
            {
                csv.AppendLine(string.Join(",",
                    row.Timestamp.ToString("O"),
                    EscapeCsv(row.PC?.Room?.RoomNumber ?? "Unassigned"),
                    EscapeCsv(row.PC?.Hostname ?? "Unknown"),
                    EscapeCsv(row.PC?.MacAddress ?? string.Empty),
                    (row.UploadSpeed ?? 0).ToString("F3"),
                    (row.DownloadSpeed ?? 0).ToString("F3"),
                    (row.Latency ?? 0).ToString("F2"),
                    (row.PacketLoss ?? 0).ToString("F2")));
            }

            return Encoding.UTF8.GetBytes(csv.ToString());
        }

        public async Task<byte[]> ExportHardwareAnalyticsCsvAsync(DateTime startUtc, DateTime endUtc, int? roomId = null)
        {
            var query = _context.HardwareMetrics
                .Include(h => h.PC)
                .ThenInclude(p => p.Room)
                .Where(h => h.Timestamp >= startUtc && h.Timestamp <= endUtc);

            if (roomId.HasValue)
            {
                query = query.Where(h => h.PC.RoomId == roomId.Value);
            }

            var rows = await query
                .OrderByDescending(h => h.Timestamp)
                .Take(5000)
                .ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("TimestampUtc,Room,PC,MacAddress,CpuUsagePercent,RamUsagePercent,DiskUsagePercent,CpuTempC,GpuUsagePercent,GpuTempC");

            foreach (var row in rows)
            {
                csv.AppendLine(string.Join(",",
                    row.Timestamp.ToString("O"),
                    EscapeCsv(row.PC?.Room?.RoomNumber ?? "Unassigned"),
                    EscapeCsv(row.PC?.Hostname ?? "Unknown"),
                    EscapeCsv(row.PC?.MacAddress ?? string.Empty),
                    (row.CpuUsage ?? 0).ToString("F2"),
                    (row.MemoryUsage ?? 0).ToString("F2"),
                    (row.DiskUsage ?? 0).ToString("F2"),
                    (row.CpuTemperature ?? 0).ToString("F2"),
                    (row.GpuUsage ?? 0).ToString("F2"),
                    (row.GpuTemperature ?? 0).ToString("F2")));
            }

            return Encoding.UTF8.GetBytes(csv.ToString());
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
            var nowUtc = DateTime.UtcNow;

            // Get all PCs with their room information
            var allPCs = await _context.PCs
                .Include(p => p.Room)
                .ToListAsync();

            // Get online PCs grouped by room number
            var onlinePCsByRoom = allPCs
                .Where(p => IsPcOnlineForMonitor(p, nowUtc))
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
            var nowUtc = DateTime.UtcNow;

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
                var isOnline = IsPcOnlineForMonitor(pc, nowUtc);

                var latestMetric = pc.HardwareMetrics
                    .OrderByDescending(m => m.Timestamp)
                    .FirstOrDefault();

                var latestUser = pc.UserLogs
                    .OrderByDescending(u => u.Timestamp)
                    .FirstOrDefault();

                var latestNetwork = pc.NetworkMetrics
                    .OrderByDescending(n => n.Timestamp)
                    .FirstOrDefault();

                var networkUsage = isOnline
                    ? (latestNetwork?.DownloadSpeed ?? 0) + (latestNetwork?.UploadSpeed ?? 0)
                    : 0;

                return new PCMonitorInfo
                {
                    Id = pc.Id,
                    Name = pc.Hostname ?? "Unknown",
                    IpAddress = pc.IpAddress ?? "N/A",
                    MacAddress = pc.MacAddress ?? "",
                    RoomName = pc.Room?.RoomNumber ?? "Unassigned",
                    OperatingSystem = pc.OperatingSystem ?? "Unknown",
                    Status = isOnline ? Models.PCStatus.Online.ToString() : Models.PCStatus.Offline.ToString(),
                    CpuUsage = isOnline ? latestMetric?.CpuUsage ?? 0 : 0,
                    CpuTemperature = isOnline ? latestMetric?.CpuTemperature : null,
                    GpuUsage = isOnline ? latestMetric?.GpuUsage : null,
                    GpuTemperature = isOnline ? latestMetric?.GpuTemperature : null,
                    RamUsage = isOnline ? latestMetric?.MemoryUsage ?? 0 : 0,
                    DiskUsage = isOnline ? latestMetric?.DiskUsage ?? 0 : 0,
                    NetworkUsage = networkUsage,
                    NetworkUploadMbps = isOnline ? latestNetwork?.UploadSpeed ?? 0 : 0,
                    NetworkDownloadMbps = isOnline ? latestNetwork?.DownloadSpeed ?? 0 : 0,
                    NetworkLatencyMs = isOnline ? latestNetwork?.Latency : null,
                    PacketLossPercent = isOnline ? latestNetwork?.PacketLoss : null,
                    LastMetricTimestamp = latestMetric?.Timestamp ?? latestNetwork?.Timestamp,
                    User = latestUser?.User?.Username ?? ""
                };
            }).ToList();
        }

        public async Task<PCStatusCounts> GetPCStatusCountsAsync(int? roomId = null)
        {
            var nowUtc = DateTime.UtcNow;

            var query = _context.PCs.AsQueryable();
            if (roomId.HasValue)
                query = query.Where(p => p.RoomId == roomId.Value);

            var pcs = await query.ToListAsync();
            var onlineCount = pcs.Count(pc => IsPcOnlineForMonitor(pc, nowUtc));
            var offlineCount = pcs.Count - onlineCount;

            return new PCStatusCounts
            {
                OnlineCount = onlineCount,
                OfflineCount = offlineCount
            };
        }

        public async Task<List<LiveAlertItem>> GetLiveAlertsAsync(int? roomId = null, int maxItems = 50)
        {
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
                var isOnline = IsPcOnlineForMonitor(pc, DateTime.UtcNow);

                if (!isOnline)
                {
                    continue;
                }

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

                if (latestHardware != null)
                {
                    AddThresholdAlert(alerts, pc.Id, pcName, roomName, "Hardware", "CPU", latestHardware.CpuUsage,
                        cpuHistory,
                        activePolicy?.CpuUsageWarningThreshold ?? 85,
                        activePolicy?.CpuUsageCriticalThreshold ?? 95,
                        activePolicy?.WarningSustainSeconds ?? 30,
                        activePolicy?.CriticalSustainSeconds ?? 20,
                        latestHardware.Timestamp, "%", "cpu-usage");

                    AddThresholdAlert(alerts, pc.Id, pcName, roomName, "Hardware", "RAM", latestHardware.MemoryUsage,
                        ramHistory,
                        activePolicy?.RamUsageWarningThreshold ?? 85,
                        activePolicy?.RamUsageCriticalThreshold ?? 95,
                        activePolicy?.WarningSustainSeconds ?? 30,
                        activePolicy?.CriticalSustainSeconds ?? 20,
                        latestHardware.Timestamp, "%", "ram-usage");

                    AddThresholdAlert(alerts, pc.Id, pcName, roomName, "Hardware", "Disk", latestHardware.DiskUsage,
                        diskHistory,
                        activePolicy?.DiskUsageWarningThreshold ?? 90,
                        activePolicy?.DiskUsageCriticalThreshold ?? 98,
                        activePolicy?.WarningSustainSeconds ?? 30,
                        activePolicy?.CriticalSustainSeconds ?? 20,
                        latestHardware.Timestamp, "%", "disk-usage");

                    AddThresholdAlert(alerts, pc.Id, pcName, roomName, "Thermal", "CPU temperature", latestHardware.CpuTemperature,
                        cpuTempHistory,
                        activePolicy?.CpuTemperatureWarningThreshold ?? 80,
                        activePolicy?.CpuTemperatureCriticalThreshold ?? 90,
                        activePolicy?.WarningSustainSeconds ?? 30,
                        activePolicy?.CriticalSustainSeconds ?? 20,
                        latestHardware.Timestamp, " °C", "cpu-temp");

                    AddThresholdAlert(alerts, pc.Id, pcName, roomName, "Thermal", "GPU temperature", latestHardware.GpuTemperature,
                        gpuTempHistory,
                        activePolicy?.GpuTemperatureWarningThreshold ?? 80,
                        activePolicy?.GpuTemperatureCriticalThreshold ?? 90,
                        activePolicy?.WarningSustainSeconds ?? 30,
                        activePolicy?.CriticalSustainSeconds ?? 20,
                        latestHardware.Timestamp, " °C", "gpu-temp");
                }

                if (latestNetwork != null)
                {
                    AddThresholdAlert(alerts, pc.Id, pcName, roomName, "Network", "Packet loss", latestNetwork.PacketLoss,
                        packetLossHistory,
                        activePolicy?.PacketLossWarningThreshold ?? 3,
                        activePolicy?.PacketLossCriticalThreshold ?? 10,
                        activePolicy?.WarningSustainSeconds ?? 30,
                        activePolicy?.CriticalSustainSeconds ?? 20,
                        latestNetwork.Timestamp, "%", "packet-loss");

                    AddThresholdAlert(alerts, pc.Id, pcName, roomName, "Network", "Latency", latestNetwork.Latency,
                        latencyHistory,
                        activePolicy?.LatencyWarningThreshold ?? 150,
                        activePolicy?.LatencyCriticalThreshold ?? 300,
                        activePolicy?.WarningSustainSeconds ?? 30,
                        activePolicy?.CriticalSustainSeconds ?? 20,
                        latestNetwork.Timestamp, " ms", "latency");
                }
            }

            return alerts
                .OrderByDescending(a => a.SeverityRank)
                .ThenByDescending(a => a.Timestamp)
                .Take(Math.Max(1, maxItems))
                .ToList();
        }

        public async Task<List<PersistedAlertItem>> GetAlertFeedAsync(int? roomId = null, int maxItems = 200, bool includeResolved = false)
        {
            var liveAlerts = await GetLiveAlertsAsync(roomId, 500);
            await SyncLiveAlertsAsync(liveAlerts, roomId);

            var query = _context.Alerts
                .AsNoTracking()
                .Include(a => a.PC)
                .ThenInclude(pc => pc.Room)
                .Where(a => roomId == null || a.PC.RoomId == roomId.Value);

            if (!includeResolved)
            {
                query = query.Where(a => !a.IsResolved);
            }

            var alerts = await query
                .OrderByDescending(a => a.CreatedAt)
                .Take(Math.Max(1, maxItems))
                .ToListAsync();

            // Sort by severity in-memory to avoid Npgsql enum/string cast issues
            alerts = alerts
                .OrderByDescending(a => (int)a.Severity)
                .ThenByDescending(a => a.CreatedAt)
                .ToList();

            return alerts.Select(a => new PersistedAlertItem
            {
                AlertId = a.Id,
                AlertKey = a.AlertKey,
                PCId = a.PCId,
                PCName = a.PC?.Hostname ?? $"PC-{a.PCId}",
                RoomName = a.PC?.Room?.RoomNumber ?? "Unassigned",
                Severity = a.Severity.ToString() switch
                {
                    "Low" => "Low",
                    "Medium" => "Medium",
                    "High" => "High",
                    "Critical" => "Critical",
                    _ => "Medium"
                },
                Type = a.Type.ToString(),
                Message = a.Message,
                CreatedAt = a.CreatedAt,
                IsAcknowledged = a.IsAcknowledged,
                AcknowledgedAt = a.AcknowledgedAt,
                IsResolved = a.IsResolved,
                ResolvedAt = a.ResolvedAt
            }).ToList();
        }

        public async Task<bool> AcknowledgeAlertAsync(int alertId, int userId)
        {
            var alert = await _context.Alerts.FirstOrDefaultAsync(a => a.Id == alertId);
            if (alert == null)
            {
                return false;
            }

            if (alert.IsAcknowledged)
            {
                return true;
            }

            alert.IsAcknowledged = true;
            alert.AcknowledgedAt = DateTime.UtcNow;
            if (userId > 0)
            {
                alert.UserId = userId;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ResolveAlertAsync(int alertId, int userId)
        {
            var alert = await _context.Alerts.FirstOrDefaultAsync(a => a.Id == alertId);
            if (alert == null)
            {
                return false;
            }

            if (alert.IsResolved)
            {
                return true;
            }

            alert.IsResolved = true;
            alert.ResolvedAt = DateTime.UtcNow;
            if (!alert.IsAcknowledged)
            {
                alert.IsAcknowledged = true;
                alert.AcknowledgedAt = DateTime.UtcNow;
            }

            if (userId > 0)
            {
                alert.UserId = userId;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<int> AcknowledgeAlertsAsync(IEnumerable<int> alertIds, int userId)
        {
            var ids = alertIds?.Distinct().ToList() ?? new List<int>();
            if (!ids.Any())
            {
                return 0;
            }

            var alerts = await _context.Alerts
                .Where(a => ids.Contains(a.Id) && !a.IsAcknowledged && !a.IsResolved)
                .ToListAsync();

            var nowUtc = DateTime.UtcNow;
            foreach (var alert in alerts)
            {
                alert.IsAcknowledged = true;
                alert.AcknowledgedAt = nowUtc;
                if (userId > 0)
                {
                    alert.UserId = userId;
                }
            }

            if (alerts.Count > 0)
            {
                await _context.SaveChangesAsync();
            }

            return alerts.Count;
        }

        public async Task<int> ResolveAlertsAsync(IEnumerable<int> alertIds, int userId)
        {
            var ids = alertIds?.Distinct().ToList() ?? new List<int>();
            if (!ids.Any())
            {
                return 0;
            }

            var alerts = await _context.Alerts
                .Where(a => ids.Contains(a.Id) && !a.IsResolved)
                .ToListAsync();

            var nowUtc = DateTime.UtcNow;
            foreach (var alert in alerts)
            {
                if (!alert.IsAcknowledged)
                {
                    alert.IsAcknowledged = true;
                    alert.AcknowledgedAt = nowUtc;
                }

                alert.IsResolved = true;
                alert.ResolvedAt = nowUtc;
                if (userId > 0)
                {
                    alert.UserId = userId;
                }
            }

            if (alerts.Count > 0)
            {
                await _context.SaveChangesAsync();
            }

            return alerts.Count;
        }

        public async Task<List<PcHealthTimelineEvent>> GetPcHealthTimelineAsync(int pcId, int hours = 24, int maxItems = 120)
        {
            if (pcId <= 0)
            {
                return new List<PcHealthTimelineEvent>();
            }

            var nowUtc = DateTime.UtcNow;
            var boundedHours = Math.Clamp(hours, 1, 168);
            var boundedMaxItems = Math.Clamp(maxItems, 20, 400);
            var sinceUtc = nowUtc.AddHours(-boundedHours);

            var pc = await _context.PCs
                .AsNoTracking()
                .Include(p => p.Room)
                .ThenInclude(r => r.Policies)
                .FirstOrDefaultAsync(p => p.Id == pcId);

            if (pc == null)
            {
                return new List<PcHealthTimelineEvent>();
            }

            var events = new List<PcHealthTimelineEvent>();
            var isOnlineNow = IsPcOnlineForMonitor(pc, nowUtc);

            events.Add(new PcHealthTimelineEvent
            {
                PCId = pc.Id,
                Timestamp = nowUtc,
                Severity = isOnlineNow ? "Info" : "Warning",
                Category = "Connectivity",
                Title = isOnlineNow ? "Currently online" : "Currently offline",
                Message = isOnlineNow
                    ? $"Heartbeat active ({Math.Max(0, (int)(nowUtc - pc.LastSeen).TotalSeconds)}s ago)."
                    : $"No heartbeat for {FormatDurationShort(nowUtc - pc.LastSeen)}."
            });

            events.Add(new PcHealthTimelineEvent
            {
                PCId = pc.Id,
                Timestamp = pc.LastSeen,
                Severity = "Info",
                Category = "Connectivity",
                Title = "Last heartbeat",
                Message = $"Last agent heartbeat at {pc.LastSeen:MMM dd, yyyy HH:mm:ss} UTC."
            });

            var alerts = await _context.Alerts
                .AsNoTracking()
                .Where(a => a.PCId == pcId)
                .Where(a => a.CreatedAt >= sinceUtc
                    || (a.AcknowledgedAt.HasValue && a.AcknowledgedAt.Value >= sinceUtc)
                    || (a.ResolvedAt.HasValue && a.ResolvedAt.Value >= sinceUtc))
                .OrderByDescending(a => a.CreatedAt)
                .Take(boundedMaxItems * 2)
                .ToListAsync();

            foreach (var alert in alerts)
            {
                if (alert.CreatedAt >= sinceUtc)
                {
                    events.Add(new PcHealthTimelineEvent
                    {
                        PCId = pc.Id,
                        Timestamp = alert.CreatedAt,
                        Severity = alert.Severity.ToString(),
                        Category = alert.Type.ToString(),
                        Title = "Alert raised",
                        Message = alert.Message
                    });
                }

                if (alert.AcknowledgedAt.HasValue && alert.AcknowledgedAt.Value >= sinceUtc)
                {
                    events.Add(new PcHealthTimelineEvent
                    {
                        PCId = pc.Id,
                        Timestamp = alert.AcknowledgedAt.Value,
                        Severity = "Info",
                        Category = "Alert",
                        Title = "Alert acknowledged",
                        Message = alert.Message
                    });
                }

                if (alert.ResolvedAt.HasValue && alert.ResolvedAt.Value >= sinceUtc)
                {
                    events.Add(new PcHealthTimelineEvent
                    {
                        PCId = pc.Id,
                        Timestamp = alert.ResolvedAt.Value,
                        Severity = "Info",
                        Category = "Alert",
                        Title = "Alert resolved",
                        Message = alert.Message
                    });
                }
            }

            var hardwarePoints = await _context.HardwareMetrics
                .AsNoTracking()
                .Where(m => m.PCId == pcId && m.Timestamp >= sinceUtc)
                .OrderBy(m => m.Timestamp)
                .Select(m => new
                {
                    m.Timestamp,
                    m.CpuUsage,
                    m.MemoryUsage,
                    m.CpuTemperature,
                    m.GpuTemperature
                })
                .ToListAsync();

            var networkPoints = await _context.NetworkMetrics
                .AsNoTracking()
                .Where(m => m.PCId == pcId && m.Timestamp >= sinceUtc)
                .OrderBy(m => m.Timestamp)
                .Select(m => new
                {
                    m.Timestamp,
                    m.Latency,
                    m.PacketLoss
                })
                .ToListAsync();

            var activePolicy = pc.Room?.Policies?.FirstOrDefault(p => p.IsActive);

            AddThresholdTransitionEvents(
                events,
                pc.Id,
                "Hardware",
                "CPU usage",
                "%",
                hardwarePoints.Select(p => (p.Timestamp, p.CpuUsage)),
                activePolicy?.CpuUsageWarningThreshold ?? 85,
                activePolicy?.CpuUsageCriticalThreshold ?? 95);

            AddThresholdTransitionEvents(
                events,
                pc.Id,
                "Hardware",
                "RAM usage",
                "%",
                hardwarePoints.Select(p => (p.Timestamp, p.MemoryUsage)),
                activePolicy?.RamUsageWarningThreshold ?? 85,
                activePolicy?.RamUsageCriticalThreshold ?? 95);

            AddThresholdTransitionEvents(
                events,
                pc.Id,
                "Thermal",
                "CPU temperature",
                "°C",
                hardwarePoints.Select(p => (p.Timestamp, p.CpuTemperature)),
                activePolicy?.CpuTemperatureWarningThreshold ?? 80,
                activePolicy?.CpuTemperatureCriticalThreshold ?? 90);

            AddThresholdTransitionEvents(
                events,
                pc.Id,
                "Thermal",
                "GPU temperature",
                "°C",
                hardwarePoints.Select(p => (p.Timestamp, p.GpuTemperature)),
                activePolicy?.GpuTemperatureWarningThreshold ?? 80,
                activePolicy?.GpuTemperatureCriticalThreshold ?? 90);

            AddThresholdTransitionEvents(
                events,
                pc.Id,
                "Network",
                "Latency",
                "ms",
                networkPoints.Select(p => (p.Timestamp, p.Latency)),
                activePolicy?.LatencyWarningThreshold ?? 150,
                activePolicy?.LatencyCriticalThreshold ?? 300);

            AddThresholdTransitionEvents(
                events,
                pc.Id,
                "Network",
                "Packet loss",
                "%",
                networkPoints.Select(p => (p.Timestamp, p.PacketLoss)),
                activePolicy?.PacketLossWarningThreshold ?? 3,
                activePolicy?.PacketLossCriticalThreshold ?? 10);

            var telemetryTimestamps = hardwarePoints
                .Select(p => p.Timestamp)
                .Concat(networkPoints.Select(p => p.Timestamp))
                .Distinct()
                .OrderBy(ts => ts)
                .ToList();

            var telemetryGapThreshold = MonitorHeartbeatGrace.Add(TimeSpan.FromSeconds(30));
            for (var i = 1; i < telemetryTimestamps.Count; i++)
            {
                var previous = telemetryTimestamps[i - 1];
                var current = telemetryTimestamps[i];
                var gap = current - previous;

                if (gap < telemetryGapThreshold)
                {
                    continue;
                }

                var offlineAt = previous.Add(MonitorHeartbeatGrace);
                if (offlineAt >= sinceUtc && offlineAt <= nowUtc)
                {
                    events.Add(new PcHealthTimelineEvent
                    {
                        PCId = pc.Id,
                        Timestamp = offlineAt,
                        Severity = "Warning",
                        Category = "Connectivity",
                        Title = "Telemetry gap detected",
                        Message = $"No heartbeat or metrics for {FormatDurationShort(gap)}."
                    });
                }

                events.Add(new PcHealthTimelineEvent
                {
                    PCId = pc.Id,
                    Timestamp = current,
                    Severity = "Info",
                    Category = "Connectivity",
                    Title = "Telemetry resumed",
                    Message = "Heartbeat and metric flow resumed."
                });
            }

            return events
                .Where(e => e.Timestamp >= sinceUtc)
                .OrderByDescending(e => e.Timestamp)
                .Take(boundedMaxItems)
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
            string unit,
            string metricKey)
        {
            if (!value.HasValue)
            {
                return;
            }

            if (value.Value >= criticalThreshold && IsSustained(metricHistory, criticalThreshold, criticalSustainSeconds, timestamp))
            {
                alerts.Add(CreateAlert(pcId, pcName, roomName, "Critical", type, $"{metricName} is critical ({value.Value:F1}{unit}) for {criticalSustainSeconds}s", timestamp, metricKey));
                return;
            }

            if (value.Value >= warningThreshold && IsSustained(metricHistory, warningThreshold, warningSustainSeconds, timestamp))
            {
                alerts.Add(CreateAlert(pcId, pcName, roomName, "High", type, $"{metricName} is high ({value.Value:F1}{unit}) for {warningSustainSeconds}s", timestamp, metricKey));
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
            DateTime timestamp,
            string metricKey)
        {
            return new LiveAlertItem
            {
                AlertKey = $"pc:{pcId}|type:{type.ToLowerInvariant()}|metric:{metricKey}|severity:{severity.ToLowerInvariant()}",
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

        private async Task SyncLiveAlertsAsync(List<LiveAlertItem> liveAlerts, int? roomId)
        {
            var nowUtc = DateTime.UtcNow;
            var pcIds = liveAlerts.Select(a => a.PCId).Distinct().ToList();

            if (!pcIds.Any() && roomId.HasValue)
            {
                pcIds = await _context.PCs
                    .Where(pc => pc.RoomId == roomId.Value)
                    .Select(pc => pc.Id)
                    .ToListAsync();
            }

            if (!pcIds.Any())
            {
                return;
            }

            var existingOpenAlerts = await _context.Alerts
                .Where(a => pcIds.Contains(a.PCId) && !a.IsResolved)
                .ToListAsync();

            var liveKeys = new HashSet<string>(liveAlerts.Select(a => a.AlertKey), StringComparer.OrdinalIgnoreCase);
            var defaultUserId = await GetDefaultAlertUserIdAsync();

            foreach (var live in liveAlerts)
            {
                var existing = existingOpenAlerts.FirstOrDefault(a =>
                    a.PCId == live.PCId &&
                    a.AlertKey == live.AlertKey);

                if (existing != null)
                {
                    existing.Message = live.Message;
                    existing.CreatedAt = live.Timestamp;
                    existing.Severity = ParseSeverity(live.Severity);
                    existing.Type = ParseType(live.Type);
                    continue;
                }

                _context.Alerts.Add(new Alert
                {
                    PCId = live.PCId,
                    UserId = defaultUserId,
                    AlertKey = live.AlertKey,
                    Title = BuildAlertTitle(live),
                    Message = live.Message,
                    Severity = ParseSeverity(live.Severity),
                    Type = ParseType(live.Type),
                    IsAcknowledged = false,
                    IsResolved = false,
                    CreatedAt = live.Timestamp,
                    AcknowledgedAt = null,
                    ResolvedAt = null
                });
            }

            foreach (var openAlert in existingOpenAlerts)
            {
                if (!liveKeys.Contains(openAlert.AlertKey))
                {
                    openAlert.IsResolved = true;
                    openAlert.ResolvedAt = nowUtc;
                }
            }

            await _context.SaveChangesAsync();
        }

        private async Task<int> GetDefaultAlertUserIdAsync()
        {
            var sysAdminId = await _context.Users
                .Where(u => u.IsActive && u.Role == UserRole.SystemAdministrator)
                .Select(u => (int?)u.Id)
                .FirstOrDefaultAsync();

            if (sysAdminId.HasValue)
            {
                return sysAdminId.Value;
            }

            var firstActiveUserId = await _context.Users
                .Where(u => u.IsActive)
                .Select(u => (int?)u.Id)
                .FirstOrDefaultAsync();

            return firstActiveUserId ?? 1;
        }

        private static AlertSeverity ParseSeverity(string severity)
        {
            return severity.ToLowerInvariant() switch
            {
                "critical" => AlertSeverity.Critical,
                "high" => AlertSeverity.High,
                "medium" => AlertSeverity.Medium,
                "low" => AlertSeverity.Low,
                _ => AlertSeverity.Medium
            };
        }

        private static AlertType ParseType(string type)
        {
            return type.ToLowerInvariant() switch
            {
                "hardware" => AlertType.Hardware,
                "network" => AlertType.Network,
                "software" => AlertType.Software,
                "security" => AlertType.Security,
                "thermal" => AlertType.Hardware,
                _ => AlertType.System
            };
        }

        private static string BuildAlertTitle(LiveAlertItem alert)
        {
            return $"{alert.Type} {alert.Severity} alert";
        }

        private static void AddThresholdTransitionEvents(
            List<PcHealthTimelineEvent> events,
            int pcId,
            string category,
            string metricName,
            string unit,
            IEnumerable<(DateTime Timestamp, double? Value)> points,
            double warningThreshold,
            double criticalThreshold)
        {
            var sortedPoints = points
                .Where(p => p.Value.HasValue)
                .OrderBy(p => p.Timestamp)
                .ToList();

            if (sortedPoints.Count == 0)
            {
                return;
            }

            var previousState = 0;

            foreach (var point in sortedPoints)
            {
                var value = point.Value!.Value;
                var currentState = GetThresholdState(value, warningThreshold, criticalThreshold);

                if (currentState == previousState)
                {
                    continue;
                }

                if (currentState == 1)
                {
                    events.Add(new PcHealthTimelineEvent
                    {
                        PCId = pcId,
                        Timestamp = point.Timestamp,
                        Severity = "High",
                        Category = category,
                        Title = $"{metricName} warning",
                        Message = $"{metricName} reached {value:F1}{unit} (warning threshold {warningThreshold:F1}{unit})."
                    });
                }
                else if (currentState == 2)
                {
                    events.Add(new PcHealthTimelineEvent
                    {
                        PCId = pcId,
                        Timestamp = point.Timestamp,
                        Severity = "Critical",
                        Category = category,
                        Title = $"{metricName} critical",
                        Message = $"{metricName} reached {value:F1}{unit} (critical threshold {criticalThreshold:F1}{unit})."
                    });
                }
                else if (previousState > 0)
                {
                    events.Add(new PcHealthTimelineEvent
                    {
                        PCId = pcId,
                        Timestamp = point.Timestamp,
                        Severity = "Info",
                        Category = category,
                        Title = $"{metricName} recovered",
                        Message = $"{metricName} returned to normal at {value:F1}{unit}."
                    });
                }

                previousState = currentState;
            }
        }

        private static int GetThresholdState(double value, double warningThreshold, double criticalThreshold)
        {
            if (value >= criticalThreshold)
            {
                return 2;
            }

            if (value >= warningThreshold)
            {
                return 1;
            }

            return 0;
        }

        private static string FormatDurationShort(TimeSpan value)
        {
            var duration = value.Duration();
            if (duration.TotalHours >= 1)
            {
                return $"{(int)duration.TotalHours}h {duration.Minutes}m";
            }

            if (duration.TotalMinutes >= 1)
            {
                return $"{(int)duration.TotalMinutes}m {duration.Seconds}s";
            }

            return $"{Math.Max(0, (int)duration.TotalSeconds)}s";
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }

        private static bool IsPcOnlineForMonitor(Models.PC pc, DateTime nowUtc)
        {
            return pc.LastSeen >= nowUtc.Subtract(MonitorHeartbeatGrace);
        }
    }
}
