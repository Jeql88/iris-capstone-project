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
        Task<Policy> CreateOrUpdatePolicyAsync(int roomId, bool resetWallpaperOnStartup, int? autoShutdownIdleMinutes, string? wallpaperPath = null);
        Task<bool> UpdateWallpaperPolicyAsync(int roomId, string wallpaperPath);
    }
}
