using IRIS.Core.Data;
using IRIS.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace IRIS.Core.Services
{
    public class PolicyService : IPolicyService
    {
        private readonly IRISDbContext _context;

        public PolicyService(IRISDbContext context)
        {
            _context = context;
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

        public async Task<Policy> CreateOrUpdatePolicyAsync(int roomId, bool resetWallpaperOnStartup, int? autoShutdownIdleMinutes, string? wallpaperPath = null)
        {
            var existingPolicy = await _context.Policies
                .FirstOrDefaultAsync(p => p.RoomId == roomId);

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
                
                existingPolicy.IsActive = true; // Always keep policy active once created
                existingPolicy.UpdatedAt = DateTime.UtcNow;
                
                _context.Policies.Update(existingPolicy);
                await _context.SaveChangesAsync();
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
                    IsActive = true, // Always active once created
                    CreatedAt = DateTime.UtcNow
                };
                
                _context.Policies.Add(newPolicy);
                await _context.SaveChangesAsync();
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
                return true;
            }

            return false;
        }
    }
}