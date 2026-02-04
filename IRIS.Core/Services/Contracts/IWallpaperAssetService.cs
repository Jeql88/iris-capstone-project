using IRIS.Core.Models;

namespace IRIS.Core.Services.Contracts
{
    public interface IWallpaperAssetService
    {
        Task<WallpaperAsset?> GetActiveWallpaperAsync();
        Task<WallpaperAsset> UploadWallpaperAsync(string fileName, byte[] fileData, string uploadedBy);
        Task<bool> SetActiveWallpaperAsync(int wallpaperAssetId);
        Task<IEnumerable<WallpaperAsset>> GetAllWallpapersAsync();
        Task<bool> DeleteWallpaperAsync(int wallpaperAssetId);
    }
}
