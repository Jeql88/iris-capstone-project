using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Serilog;
using IRIS.Core.Data;
using IRIS.Core.Models;

namespace IRIS.Agent.Logic
{
    public class WallpaperPolicyEnforcer
    {
        private readonly IRISDbContext _context;
        private readonly string _macAddress;
        private readonly string _wallpaperCachePath;

        public WallpaperPolicyEnforcer(IRISDbContext context, string macAddress)
        {
            _context = context;
            _macAddress = macAddress;

            _wallpaperCachePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "IRIS", "Assets", "Wallpapers"
            );

            Directory.CreateDirectory(_wallpaperCachePath);
        }

        public async Task<bool> EnforceWallpaperPolicyAsync()
        {
            try
            {
                var policy = await LoadActiveWallpaperPolicyAsync();
                if (policy == null)
                {
                    Log.Debug("No active wallpaper policy found for PC {MacAddress}", _macAddress);
                    return false;
                }

                return await ApplyWallpaperAsync(policy);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to enforce wallpaper policy for PC {MacAddress}", _macAddress);
                return false;
            }
        }

        public async Task<bool> CheckAndEnforceWallpaperComplianceAsync()
        {
            try
            {
                var policy = await LoadActiveWallpaperPolicyAsync();
                if (policy == null)
                    return false;

                var cachedWallpaperPath = ResolveCachedWallpaperPath(policy.WallpaperFileName);
                if (!File.Exists(cachedWallpaperPath))
                {
                    return await ApplyWallpaperAsync(policy);
                }

                var currentWallpaper = GetCurrentWallpaperPath();
                if (!string.Equals(currentWallpaper, cachedWallpaperPath, StringComparison.OrdinalIgnoreCase))
                {
                    Log.Information("Wallpaper compliance violation detected, re-enforcing policy for PC {MacAddress}", _macAddress);
                    return await ApplyWallpaperAsync(policy);
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to check wallpaper compliance for PC {MacAddress}", _macAddress);
                return false;
            }
        }

        private async Task<Policy?> LoadActiveWallpaperPolicyAsync()
        {
            var pc = await _context.PCs
                .Include(p => p.Room)
                .ThenInclude(r => r.Policies)
                .FirstOrDefaultAsync(p => p.MacAddress == _macAddress);

            return pc?.Room?.Policies?.FirstOrDefault(p =>
                p.IsActive
                && p.ResetWallpaperOnStartup
                && p.WallpaperData != null
                && p.WallpaperData.Length > 0);
        }

        private async Task<bool> ApplyWallpaperAsync(Policy policy)
        {
            try
            {
                if (policy.WallpaperData == null || policy.WallpaperData.Length == 0)
                {
                    Log.Warning("Wallpaper policy for PC {MacAddress} has no data", _macAddress);
                    return false;
                }

                var cachedWallpaperPath = ResolveCachedWallpaperPath(policy.WallpaperFileName);

                var expectedHash = !string.IsNullOrWhiteSpace(policy.WallpaperHash)
                    ? policy.WallpaperHash!
                    : Convert.ToHexString(SHA256.HashData(policy.WallpaperData));

                var needsWrite = true;
                if (File.Exists(cachedWallpaperPath))
                {
                    var cachedHash = await CalculateFileHashAsync(cachedWallpaperPath);
                    needsWrite = !string.Equals(cachedHash, expectedHash, StringComparison.OrdinalIgnoreCase);
                }

                if (needsWrite)
                {
                    await File.WriteAllBytesAsync(cachedWallpaperPath, policy.WallpaperData);
                    Log.Information("Wallpaper cache updated for PC {MacAddress}", _macAddress);
                }
                else
                {
                    Log.Debug("Wallpaper cache already up to date for PC {MacAddress}", _macAddress);
                }

                var result = NativeMethods.SystemParametersInfo(NativeMethods.SPI_SETDESKWALLPAPER, 0, cachedWallpaperPath,
                    NativeMethods.SPIF_UPDATEINIFILE | NativeMethods.SPIF_SENDCHANGE);

                if (result != 0)
                {
                    Log.Information("Wallpaper policy enforced successfully for PC {MacAddress}", _macAddress);
                    return true;
                }

                Log.Warning("Failed to set wallpaper via Windows API for PC {MacAddress}", _macAddress);
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to apply wallpaper for PC {MacAddress}", _macAddress);
                return false;
            }
        }

        private string ResolveCachedWallpaperPath(string? fileName)
        {
            var extension = ".jpg";
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                var ext = Path.GetExtension(fileName);
                if (!string.IsNullOrWhiteSpace(ext))
                {
                    extension = ext;
                }
            }

            return Path.Combine(_wallpaperCachePath, "active_wallpaper" + extension);
        }

        private static async Task<string> CalculateFileHashAsync(string filePath)
        {
            using var sha256 = SHA256.Create();
            await using var stream = File.OpenRead(filePath);
            var hash = await sha256.ComputeHashAsync(stream);
            return Convert.ToHexString(hash);
        }

        private static string? GetCurrentWallpaperPath()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
                return key?.GetValue("Wallpaper")?.ToString();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to get current wallpaper path");
                return null;
            }
        }
    }
}
