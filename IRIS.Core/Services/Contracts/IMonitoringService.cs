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
        Task<List<LatencyDataPoint>> GetLatencyHistoryAsync(DateTime startUtc, DateTime endUtc, int? roomId = null);
        Task<List<BandwidthDataPoint>> GetBandwidthHistoryAsync(DateTime startUtc, DateTime endUtc, int? roomId = null);
        Task<List<PacketLossDataPoint>> GetPacketLossHistoryAsync(DateTime startUtc, DateTime endUtc, int? roomId = null);
        Task<List<HeavyApplication>> GetHeavyApplicationsAsync(int? roomId = null);
        Task<Dictionary<string, int>> GetActiveLabPCsAsync();
        Task<List<RoomDto>> GetRoomsAsync();
        Task<List<PCMonitorInfo>> GetPCsForMonitorAsync(int? roomId = null);
        Task<PCStatusCounts> GetPCStatusCountsAsync(int? roomId = null);
        Task<List<LiveAlertItem>> GetLiveAlertsAsync(int? roomId = null, int maxItems = 50);
        Task<List<PersistedAlertItem>> GetAlertFeedAsync(int? roomId = null, int maxItems = 200, bool includeResolved = false);
        Task<bool> AcknowledgeAlertAsync(int alertId, int userId);
        Task<bool> ResolveAlertAsync(int alertId, int userId);
        Task<int> AcknowledgeAlertsAsync(IEnumerable<int> alertIds, int userId);
        Task<int> ResolveAlertsAsync(IEnumerable<int> alertIds, int userId);
        Task<List<PcHealthTimelineEvent>> GetPcHealthTimelineAsync(int pcId, int hours = 24, int maxItems = 120);
        Task<byte[]> ExportNetworkAnalyticsCsvAsync(DateTime startUtc, DateTime endUtc, int? roomId = null);
        Task<byte[]> ExportHardwareAnalyticsCsvAsync(DateTime startUtc, DateTime endUtc, int? roomId = null);
        Task<PCHardwareConfigDto?> GetPCHardwareConfigAsync(int pcId);
    }
}
