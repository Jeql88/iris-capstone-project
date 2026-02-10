using IRIS.Core.DTOs;

namespace IRIS.Core.Services.Contracts
{
    public interface IPCAdminService
    {
        Task<List<PCDto>> GetUnassignedPCsAsync(string defaultRoomNumber = "DEFAULT");
        Task<bool> AssignPCsToRoomAsync(IEnumerable<int> pcIds, int roomId);
    }
}
