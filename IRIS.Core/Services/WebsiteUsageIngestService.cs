using IRIS.Core.Data;
using IRIS.Core.DTOs;
using IRIS.Core.Models;
using IRIS.Core.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace IRIS.Core.Services;

public class WebsiteUsageIngestService : IWebsiteUsageIngestService
{
    private readonly IRISDbContext _context;

    public WebsiteUsageIngestService(IRISDbContext context)
    {
        _context = context;
    }

    public async Task UpsertBatchAsync(IReadOnlyCollection<WebsiteUsageIngestDto> items, CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
        {
            return;
        }

        foreach (var item in items)
        {
            var browser = item.Browser.Trim();
            var domain = item.Domain.Trim().ToLowerInvariant();
            var visitedAt = DateTime.SpecifyKind(item.VisitedAt, DateTimeKind.Utc);
            var visitCount = item.VisitCount <= 0 ? 1 : item.VisitCount;

            var existing = await _context.WebsiteUsageHistory
                .FirstOrDefaultAsync(w =>
                    w.PCId == item.PCId &&
                    w.Browser == browser &&
                    w.Domain == domain &&
                    w.VisitedAt == visitedAt,
                    cancellationToken);

            if (existing == null)
            {
                _context.WebsiteUsageHistory.Add(new WebsiteUsageHistory
                {
                    PCId = item.PCId,
                    Browser = browser,
                    Domain = domain,
                    VisitedAt = visitedAt,
                    VisitCount = visitCount,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.VisitCount += visitCount;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
