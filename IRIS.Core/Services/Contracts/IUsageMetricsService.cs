using IRIS.Core.DTOs;

namespace IRIS.Core.Services.Contracts
{
    public interface IUsageMetricsService
    {
        Task<List<ApplicationUsageDto>> GetMostUsedApplicationsAsync(int days, int limit = 10);
        Task<List<WebsiteUsageDto>> GetMostVisitedWebsitesAsync(int days, int limit = 10);
    }
}
