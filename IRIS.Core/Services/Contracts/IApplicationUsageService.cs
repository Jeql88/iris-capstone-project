using IRIS.Core.DTOs;

namespace IRIS.Core.Services.Contracts
{
    public interface IApplicationUsageService
    {
        Task RecordApplicationUsageAsync(ApplicationUsageCreateDto dto);
        Task RecordApplicationUsageBatchAsync(List<ApplicationUsageCreateDto> dtos);
    }
}
