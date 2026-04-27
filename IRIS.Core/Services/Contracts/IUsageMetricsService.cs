using IRIS.Core.DTOs;
using IRIS.Core.Models;

namespace IRIS.Core.Services.Contracts
{
    public interface IUsageMetricsService
    {
        Task<List<ApplicationUsageDto>> GetMostUsedApplicationsAsync(int days, int limit = 10);
        Task<PaginatedResult<ApplicationUsageDetailDto>> GetApplicationUsageDetailsPaginatedAsync(DateTime startDate, DateTime endDate, int pageNumber, int pageSize, string? searchText = null, string? roomFilter = null);
        Task<List<string>> GetApplicationUsageLaboratoriesAsync(DateTime startDate, DateTime endDate);
        Task<List<ApplicationUsageDetailDto>> GetApplicationUsageDetailsAsync(DateTime startDate, DateTime endDate);
        Task<PaginatedResult<WebsiteUsageDetailDto>> GetWebsiteUsageDetailsPaginatedAsync(DateTime startDate, DateTime endDate, int pageNumber, int pageSize, string? searchText = null, string? roomFilter = null);
        Task<List<string>> GetWebsiteUsageLaboratoriesAsync(DateTime startDate, DateTime endDate);
        Task<byte[]> ExportUsageMetricsToExcelAsync(DateTime startDate, DateTime endDate, string? appSearchText = null, string? webSearchText = null, string? appRoomFilter = null, string? webRoomFilter = null);
        Task<UsageMetricsSummaryDto> GetUsageSummaryAsync(DateTime startDate, DateTime endDate);
        Task<PaginatedResult<ApplicationUsageAggregatedDto>> GetApplicationUsageGroupedByApplicationAsync(DateTime startDate, DateTime endDate, int pageNumber, int pageSize, string? searchText = null, string? roomFilter = null);
        Task<PaginatedResult<PCUsageAggregatedDto>> GetApplicationUsageGroupedByPCAsync(DateTime startDate, DateTime endDate, int pageNumber, int pageSize, string? searchText = null, string? roomFilter = null);
    }
}
