using System.Threading.Tasks;

namespace IRIS.Agent.Services.Contracts
{
    public interface IWallpaperPolicyService
    {
        Task<bool> EnforceWallpaperPolicyAsync();
        Task<bool> CheckAndEnforceWallpaperComplianceAsync();
    }
}
