using IRIS.Core.DTOs;
using IRIS.Core.Services.ServiceModels;

namespace IRIS.Core.Services.Contracts
{
    public interface IMonitoringService
    {
        Task<DashboardSummary> GetDashboardSummaryAsync(int? roomId = null);
        Task<DashboardMetrics> GetDashboardMetricsAsync(int? roomId = null);
        Task<List<LatencyDataPoint>> GetLatencyHistoryAsync(int hours = 24);
        Task<List<BandwidthDataPoint>> GetBandwidthHistoryAsync(int hours = 24);
        Task<List<PacketLossDataPoint>> GetPacketLossHistoryAsync(int hours = 24);
        Task<List<HeavyApplication>> GetHeavyApplicationsAsync(int? roomId = null);
        Task<Dictionary<string, int>> GetActiveLabPCsAsync();
        Task<List<RoomDto>> GetRoomsAsync();
        Task<List<PCMonitorInfo>> GetPCsForMonitorAsync(int? roomId = null);
        Task<PCStatusCounts> GetPCStatusCountsAsync(int? roomId = null);
        Task<List<LiveAlertItem>> GetLiveAlertsAsync(int? roomId = null, int maxItems = 50);
        Task<PCHardwareConfigDto?> GetPCHardwareConfigAsync(int pcId);
    }
}
