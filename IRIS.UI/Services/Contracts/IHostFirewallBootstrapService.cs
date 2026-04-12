namespace IRIS.UI.Services.Contracts
{
    public interface IHostFirewallBootstrapService
    {
        Task EnsureWallpaperFileRuleAsync(CancellationToken cancellationToken = default);
    }
}