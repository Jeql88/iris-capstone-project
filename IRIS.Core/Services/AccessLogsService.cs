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

        public async Task<PaginatedResult<UserLog>> GetAccessLogsAsync(int pageNumber = 1, int pageSize = 10, string? search = null, string? action = null, UserRole? role = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.UserLogs
                .Include(ul => ul.User)
                .AsQueryable();

            if (startDate.HasValue)
            {
                query = query.Where(ul => ul.Timestamp >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(ul => ul.Timestamp <= endDate.Value);
            }

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                var normalizedSearch = search.Trim().ToLower();
                query = query.Where(ul =>
                    (ul.User != null && ul.User.Username.ToLower().Contains(normalizedSearch)) ||
                    ul.Action.ToLower().Contains(normalizedSearch) ||
                    (ul.Details != null && ul.Details.ToLower().Contains(normalizedSearch)) ||
                    (ul.IpAddress != null && ul.IpAddress.ToLower().Contains(normalizedSearch)));
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