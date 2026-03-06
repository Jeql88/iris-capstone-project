using IRIS.Core.DTOs;

namespace IRIS.Core.Services.Contracts;

public interface IWebsiteUsageIngestService
{
    Task UpsertBatchAsync(IReadOnlyCollection<WebsiteUsageIngestDto> items, CancellationToken cancellationToken = default);
}
