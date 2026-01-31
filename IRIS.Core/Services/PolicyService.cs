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

        public async Task<bool> ApplyPolicyToRoomAsync(int policyId, int roomId)
        {
            var policy = await _context.Policies.FindAsync(policyId);
            if (policy == null) return false;

            // Create a copy of the policy for the target room
            var newPolicy = new Policy
            {
                Name = policy.Name,
                Description = policy.Description,
                RoomId = roomId,
                ResetWallpaperOnStartup = policy.ResetWallpaperOnStartup,
                AutoShutdownIdleMinutes = policy.AutoShutdownIdleMinutes,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Policies.Add(newPolicy);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}