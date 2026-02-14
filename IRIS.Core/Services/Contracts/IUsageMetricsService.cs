using IRIS.Core.DTOs;
using IRIS.Core.Models;

namespace IRIS.Core.Services.Contracts
{
    public interface IUsageMetricsService
    {
        Task<List<ApplicationUsageDto>> GetMostUsedApplicationsAsync(int days, int limit = 10);
        Task<List<WebsiteUsageDto>> GetMostVisitedWebsitesAsync(int days, int limit = 10);
        Task<PaginatedResult<ApplicationUsageDetailDto>> GetApplicationUsageDetailsPaginatedAsync(DateTime startDate, DateTime endDate, int pageNumber, int pageSize, string? searchText = null);
        Task<PaginatedResult<WebsiteUsageDetailDto>> GetWebsiteUsageDetailsPaginatedAsync(DateTime startDate, DateTime endDate, int pageNumber, int pageSize, string? searchText = null);
        Task<List<ApplicationUsageDetailDto>> GetApplicationUsageDetailsAsync(DateTime startDate, DateTime endDate);
        Task<List<WebsiteUsageDetailDto>> GetWebsiteUsageDetailsAsync(DateTime startDate, DateTime endDate);
        Task<UsageMetricsSummaryDto> GetUsageSummaryAsync(DateTime startDate, DateTime endDate);
    }
}
