using IRIS.Core.DTOs;

namespace IRIS.Core.Services;

public interface IApplicationUsageService
{
    Task RecordApplicationUsageAsync(ApplicationUsageCreateDto dto);
    Task RecordApplicationUsageBatchAsync(List<ApplicationUsageCreateDto> dtos);
}
