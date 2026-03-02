using IRIS.Core.DTOs;

namespace IRIS.Core.Services.Contracts
{
    public interface IDeploymentDataService
    {
        Task<List<DeploymentPCDto>> GetRegisteredPCsAsync(int? roomId = null);
        Task LogDeploymentResultAsync(DeploymentLogCreateDto dto);
        Task<List<DeploymentLogDto>> GetRecentDeploymentLogsAsync(int take = 100);
    }
}
