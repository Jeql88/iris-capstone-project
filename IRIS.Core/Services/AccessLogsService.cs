using IRIS.Core.Data;
using IRIS.Core.Models;
using IRIS.Core.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace IRIS.Core.Services
{
    public class AccessLogsService : IAccessLogsService
    {
        private readonly IRISDbContext _context;

        public AccessLogsService(IRISDbContext context)
        {
            _context = context;
        }

        public async Task<PaginatedResult<UserLog>> GetAccessLogsAsync(int pageNumber = 1, int pageSize = 10, string? search = null, string? action = null, UserRole? role = null)
        {
            var query = _context.UserLogs
                .Include(ul => ul.User)
                .AsQueryable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(ul => 
                    (ul.User != null && ul.User.Username.Contains(search)) ||
                    ul.Action.Contains(search) ||
                    (ul.Details != null && ul.Details.Contains(search)) ||
                    (ul.IpAddress != null && ul.IpAddress.Contains(search)));
            }

            // Apply action filter
            if (!string.IsNullOrWhiteSpace(action) && action != "All Actions")
            {
                query = query.Where(ul => ul.Action == action);
            }

            // Apply role filter
            if (role.HasValue)
            {
                query = query.Where(ul => ul.User != null && ul.User.Role == role.Value);
            }

            var totalCount = await query.CountAsync();

            var logs = await query
                .OrderByDescending(ul => ul.Timestamp)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PaginatedResult<UserLog>
            {
                Items = logs,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }
    }
}