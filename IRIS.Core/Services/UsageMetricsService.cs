using IRIS.Core.Data;
using IRIS.Core.DTOs;
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
}
