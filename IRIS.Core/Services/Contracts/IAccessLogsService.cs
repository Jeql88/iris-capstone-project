using IRIS.Core.Models;

namespace IRIS.Core.Services.Contracts
{
    public interface IAccessLogsService
    {
        Task<PaginatedResult<UserLog>> GetAccessLogsAsync(int pageNumber = 1, int pageSize = 10, string? search = null, string? action = null, UserRole? role = null, DateTime? startDate = null, DateTime? endDate = null);
    }
}
