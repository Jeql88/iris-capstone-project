using IRIS.Core.Data;
using IRIS.Core.DTOs;
using IRIS.Core.Models;
using IRIS.Core.Services.Contracts;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace IRIS.Core.Services;

public class UsageMetricsService : IUsageMetricsService
{
    private readonly IRISDbContext _context;
    private readonly IAuthenticationService _authService;

    public UsageMetricsService(IRISDbContext context, IAuthenticationService authService)
    {
        _context = context;
        _authService = authService;
    }

    public async Task<List<ApplicationUsageDto>> GetMostUsedApplicationsAsync(int days, int limit = 10)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-days);

        var totalCount = await _context.SoftwareUsageHistory
            .Where(s => s.CreatedAt >= cutoffDate)
            .CountAsync();

        if (totalCount == 0)
            return new List<ApplicationUsageDto>();

        var results = await _context.SoftwareUsageHistory
            .Where(s => s.CreatedAt >= cutoffDate)
            .GroupBy(s => s.ApplicationName)
            .Select(g => new ApplicationUsageDto
            {
                ApplicationName = g.Key,
                UsageCount = g.Count(),
                Percentage = 0
            })
            .OrderByDescending(a => a.UsageCount)
            .Take(limit)
            .ToListAsync();

        foreach (var result in results)
        {
            result.Percentage = Math.Round((double)result.UsageCount / totalCount * 100, 1);
        }

        return results;
    }

    public async Task<PaginatedResult<ApplicationUsageDetailDto>> GetApplicationUsageDetailsPaginatedAsync(
        DateTime startDate, DateTime endDate, int pageNumber, int pageSize, string? searchText = null, string? roomFilter = null)
    {
        var query = _context.SoftwareUsageHistory
            .Include(s => s.PC)
                .ThenInclude(p => p.Room)
            .Where(s => s.StartTime >= startDate && s.StartTime <= endDate);

        if (!string.IsNullOrEmpty(searchText))
        {
            var normalizedSearch = searchText.Trim().ToLower();
            query = query.Where(s =>
                s.ApplicationName.ToLower().Contains(normalizedSearch) ||
                (s.PC.Hostname != null && s.PC.Hostname.ToLower().Contains(normalizedSearch)));
        }

        if (!string.IsNullOrWhiteSpace(roomFilter))
        {
            query = query.Where(s => s.PC.Room != null && s.PC.Room.RoomNumber == roomFilter);
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(s => s.StartTime)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new ApplicationUsageDetailDto
            {
                Id = s.Id,
                ApplicationName = s.ApplicationName,
                PCName = s.PC.Hostname ?? "Unknown",
                RoomNumber = s.PC.Room != null ? s.PC.Room.RoomNumber : "Unassigned",
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                Duration = s.Duration
            })
            .ToListAsync();

        return new PaginatedResult<ApplicationUsageDetailDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<List<string>> GetApplicationUsageLaboratoriesAsync(DateTime startDate, DateTime endDate)
    {
        return await _context.SoftwareUsageHistory
            .Where(s => s.StartTime >= startDate && s.StartTime <= endDate && s.PC.Room != null)
            .Select(s => s.PC.Room.RoomNumber)
            .Distinct()
            .OrderBy(room => room)
            .ToListAsync();
    }

    public async Task<List<ApplicationUsageDetailDto>> GetApplicationUsageDetailsAsync(DateTime startDate, DateTime endDate)
    {
        return await _context.SoftwareUsageHistory
            .Include(s => s.PC)
                .ThenInclude(p => p.Room)
            .Where(s => s.StartTime >= startDate && s.StartTime <= endDate)
            .OrderByDescending(s => s.StartTime)
            .Select(s => new ApplicationUsageDetailDto
            {
                Id = s.Id,
                ApplicationName = s.ApplicationName,
                PCName = s.PC.Hostname ?? "Unknown",
                RoomNumber = s.PC.Room != null ? s.PC.Room.RoomNumber : "Unassigned",
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                Duration = s.Duration
            })
            .ToListAsync();
    }


    public async Task<UsageMetricsSummaryDto> GetUsageSummaryAsync(DateTime startDate, DateTime endDate)
    {
        var totalApps = await _context.SoftwareUsageHistory
            .Where(s => s.StartTime >= startDate && s.StartTime <= endDate)
            .Select(s => s.ApplicationName)
            .Distinct()
            .CountAsync();

        var totalWebsites = await _context.WebsiteUsageHistory
            .Where(w => w.VisitedAt >= startDate && w.VisitedAt <= endDate)
            .Select(w => w.Domain)
            .Distinct()
            .CountAsync();

        return new UsageMetricsSummaryDto
        {
            TotalApplications = totalApps,
            TotalWebsites = totalWebsites
        };
    }

    public async Task<PaginatedResult<WebsiteUsageDetailDto>> GetWebsiteUsageDetailsPaginatedAsync(
        DateTime startDate, DateTime endDate, int pageNumber, int pageSize, string? searchText = null, string? roomFilter = null)
    {
        var query = _context.WebsiteUsageHistory
            .Where(w => w.VisitedAt >= startDate && w.VisitedAt <= endDate);

        if (!string.IsNullOrEmpty(searchText))
        {
            var normalizedSearch = searchText.Trim().ToLower();
            query = query.Where(w =>
                w.Domain.ToLower().Contains(normalizedSearch) ||
                w.Browser.ToLower().Contains(normalizedSearch) ||
                (w.PC.Hostname != null && w.PC.Hostname.ToLower().Contains(normalizedSearch)) ||
                (w.PC.Room != null && w.PC.Room.RoomNumber.ToLower().Contains(normalizedSearch)));
        }

        if (!string.IsNullOrWhiteSpace(roomFilter))
        {
            query = query.Where(w => w.PC.Room != null && w.PC.Room.RoomNumber == roomFilter);
        }

        var groupedQuery = query
            .GroupBy(w => new
            {
                w.Domain,
                w.Browser,
                PCName = w.PC.Hostname,
                RoomNumber = w.PC.Room != null ? w.PC.Room.RoomNumber : "Unassigned"
            })
            .Select(g => new WebsiteUsageDetailDto
            {
                Id = 0,
                Domain = g.Key.Domain,
                Browser = g.Key.Browser,
                PCName = g.Key.PCName ?? "Unknown",
                RoomNumber = g.Key.RoomNumber,
                VisitTime = g.Max(x => x.VisitedAt),
                VisitCount = g.Sum(x => x.VisitCount)
            });

        var totalCount = await groupedQuery.CountAsync();

        var items = await groupedQuery
            .OrderByDescending(w => w.VisitCount)
            .ThenByDescending(w => w.VisitTime)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PaginatedResult<WebsiteUsageDetailDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<List<string>> GetWebsiteUsageLaboratoriesAsync(DateTime startDate, DateTime endDate)
    {
        return await _context.WebsiteUsageHistory
            .Where(w => w.VisitedAt >= startDate && w.VisitedAt <= endDate && w.PC.Room != null)
            .Select(w => w.PC.Room.RoomNumber)
            .Distinct()
            .OrderBy(room => room)
            .ToListAsync();
    }

    public async Task<byte[]> ExportUsageMetricsToExcelAsync(DateTime startDate, DateTime endDate, string? appSearchText = null, string? webSearchText = null, string? appRoomFilter = null, string? webRoomFilter = null)
    {
        var appQuery = _context.SoftwareUsageHistory
            .Include(s => s.PC)
                .ThenInclude(p => p.Room)
            .Where(s => s.StartTime >= startDate && s.StartTime <= endDate);

        if (!string.IsNullOrWhiteSpace(appSearchText))
        {
            var normalizedAppSearch = appSearchText.Trim().ToLower();
            appQuery = appQuery.Where(s =>
                s.ApplicationName.ToLower().Contains(normalizedAppSearch) ||
                (s.PC.Hostname != null && s.PC.Hostname.ToLower().Contains(normalizedAppSearch)));
        }

        if (!string.IsNullOrWhiteSpace(appRoomFilter))
        {
            appQuery = appQuery.Where(s => s.PC.Room != null && s.PC.Room.RoomNumber == appRoomFilter);
        }

        var appItems = await appQuery
            .OrderByDescending(s => s.StartTime)
            .Select(s => new ApplicationUsageDetailDto
            {
                Id = s.Id,
                ApplicationName = s.ApplicationName,
                PCName = s.PC.Hostname ?? "Unknown",
                RoomNumber = s.PC.Room != null ? s.PC.Room.RoomNumber : "Unassigned",
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                Duration = s.Duration
            })
            .ToListAsync();

        var webQuery = _context.WebsiteUsageHistory
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(webSearchText))
        {
            var normalizedWebSearch = webSearchText.Trim().ToLower();
            webQuery = webQuery.Where(w =>
                w.Domain.ToLower().Contains(normalizedWebSearch) ||
                w.Browser.ToLower().Contains(normalizedWebSearch) ||
                (w.PC.Hostname != null && w.PC.Hostname.ToLower().Contains(normalizedWebSearch)) ||
                (w.PC.Room != null && w.PC.Room.RoomNumber.ToLower().Contains(normalizedWebSearch)));
        }

        if (!string.IsNullOrWhiteSpace(webRoomFilter))
        {
            webQuery = webQuery.Where(w => w.PC.Room != null && w.PC.Room.RoomNumber == webRoomFilter);
        }

        var webItems = await webQuery
            .GroupBy(w => new
            {
                w.Domain,
                w.Browser,
                PCName = w.PC.Hostname,
                RoomNumber = w.PC.Room != null ? w.PC.Room.RoomNumber : "Unassigned"
            })
            .Select(g => new WebsiteUsageDetailDto
            {
                Id = 0,
                Domain = g.Key.Domain,
                Browser = g.Key.Browser,
                PCName = g.Key.PCName ?? "Unknown",
                RoomNumber = g.Key.RoomNumber,
                VisitTime = g.Max(x => x.VisitedAt),
                VisitCount = g.Sum(x => x.VisitCount)
            })
            .OrderByDescending(w => w.VisitCount)
            .ThenByDescending(w => w.VisitTime)
            .ToListAsync();

        using var workbook = new XLWorkbook();

        var appSheet = workbook.Worksheets.Add("Application Usage");
        appSheet.Cell(1, 1).Value = "Application";
        appSheet.Cell(1, 2).Value = "PC Name";
        appSheet.Cell(1, 3).Value = "Room";
        appSheet.Cell(1, 4).Value = "Start Time (UTC)";
        appSheet.Cell(1, 5).Value = "End Time (UTC)";
        appSheet.Cell(1, 6).Value = "Duration";

        for (var index = 0; index < appItems.Count; index++)
        {
            var row = index + 2;
            var item = appItems[index];

            appSheet.Cell(row, 1).Value = item.ApplicationName;
            appSheet.Cell(row, 2).Value = item.PCName;
            appSheet.Cell(row, 3).Value = item.RoomNumber;
            appSheet.Cell(row, 4).Value = item.StartTime;
            appSheet.Cell(row, 5).Value = item.EndTime;
            appSheet.Cell(row, 6).Value = item.Duration?.ToString() ?? "Active";
        }

        var webSheet = workbook.Worksheets.Add("Website Usage");
        webSheet.Cell(1, 1).Value = "Domain";
        webSheet.Cell(1, 2).Value = "Browser";
        webSheet.Cell(1, 3).Value = "PC Name";
        webSheet.Cell(1, 4).Value = "Room";
        webSheet.Cell(1, 5).Value = "Visit Count";
        webSheet.Cell(1, 6).Value = "Last Visit (UTC)";

        for (var index = 0; index < webItems.Count; index++)
        {
            var row = index + 2;
            var item = webItems[index];

            webSheet.Cell(row, 1).Value = item.Domain;
            webSheet.Cell(row, 2).Value = item.Browser;
            webSheet.Cell(row, 3).Value = item.PCName;
            webSheet.Cell(row, 4).Value = item.RoomNumber;
            webSheet.Cell(row, 5).Value = item.VisitCount;
            webSheet.Cell(row, 6).Value = item.VisitTime;
        }

        appSheet.Columns().AdjustToContents();
        webSheet.Columns().AdjustToContents();

        appSheet.RangeUsed()?.SetAutoFilter();
        webSheet.RangeUsed()?.SetAutoFilter();

        await _authService.LogUserActionAsync(
            "Usage Metrics Exported",
            $"Exported usage metrics from {startDate:yyyy-MM-dd HH:mm:ss} to {endDate:yyyy-MM-dd HH:mm:ss}. App rows: {appItems.Count}, Web rows: {webItems.Count}");

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
