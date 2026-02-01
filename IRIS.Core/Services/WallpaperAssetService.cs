using System.Security.Cryptography;
using IRIS.Core.Data;
using IRIS.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace IRIS.Core.Services
{
    public interface IWallpaperAssetService
    {
        Task<WallpaperAsset?> GetActiveWallpaperAsync();
        Task<WallpaperAsset> UploadWallpaperAsync(string fileName, byte[] fileData, string uploadedBy);
        Task<bool> SetActiveWallpaperAsync(int wallpaperAssetId);
        Task<IEnumerable<WallpaperAsset>> GetAllWallpapersAsync();
        Task<bool> DeleteWallpaperAsync(int wallpaperAssetId);
    }

    public class WallpaperAssetService : IWallpaperAssetService
    {
        private readonly IRISDbContext _context;
        private readonly string _wallpaperStoragePath;

        public WallpaperAssetService(IRISDbContext context)
        {
            _context = context;
            _wallpaperStoragePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "IRIS", "Assets", "Wallpapers", "Server"
            );
            
            // Ensure storage directory exists
            Directory.CreateDirectory(_wallpaperStoragePath);
        }

        public async Task<WallpaperAsset?> GetActiveWallpaperAsync()
        {
            return await _context.WallpaperAssets
                .Where(w => w.IsActive)
                .OrderByDescending(w => w.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<WallpaperAsset> UploadWallpaperAsync(string fileName, byte[] fileData, string uploadedBy)
        {
            // Calculate hash
            using var sha256 = SHA256.Create();
            var hash = Convert.ToHexString(sha256.ComputeHash(fileData));

            // Check if wallpaper with same hash already exists
            var existingWallpaper = await _context.WallpaperAssets
                .FirstOrDefaultAsync(w => w.Hash == hash);

            if (existingWallpaper != null)
            {
                throw new InvalidOperationException("A wallpaper with the same content already exists.");
            }

            // Generate unique filename
            var fileExtension = Path.GetExtension(fileName);
            var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(_wallpaperStoragePath, uniqueFileName);

            // Save file to disk
            await File.WriteAllBytesAsync(filePath, fileData);

            // Create database record
            var wallpaperAsset = new WallpaperAsset
            {
                FileName = fileName,
                Hash = hash,
                FileSize = fileData.Length,
                FilePath = filePath,
                IsActive = false, // Not active by default
                UploadedBy = uploadedBy,
                CreatedAt = DateTime.UtcNow
            };

            _context.WallpaperAssets.Add(wallpaperAsset);
            await _context.SaveChangesAsync();

            return wallpaperAsset;
        }

        public async Task<bool> SetActiveWallpaperAsync(int wallpaperAssetId)
        {
            var wallpaper = await _context.WallpaperAssets.FindAsync(wallpaperAssetId);
            if (wallpaper == null || !File.Exists(wallpaper.FilePath))
            {
                return false;
            }

            // Deactivate all other wallpapers
            var allWallpapers = await _context.WallpaperAssets.ToListAsync();
            foreach (var w in allWallpapers)
            {
                w.IsActive = false;
            }

            // Activate the selected wallpaper
            wallpaper.IsActive = true;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<WallpaperAsset>> GetAllWallpapersAsync()
        {
            return await _context.WallpaperAssets
                .OrderByDescending(w => w.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> DeleteWallpaperAsync(int wallpaperAssetId)
        {
            var wallpaper = await _context.WallpaperAssets.FindAsync(wallpaperAssetId);
            if (wallpaper == null)
            {
                return false;
            }

            // Don't allow deletion of active wallpaper
            if (wallpaper.IsActive)
            {
                throw new InvalidOperationException("Cannot delete the active wallpaper. Please set another wallpaper as active first.");
            }

            // Delete file from disk
            if (File.Exists(wallpaper.FilePath))
            {
                File.Delete(wallpaper.FilePath);
            }

            // Remove from database
            _context.WallpaperAssets.Remove(wallpaper);
            await _context.SaveChangesAsync();

            return true;
        }
    }
}