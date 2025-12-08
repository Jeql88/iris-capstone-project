namespace IRIS.Core.Services
{
    public interface IMonitoringService
    {
        Task<DashboardMetrics> GetDashboardMetricsAsync(int? roomId = null);
        Task<List<LatencyDataPoint>> GetLatencyHistoryAsync(int hours = 24);
        Task<List<BandwidthDataPoint>> GetBandwidthHistoryAsync(int hours = 24);
        Task<List<PacketLossDataPoint>> GetPacketLossHistoryAsync(int hours = 24);
        Task<List<HeavyApplication>> GetHeavyApplicationsAsync(int? roomId = null);
        Task<Dictionary<string, int>> GetActiveLabPCsAsync();
        Task<List<PCMonitorInfo>> GetPCsForMonitorAsync(int? roomId = null);
        Task<PCStatusCounts> GetPCStatusCountsAsync(int? roomId = null);
    }

    public class DashboardMetrics
    {
        public double AverageLatency { get; set; }
        public double CurrentBandwidth { get; set; }
        public double PeakBandwidth { get; set; }
        public int TotalPCs { get; set; }
        public int OnlinePCs { get; set; }
    }

    public class LatencyDataPoint
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    public class BandwidthDataPoint
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    public class PacketLossDataPoint
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    public class HeavyApplication
    {
        public string Name { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public int InstanceCount { get; set; }
        public double AverageRamUsage { get; set; }
    }

    public class PCMonitorInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string OperatingSystem { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public double CpuUsage { get; set; }
        public double RamUsage { get; set; }
        public double NetworkUsage { get; set; }
        public string User { get; set; } = string.Empty;
    }

    public class PCStatusCounts
    {
        public int OnlineCount { get; set; }
        public int OfflineCount { get; set; }
        public int WarningCount { get; set; }
    }
}
