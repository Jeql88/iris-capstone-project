// Service Response Models
// They represent aggregated/calculated data returned by services
// DTOs map 1:1 to database tables, while these models combine multiple entities or calculations

namespace IRIS.Core.Services.ServiceModels
{
    public class DashboardMetrics
    {
        public double AverageLatency { get; set; }
        public double CurrentBandwidth { get; set; }
        public double PeakBandwidth { get; set; }
        public int TotalPCs { get; set; }
        public int OnlinePCs { get; set; }
    }

    public class DashboardSummary
    {
        public double AverageLatency { get; set; }
        public double AveragePacketLoss { get; set; }
        public double CurrentBandwidth { get; set; }
        public double PeakBandwidth { get; set; }
        public int TotalPCs { get; set; }
        public int OnlinePCs { get; set; }
        public int OfflinePCs { get; set; }
        public int WarningPCs { get; set; }
        public Dictionary<string, int> LabStatuses { get; set; } = new();
        public List<HeavyApplication> HeavyApplications { get; set; } = new();
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
        public string MacAddress { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public string OperatingSystem { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public double CpuUsage { get; set; }
        public double RamUsage { get; set; }
        public double DiskUsage { get; set; }
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
