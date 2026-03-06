using IRIS.Core.Data;
using IRIS.Core.DTOs;
using IRIS.Core.Models;
using IRIS.Core.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace IRIS.Core.Services
{
    public class DeploymentDataService : IDeploymentDataService
    {
        private readonly IRISDbContext _context;

        public DeploymentDataService(IRISDbContext context)
        {
            _context = context;
        }

        public async Task<List<DeploymentPCDto>> GetRegisteredPCsAsync(int? roomId = null)
        {
            var query = _context.PCs
                .AsNoTracking()
                .Include(p => p.Room)
                .AsQueryable();

            if (roomId.HasValue && roomId.Value > 0)
            {
                query = query.Where(p => p.RoomId == roomId.Value);
            }

            return await query
                .OrderBy(p => p.Room.RoomNumber)
                .ThenBy(p => p.Hostname)
                .Select(p => new DeploymentPCDto(
                    p.Id,
                    p.Hostname,
                    p.IpAddress,
                    p.Status.ToString(),
                    p.RoomId,
                    p.Room.RoomNumber,
                    p.LastSeen))
                .ToListAsync();
        }

        public async Task LogDeploymentResultAsync(DeploymentLogCreateDto dto)
        {
            var entity = new DeploymentLog
            {
                PCId = dto.PCId,
                PCName = dto.PCName,
                IPAddress = dto.IPAddress,
                FileName = dto.FileName,
                Status = dto.Status,
                Details = dto.Details,
                Timestamp = dto.Timestamp ?? DateTime.UtcNow
            };

            _context.Set<DeploymentLog>().Add(entity);
            await _context.SaveChangesAsync();
        }

        public async Task<List<DeploymentLogDto>> GetRecentDeploymentLogsAsync(int take = 100)
        {
            take = Math.Clamp(take, 1, 1000);

            return await _context.Set<DeploymentLog>()
                .AsNoTracking()
                .OrderByDescending(x => x.Timestamp)
                .Take(take)
                .Select(x => new DeploymentLogDto(
                    x.Id,
                    x.PCId,
                    x.PCName,
                    x.IPAddress,
                    x.FileName,
                    x.Status,
                    x.Details,
                    x.Timestamp))
                .ToListAsync();
        }
    }
}
