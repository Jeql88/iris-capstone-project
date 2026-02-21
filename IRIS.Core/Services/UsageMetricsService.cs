using IRIS.Core.Data;
using IRIS.Core.DTOs;
using IRIS.Core.Models;
using IRIS.Core.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace IRIS.Core.Services;

public class UsageMetricsService : IUsageMetricsService
{
    private readonly IRISDbContext _context;

    public UsageMetricsService(IRISDbContext context)
    {
        _context = context;
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
        DateTime startDate, DateTime endDate, int pageNumber, int pageSize, string? searchText = null)
    {
        var query = _context.SoftwareUsageHistory
            .Include(s => s.PC)
                .ThenInclude(p => p.Room)
            .Where(s => s.StartTime >= startDate && s.StartTime <= endDate);

        if (!string.IsNullOrEmpty(searchText))
        {
            query = query.Where(s => 
                s.ApplicationName.Contains(searchText) ||
                (s.PC.Hostname != null && s.PC.Hostname.Contains(searchText)));
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

        var totalHours = (await _context.SoftwareUsageHistory
            .Where(s => s.StartTime >= startDate && s.StartTime <= endDate && s.Duration != null)
            .Select(s => s.Duration)
            .ToListAsync())
            .Sum(d => d?.TotalHours ?? 0);

        return new UsageMetricsSummaryDto
        {
            TotalApplications = totalApps,
            TotalWebsites = totalWebsites,
            TotalHours = totalHours
        };
    }

    public async Task<PaginatedResult<WebsiteUsageDetailDto>> GetWebsiteUsageDetailsPaginatedAsync(
        DateTime startDate, DateTime endDate, int pageNumber, int pageSize, string? searchText = null)
    {
        var query = _context.WebsiteUsageHistory
            .Where(w => w.VisitedAt >= startDate && w.VisitedAt <= endDate);

        if (!string.IsNullOrEmpty(searchText))
        {
            query = query.Where(w =>
                w.Domain.Contains(searchText) ||
                w.Browser.Contains(searchText) ||
                (w.PC.Hostname != null && w.PC.Hostname.Contains(searchText)) ||
                (w.PC.Room != null && w.PC.Room.RoomNumber.Contains(searchText)));
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
}
