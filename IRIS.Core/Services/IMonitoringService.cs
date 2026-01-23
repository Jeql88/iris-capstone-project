using IRIS.Core.DTOs;
using IRIS.Core.Services.ServiceModels;

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
        Task<PCHardwareConfigDto?> GetPCHardwareConfigAsync(int pcId);
    }
}
