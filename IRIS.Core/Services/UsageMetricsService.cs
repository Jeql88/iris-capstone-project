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

    public async Task<List<WebsiteUsageDto>> GetMostVisitedWebsitesAsync(int days, int limit = 10)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-days);

        var totalCount = await _context.WebsiteUsageHistory
            .Where(w => w.CreatedAt >= cutoffDate)
            .CountAsync();

        if (totalCount == 0)
            return new List<WebsiteUsageDto>();

        var results = await _context.WebsiteUsageHistory
            .Where(w => w.CreatedAt >= cutoffDate && w.Domain != null)
            .GroupBy(w => w.Domain)
            .Select(g => new WebsiteUsageDto
            {
                Domain = g.Key!,
                VisitCount = g.Count(),
                Percentage = 0
            })
            .OrderByDescending(w => w.VisitCount)
            .Take(limit)
            .ToListAsync();

        foreach (var result in results)
        {
            result.Percentage = Math.Round((double)result.VisitCount / totalCount * 100, 1);
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

    public async Task<PaginatedResult<WebsiteUsageDetailDto>> GetWebsiteUsageDetailsPaginatedAsync(
        DateTime startDate, DateTime endDate, int pageNumber, int pageSize, string? searchText = null)
    {
        var query = _context.WebsiteUsageHistory
            .Include(w => w.PC)
                .ThenInclude(p => p.Room)
            .Where(w => w.VisitedAt >= startDate && w.VisitedAt <= endDate);

        if (!string.IsNullOrEmpty(searchText))
        {
            query = query.Where(w => 
                w.Url.Contains(searchText) ||
                (w.Domain != null && w.Domain.Contains(searchText)) ||
                (w.PC.Hostname != null && w.PC.Hostname.Contains(searchText)));
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(w => w.VisitedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(w => new WebsiteUsageDetailDto
            {
                Id = w.Id,
                Url = w.Url,
                Title = w.Domain ?? w.Url,
                PCName = w.PC.Hostname ?? "Unknown",
                RoomNumber = w.PC.Room != null ? w.PC.Room.RoomNumber : "Unassigned",
                VisitTime = w.VisitedAt,
                VisitCount = w.VisitCount
            })
            .ToListAsync();

        return new PaginatedResult<WebsiteUsageDetailDto>
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

    public async Task<List<WebsiteUsageDetailDto>> GetWebsiteUsageDetailsAsync(DateTime startDate, DateTime endDate)
    {
        return await _context.WebsiteUsageHistory
            .Include(w => w.PC)
                .ThenInclude(p => p.Room)
            .Where(w => w.VisitedAt >= startDate && w.VisitedAt <= endDate)
            .OrderByDescending(w => w.VisitedAt)
            .Select(w => new WebsiteUsageDetailDto
            {
                Id = w.Id,
                Url = w.Url,
                Title = w.Domain ?? w.Url,
                PCName = w.PC.Hostname ?? "Unknown",
                RoomNumber = w.PC.Room != null ? w.PC.Room.RoomNumber : "Unassigned",
                VisitTime = w.VisitedAt,
                VisitCount = w.VisitCount
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
}
