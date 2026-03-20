using IRIS.Core.DTOs;
using IRIS.Core.Models;

namespace IRIS.Core.Services.Contracts
{
    public interface IRoomService
    {
        Task<List<RoomDto>> GetRoomsAsync();
        Task<PaginatedResult<RoomDto>> GetRoomsPagedAsync(int pageNumber, int pageSize);
        Task<RoomDto?> GetRoomAsync(int id);
        Task<RoomDto> CreateRoomAsync(RoomCreateUpdateDto request);
        Task<RoomDto?> UpdateRoomAsync(int id, RoomCreateUpdateDto request);
        Task<bool> DeleteRoomAsync(int id);
    }
}
