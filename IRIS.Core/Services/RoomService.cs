using IRIS.Core.Data;
using IRIS.Core.DTOs;
using IRIS.Core.Models;
using IRIS.Core.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace IRIS.Core.Services
{
    public class RoomService : IRoomService
    {
        private readonly IRISDbContext _context;
        private readonly IAuthenticationService _authService;

        public RoomService(IRISDbContext context, IAuthenticationService authService)
        {
            _context = context;
            _authService = authService;
        }

        public async Task<List<RoomDto>> GetRoomsAsync()
        {
            return await _context.Rooms
                .AsNoTracking()
                .Where(r => r.RoomNumber != "DEFAULT")
                .OrderBy(r => r.RoomNumber)
                .Select(r => new RoomDto(r.Id, r.RoomNumber, r.Description, r.Capacity, r.IsActive, r.CreatedAt))
                .ToListAsync();
        }

        public async Task<PaginatedResult<RoomDto>> GetRoomsPagedAsync(int pageNumber, int pageSize)
        {
            var query = _context.Rooms.AsNoTracking().Where(r => r.RoomNumber != "DEFAULT").OrderBy(r => r.RoomNumber);
            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new RoomDto(r.Id, r.RoomNumber, r.Description, r.Capacity, r.IsActive, r.CreatedAt))
                .ToListAsync();

            return new PaginatedResult<RoomDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<RoomDto?> GetRoomAsync(int id)
        {
            var room = await _context.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
            if (room == null) return null;
            return new RoomDto(room.Id, room.RoomNumber, room.Description, room.Capacity, room.IsActive, room.CreatedAt);
        }

        public async Task<RoomDto> CreateRoomAsync(RoomCreateUpdateDto request)
        {
            var normalizedRoomNumber = request.RoomNumber?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedRoomNumber))
            {
                throw new InvalidOperationException("Room number is required.");
            }

            var exists = await _context.Rooms
                .AnyAsync(r => r.RoomNumber.ToUpper() == normalizedRoomNumber.ToUpper());

            if (exists)
            {
                throw new InvalidOperationException("A room with the same number already exists.");
            }

            var room = new Room
            {
                RoomNumber = normalizedRoomNumber,
                Description = request.Description,
                Capacity = request.Capacity,
                IsActive = request.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            _context.Rooms.Add(room);
            await _context.SaveChangesAsync();

            await _authService.LogUserActionAsync(
                "Lab Created",
                $"Created lab {room.RoomNumber} (ID: {room.Id}, Capacity: {room.Capacity})");

            return new RoomDto(room.Id, room.RoomNumber, room.Description, room.Capacity, room.IsActive, room.CreatedAt);
        }

        public async Task<RoomDto?> UpdateRoomAsync(int id, RoomCreateUpdateDto request)
        {
            var room = await _context.Rooms.FirstOrDefaultAsync(r => r.Id == id);
            if (room == null) return null;

            var normalizedRoomNumber = request.RoomNumber?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedRoomNumber))
            {
                throw new InvalidOperationException("Room number is required.");
            }

            var exists = await _context.Rooms
                .AnyAsync(r => r.Id != id && r.RoomNumber.ToUpper() == normalizedRoomNumber.ToUpper());

            if (exists)
            {
                throw new InvalidOperationException("A room with the same number already exists.");
            }

            room.RoomNumber = normalizedRoomNumber;
            room.Description = request.Description;
            room.Capacity = request.Capacity;
            room.IsActive = request.IsActive;

            await _context.SaveChangesAsync();

            await _authService.LogUserActionAsync(
                "Lab Updated",
                $"Updated lab {room.RoomNumber} (ID: {room.Id})");

            return new RoomDto(room.Id, room.RoomNumber, room.Description, room.Capacity, room.IsActive, room.CreatedAt);
        }

        public async Task<bool> DeleteRoomAsync(int id)
        {
            var room = await _context.Rooms.FirstOrDefaultAsync(r => r.Id == id);
            if (room == null) return false;

            if (room.RoomNumber == "DEFAULT")
            {
                return false; // safeguard: do not delete DEFAULT room
            }

            var defaultRoom = await EnsureDefaultRoomAsync();

            // Reassign PCs to default room
            var roomPcs = await _context.PCs.Where(p => p.RoomId == room.Id).ToListAsync();
            foreach (var pc in roomPcs)
            {
                pc.RoomId = defaultRoom.Id;
            }

            // Delete associated policies
            var roomPolicies = await _context.Policies.Where(p => p.RoomId == room.Id).ToListAsync();
            _context.Policies.RemoveRange(roomPolicies);

            _context.Rooms.Remove(room);
            await _context.SaveChangesAsync();

            await _authService.LogUserActionAsync(
                "Lab Deleted",
                $"Deleted lab {room.RoomNumber} (ID: {room.Id})");

            return true;
        }

        private async Task<Room> EnsureDefaultRoomAsync()
        {
            var defaultRoom = await _context.Rooms.FirstOrDefaultAsync(r => r.RoomNumber == "DEFAULT");
            if (defaultRoom != null)
            {
                return defaultRoom;
            }

            defaultRoom = new Room
            {
                RoomNumber = "DEFAULT",
                Description = "Default Room",
                Capacity = 0,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Rooms.Add(defaultRoom);
            await _context.SaveChangesAsync();
            return defaultRoom;
        }
    }
}
