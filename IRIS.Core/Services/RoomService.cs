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

        public RoomService(IRISDbContext context)
        {
            _context = context;
        }

        public async Task<List<RoomDto>> GetRoomsAsync()
        {
            return await _context.Rooms
                .AsNoTracking()
                .OrderBy(r => r.RoomNumber)
                .Select(r => new RoomDto(r.Id, r.RoomNumber, r.Description, r.Capacity, r.IsActive, r.CreatedAt))
                .ToListAsync();
        }

        public async Task<RoomDto?> GetRoomAsync(int id)
        {
            var room = await _context.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
            if (room == null) return null;
            return new RoomDto(room.Id, room.RoomNumber, room.Description, room.Capacity, room.IsActive, room.CreatedAt);
        }

        public async Task<RoomDto> CreateRoomAsync(RoomCreateUpdateDto request)
        {
            var room = new Room
            {
                RoomNumber = request.RoomNumber,
                Description = request.Description,
                Capacity = request.Capacity,
                IsActive = request.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            _context.Rooms.Add(room);
            await _context.SaveChangesAsync();

            return new RoomDto(room.Id, room.RoomNumber, room.Description, room.Capacity, room.IsActive, room.CreatedAt);
        }

        public async Task<RoomDto?> UpdateRoomAsync(int id, RoomCreateUpdateDto request)
        {
            var room = await _context.Rooms.FirstOrDefaultAsync(r => r.Id == id);
            if (room == null) return null;

            room.RoomNumber = request.RoomNumber;
            room.Description = request.Description;
            room.Capacity = request.Capacity;
            room.IsActive = request.IsActive;

            await _context.SaveChangesAsync();

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

            _context.Rooms.Remove(room);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
