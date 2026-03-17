using IRIS.Core.Data;
using IRIS.Core.Models;
using IRIS.Core.Services.Contracts;
using ClosedXML.Excel;
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
            var query = BuildFilteredAccessLogsQuery(search, action, role, startDate, endDate);

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

        public async Task<List<UserLog>> GetAccessLogsForExportAsync(string? search = null, string? action = null, UserRole? role = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = BuildFilteredAccessLogsQuery(search, action, role, startDate, endDate);

            return await query
                .OrderByDescending(ul => ul.Timestamp)
                .ToListAsync();
        }

        public async Task<byte[]> ExportAccessLogsToExcelAsync(string? search = null, string? action = null, UserRole? role = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            var logs = await GetAccessLogsForExportAsync(search, action, role, startDate, endDate);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Access Logs");

            worksheet.Cell(1, 1).Value = "Timestamp";
            worksheet.Cell(1, 2).Value = "Username";
            worksheet.Cell(1, 3).Value = "Role";
            worksheet.Cell(1, 4).Value = "Action";
            worksheet.Cell(1, 5).Value = "Details";
            worksheet.Cell(1, 6).Value = "IP Address";

            var headerRange = worksheet.Range(1, 1, 1, 6);
            headerRange.Style.Font.Bold = true;

            var row = 2;
            foreach (var log in logs)
            {
                var roleText = log.User?.Role.ToString()
                    .Replace("SystemAdministrator", "System Administrator")
                    .Replace("ITPersonnel", "IT Personnel") ?? "Unknown";

                worksheet.Cell(row, 1).Value = log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
                worksheet.Cell(row, 2).Value = log.User?.Username ?? "Unknown";
                worksheet.Cell(row, 3).Value = roleText;
                worksheet.Cell(row, 4).Value = log.Action;
                worksheet.Cell(row, 5).Value = log.Details ?? "N/A";
                worksheet.Cell(row, 6).Value = log.IpAddress ?? "N/A";
                row++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private IQueryable<UserLog> BuildFilteredAccessLogsQuery(string? search, string? action, UserRole? role, DateTime? startDate, DateTime? endDate)
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

            if (!string.IsNullOrWhiteSpace(search))
            {
                var normalizedSearch = search.Trim().ToLower();
                query = query.Where(ul =>
                    (ul.User != null && ul.User.Username.ToLower().Contains(normalizedSearch)) ||
                    ul.Action.ToLower().Contains(normalizedSearch) ||
                    (ul.Details != null && ul.Details.ToLower().Contains(normalizedSearch)) ||
                    (ul.IpAddress != null && ul.IpAddress.ToLower().Contains(normalizedSearch)));
            }

            if (!string.IsNullOrWhiteSpace(action) && action != "All Actions")
            {
                query = query.Where(ul => ul.Action == action);
            }

            if (role.HasValue)
            {
                query = query.Where(ul => ul.User != null && ul.User.Role == role.Value);
            }

            return query;
        }
    }
}