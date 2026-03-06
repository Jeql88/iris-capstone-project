using IRIS.Core.Data;
using IRIS.Core.DTOs;
using IRIS.Core.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace IRIS.Core.Services
{
    public class PCAdminService : IPCAdminService
    {
        private readonly IRISDbContext _context;
        private readonly IAuthenticationService _authService;

        public PCAdminService(IRISDbContext context, IAuthenticationService authService)
        {
            _context = context;
            _authService = authService;
        }

        public async Task<List<PCDto>> GetUnassignedPCsAsync(string defaultRoomNumber = "DEFAULT")
        {
            var defaultRoomIds = await _context.Rooms
                .Where(r => r.RoomNumber == defaultRoomNumber)
                .Select(r => r.Id)
                .ToListAsync();

            return await _context.PCs
                .AsNoTracking()
                .Where(p => defaultRoomIds.Contains(p.RoomId))
                .Select(p => new PCDto(
                    p.Id,
                    p.MacAddress,
                    p.IpAddress,
                    p.SubnetMask,
                    p.DefaultGateway,
                    p.RoomId,
                    p.Hostname,
                    p.OperatingSystem,
                    p.Status.ToString(),
                    p.LastSeen))
                .ToListAsync();
        }

        public async Task<bool> AssignPCsToRoomAsync(IEnumerable<int> pcIds, int roomId)
        {
            var ids = pcIds.ToList();
            if (!ids.Any()) return false;

            var pcs = await _context.PCs.Where(p => ids.Contains(p.Id)).ToListAsync();
            if (!pcs.Any()) return false;

            foreach (var pc in pcs)
            {
                pc.RoomId = roomId;
            }

            await _context.SaveChangesAsync();

            var roomNumber = await _context.Rooms
                .Where(r => r.Id == roomId)
                .Select(r => r.RoomNumber)
                .FirstOrDefaultAsync();

            await _authService.LogUserActionAsync(
                "PCs Assigned To Lab",
                $"Assigned {pcs.Count} PC(s) to lab {roomNumber ?? roomId.ToString()} (RoomId: {roomId})");

            return true;
        }
    }
}
