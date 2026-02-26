using IRIS.Core.Models;

namespace IRIS.Core.Services.Contracts
{
    public interface IPolicyService
    {
        Task<IEnumerable<Policy>> GetPoliciesByRoomIdAsync(int roomId);
        Task<IEnumerable<Policy>> GetActivePoliciesByRoomIdAsync(int roomId);
        Task<Policy> CreatePolicyAsync(Policy policy);
        Task<Policy> UpdatePolicyAsync(Policy policy);
        Task DeletePolicyAsync(int policyId);
        Task DeletePoliciesByRoomIdAsync(int roomId);
        Task<Policy> CreateOrUpdatePolicyAsync(
            int roomId,
            bool resetWallpaperOnStartup,
            int? autoShutdownIdleMinutes,
            string? wallpaperPath = null,
            double? cpuUsageWarningThreshold = null,
            double? cpuUsageCriticalThreshold = null,
            double? ramUsageWarningThreshold = null,
            double? ramUsageCriticalThreshold = null,
            double? diskUsageWarningThreshold = null,
            double? diskUsageCriticalThreshold = null,
            double? cpuTemperatureWarningThreshold = null,
            double? cpuTemperatureCriticalThreshold = null,
            double? gpuTemperatureWarningThreshold = null,
            double? gpuTemperatureCriticalThreshold = null,
            double? latencyWarningThreshold = null,
            double? latencyCriticalThreshold = null,
            double? packetLossWarningThreshold = null,
            double? packetLossCriticalThreshold = null,
            int? warningSustainSeconds = null,
            int? criticalSustainSeconds = null);
        Task<bool> UpdateWallpaperPolicyAsync(int roomId, string wallpaperPath);
    }
}
