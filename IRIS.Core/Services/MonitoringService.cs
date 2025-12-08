using IRIS.Core.Data;
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
            return await _context.PCs
                .Where(p => p.Status == Models.PCStatus.Online)
                .GroupBy(p => p.Room.RoomNumber)
                .Select(g => new { RoomName = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.RoomName, x => x.Count);
        }

        public async Task<List<PCMonitorInfo>> GetPCsForMonitorAsync(int? roomId = null)
        {
            var query = _context.PCs
                .Include(p => p.HardwareMetrics)
                .Include(p => p.UserLogs)
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

                return new PCMonitorInfo
                {
                    Id = pc.Id,
                    Name = pc.Hostname ?? "Unknown",
                    IpAddress = pc.IpAddress ?? "N/A",
                    OperatingSystem = pc.OperatingSystem ?? "Unknown",
                    Status = pc.Status.ToString(),
                    CpuUsage = latestMetric?.CpuUsage ?? 0,
                    RamUsage = latestMetric?.MemoryUsage ?? 0,
                    NetworkUsage = 0, // Will be calculated from NetworkMetrics
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
                //WarningCount = await query.CountAsync(p => p.Status == Models.PCStatus.Warning)
            };
        }
    }
}
