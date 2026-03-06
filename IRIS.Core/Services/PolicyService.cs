using IRIS.Core.Data;
using IRIS.Core.Models;
using IRIS.Core.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace IRIS.Core.Services
{
    public class PolicyService : IPolicyService
    {
        private readonly IRISDbContext _context;
        private readonly IAuthenticationService _authService;

        public PolicyService(IRISDbContext context, IAuthenticationService authService)
        {
            _context = context;
            _authService = authService;
        }

        public async Task<IEnumerable<Policy>> GetPoliciesByRoomIdAsync(int roomId)
        {
            return await _context.Policies
                .Where(p => p.RoomId == roomId)
                .ToListAsync();
        }

        public async Task<IEnumerable<Policy>> GetActivePoliciesByRoomIdAsync(int roomId)
        {
            return await _context.Policies
                .Where(p => p.RoomId == roomId && p.IsActive)
                .ToListAsync();
        }

        public async Task<Policy> CreatePolicyAsync(Policy policy)
        {
            policy.CreatedAt = DateTime.UtcNow;
            _context.Policies.Add(policy);
            await _context.SaveChangesAsync();
            return policy;
        }

        public async Task<Policy> UpdatePolicyAsync(Policy policy)
        {
            policy.UpdatedAt = DateTime.UtcNow;
            _context.Policies.Update(policy);
            await _context.SaveChangesAsync();
            return policy;
        }

        public async Task DeletePolicyAsync(int policyId)
        {
            var policy = await _context.Policies.FindAsync(policyId);
            if (policy != null)
            {
                _context.Policies.Remove(policy);
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeletePoliciesByRoomIdAsync(int roomId)
        {
            var policies = await _context.Policies
                .Where(p => p.RoomId == roomId)
                .ToListAsync();
            
            _context.Policies.RemoveRange(policies);
            await _context.SaveChangesAsync();
        }

        public async Task<Policy> CreateOrUpdatePolicyAsync(
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
            int? criticalSustainSeconds = null)
        {
            var existingPolicy = await _context.Policies
                .FirstOrDefaultAsync(p => p.RoomId == roomId);

            var roomNumber = await _context.Rooms
                .Where(r => r.Id == roomId)
                .Select(r => r.RoomNumber)
                .FirstOrDefaultAsync();

            var roomLabel = roomNumber ?? roomId.ToString();

            if (existingPolicy != null)
            {
                // Update existing policy
                existingPolicy.ResetWallpaperOnStartup = resetWallpaperOnStartup;
                existingPolicy.AutoShutdownIdleMinutes = autoShutdownIdleMinutes;
                
                // Update wallpaper path if provided
                if (!string.IsNullOrEmpty(wallpaperPath))
                {
                    existingPolicy.WallpaperPath = wallpaperPath;
                }

                existingPolicy.CpuUsageWarningThreshold = cpuUsageWarningThreshold ?? existingPolicy.CpuUsageWarningThreshold;
                existingPolicy.CpuUsageCriticalThreshold = cpuUsageCriticalThreshold ?? existingPolicy.CpuUsageCriticalThreshold;
                existingPolicy.RamUsageWarningThreshold = ramUsageWarningThreshold ?? existingPolicy.RamUsageWarningThreshold;
                existingPolicy.RamUsageCriticalThreshold = ramUsageCriticalThreshold ?? existingPolicy.RamUsageCriticalThreshold;
                existingPolicy.DiskUsageWarningThreshold = diskUsageWarningThreshold ?? existingPolicy.DiskUsageWarningThreshold;
                existingPolicy.DiskUsageCriticalThreshold = diskUsageCriticalThreshold ?? existingPolicy.DiskUsageCriticalThreshold;
                existingPolicy.CpuTemperatureWarningThreshold = cpuTemperatureWarningThreshold ?? existingPolicy.CpuTemperatureWarningThreshold;
                existingPolicy.CpuTemperatureCriticalThreshold = cpuTemperatureCriticalThreshold ?? existingPolicy.CpuTemperatureCriticalThreshold;
                existingPolicy.GpuTemperatureWarningThreshold = gpuTemperatureWarningThreshold ?? existingPolicy.GpuTemperatureWarningThreshold;
                existingPolicy.GpuTemperatureCriticalThreshold = gpuTemperatureCriticalThreshold ?? existingPolicy.GpuTemperatureCriticalThreshold;
                existingPolicy.LatencyWarningThreshold = latencyWarningThreshold ?? existingPolicy.LatencyWarningThreshold;
                existingPolicy.LatencyCriticalThreshold = latencyCriticalThreshold ?? existingPolicy.LatencyCriticalThreshold;
                existingPolicy.PacketLossWarningThreshold = packetLossWarningThreshold ?? existingPolicy.PacketLossWarningThreshold;
                existingPolicy.PacketLossCriticalThreshold = packetLossCriticalThreshold ?? existingPolicy.PacketLossCriticalThreshold;
                existingPolicy.WarningSustainSeconds = warningSustainSeconds ?? existingPolicy.WarningSustainSeconds;
                existingPolicy.CriticalSustainSeconds = criticalSustainSeconds ?? existingPolicy.CriticalSustainSeconds;
                
                existingPolicy.IsActive = true; // Always keep policy active once created
                existingPolicy.UpdatedAt = DateTime.UtcNow;
                
                _context.Policies.Update(existingPolicy);
                await _context.SaveChangesAsync();

                await _authService.LogUserActionAsync(
                    "Policy Enforcement Updated",
                    $"Updated policy enforcement for lab {roomLabel} (RoomId: {roomId})");

                return existingPolicy;
            }
            else
            {
                // Create new policy
                var newPolicy = new Policy
                {
                    Name = $"Policy for Room {roomId}",
                    Description = "Auto-generated policy from Policy Enforcement UI",
                    RoomId = roomId,
                    ResetWallpaperOnStartup = resetWallpaperOnStartup,
                    AutoShutdownIdleMinutes = autoShutdownIdleMinutes,
                    WallpaperPath = wallpaperPath,
                    CpuUsageWarningThreshold = cpuUsageWarningThreshold ?? 85,
                    CpuUsageCriticalThreshold = cpuUsageCriticalThreshold ?? 95,
                    RamUsageWarningThreshold = ramUsageWarningThreshold ?? 85,
                    RamUsageCriticalThreshold = ramUsageCriticalThreshold ?? 95,
                    DiskUsageWarningThreshold = diskUsageWarningThreshold ?? 90,
                    DiskUsageCriticalThreshold = diskUsageCriticalThreshold ?? 98,
                    CpuTemperatureWarningThreshold = cpuTemperatureWarningThreshold ?? 80,
                    CpuTemperatureCriticalThreshold = cpuTemperatureCriticalThreshold ?? 90,
                    GpuTemperatureWarningThreshold = gpuTemperatureWarningThreshold ?? 80,
                    GpuTemperatureCriticalThreshold = gpuTemperatureCriticalThreshold ?? 90,
                    LatencyWarningThreshold = latencyWarningThreshold ?? 150,
                    LatencyCriticalThreshold = latencyCriticalThreshold ?? 300,
                    PacketLossWarningThreshold = packetLossWarningThreshold ?? 3,
                    PacketLossCriticalThreshold = packetLossCriticalThreshold ?? 10,
                    WarningSustainSeconds = warningSustainSeconds ?? 30,
                    CriticalSustainSeconds = criticalSustainSeconds ?? 20,
                    IsActive = true, // Always active once created
                    CreatedAt = DateTime.UtcNow
                };
                
                _context.Policies.Add(newPolicy);
                await _context.SaveChangesAsync();

                await _authService.LogUserActionAsync(
                    "Policy Enforcement Updated",
                    $"Created policy enforcement for lab {roomLabel} (RoomId: {roomId})");

                return newPolicy;
            }
        }

        public async Task<bool> UpdateWallpaperPolicyAsync(int roomId, string wallpaperPath)
        {
            var policy = await _context.Policies
                .FirstOrDefaultAsync(p => p.RoomId == roomId);

            if (policy != null)
            {
                policy.WallpaperPath = wallpaperPath;
                policy.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                var roomNumber = await _context.Rooms
                    .Where(r => r.Id == roomId)
                    .Select(r => r.RoomNumber)
                    .FirstOrDefaultAsync();

                await _authService.LogUserActionAsync(
                    "Policy Enforcement Updated",
                    $"Updated wallpaper policy for lab {roomNumber ?? roomId.ToString()} (RoomId: {roomId})");

                return true;
            }

            return false;
        }
    }
}