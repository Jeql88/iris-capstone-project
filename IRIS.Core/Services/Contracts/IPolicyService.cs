using IRIS.Core.Models;

namespace IRIS.Core.Services.Contracts
{
    public interface IPolicyService
    {
        Task<Policy> CreateOrUpdatePolicyAsync(
            int roomId,
            bool resetWallpaperOnStartup,
            int? autoShutdownIdleMinutes,
            byte[]? wallpaperData = null,
            string? wallpaperFileName = null,
            bool clearWallpaper = false,
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
    }
}
