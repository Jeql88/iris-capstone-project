namespace IRIS.Agent.Interfaces
{
    public interface IWallpaperPolicyEnforcer
    {
        Task<bool> EnforceWallpaperPolicyAsync();
        Task<bool> CheckAndEnforceWallpaperComplianceAsync();
    }
}