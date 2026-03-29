namespace IRIS.UI.Services.Contracts
{
    public interface IWallpaperFileServer
    {
        void Start();
        Task StopAsync();
    }
}