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
        public double? CpuTemperature { get; set; }
        public double? GpuUsage { get; set; }
        public double? GpuTemperature { get; set; }
        public double RamUsage { get; set; }
        public double DiskUsage { get; set; }
        public double NetworkUsage { get; set; }
        public double NetworkUploadMbps { get; set; }
        public double NetworkDownloadMbps { get; set; }
        public double? NetworkLatencyMs { get; set; }
        public double? PacketLossPercent { get; set; }
        public DateTime? LastMetricTimestamp { get; set; }
        public string User { get; set; } = string.Empty;
    }

    public class PCStatusCounts
    {
        public int OnlineCount { get; set; }
        public int OfflineCount { get; set; }
    }

    public class LiveAlertItem
    {
        public string AlertKey { get; set; } = string.Empty;
        public int PCId { get; set; }
        public string PCName { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public string Severity { get; set; } = "Medium";
        public string Type { get; set; } = "System";
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public int SeverityRank { get; set; }
        public bool IsAcknowledged { get; set; }
        public bool IsResolved { get; set; }
    }

    public class PersistedAlertItem
    {
        public int AlertId { get; set; }
        public string AlertKey { get; set; } = string.Empty;
        public int PCId { get; set; }
        public string PCName { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public string Severity { get; set; } = "Medium";
        public string Type { get; set; } = "System";
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsAcknowledged { get; set; }
        public DateTime? AcknowledgedAt { get; set; }
        public bool IsResolved { get; set; }
        public DateTime? ResolvedAt { get; set; }
    }

    public class PcHealthTimelineEvent
    {
        public int PCId { get; set; }
        public DateTime Timestamp { get; set; }
        public string Severity { get; set; } = "Info";
        public string Category { get; set; } = "System";
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
