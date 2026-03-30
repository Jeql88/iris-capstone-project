namespace IRIS.UI.Services.Contracts
{
    public interface IHostFirewallBootstrapService
    {
        Task EnsurePowerCommandRuleAsync(CancellationToken cancellationToken = default);
        Task EnsureWallpaperFileRuleAsync(CancellationToken cancellationToken = default);
    }
}