using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
        private readonly string _wallpaperApiToken;
        private static readonly HttpClient _httpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        // Windows API constants
        private const int SPI_SETDESKWALLPAPER = 20;
        private const int SPIF_UPDATEINIFILE = 0x01;
        private const int SPIF_SENDCHANGE = 0x02;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        public WallpaperPolicyEnforcer(IRISDbContext context, string macAddress, IConfiguration? configuration = null)
        {
            _context = context;
            _macAddress = macAddress;
            _wallpaperApiToken = (configuration?["AgentSettings:WallpaperApiToken"] ?? string.Empty).Trim();
            _wallpaperCachePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "IRIS", "Assets", "Wallpapers"
            );
            
            // Ensure cache directory exists
            Directory.CreateDirectory(_wallpaperCachePath);
        }

        public async Task<bool> EnforceWallpaperPolicyAsync()
        {
            try
            {
                var pc = await _context.PCs
                    .Include(p => p.Room)
                    .ThenInclude(r => r.Policies)
                    .FirstOrDefaultAsync(p => p.MacAddress == _macAddress);

                if (pc?.Room?.Policies == null)
                {
                    Log.Debug("No policies found for PC {MacAddress}", _macAddress);
                    return false;
                }

                var wallpaperPolicy = pc.Room.Policies
                    .FirstOrDefault(p => p.IsActive && p.ResetWallpaperOnStartup && !string.IsNullOrEmpty(p.WallpaperPath));

                if (wallpaperPolicy == null)
                {
                    Log.Debug("No active wallpaper policy found for PC {MacAddress}", _macAddress);
                    return false;
                }

                return await ApplyWallpaperAsync(wallpaperPolicy.WallpaperPath!);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to enforce wallpaper policy for PC {MacAddress}", _macAddress);
                return false;
            }
        }

        private async Task<bool> ApplyWallpaperAsync(string wallpaperPath)
        {
            try
            {
                var cachedWallpaperPath = Path.Combine(_wallpaperCachePath, "active_wallpaper.jpg");
                var isHttpSource = IsHttpUrl(wallpaperPath);

                if (isHttpSource)
                {
                    // Download wallpaper from HTTP source
                    using var request = new HttpRequestMessage(HttpMethod.Get, wallpaperPath);
                    if (!string.IsNullOrWhiteSpace(_wallpaperApiToken))
                    {
                        request.Headers.Add("X-IRIS-Wallpaper-Token", _wallpaperApiToken);
                    }

                    using var response = await _httpClient.SendAsync(request);
                    if (!response.IsSuccessStatusCode)
                    {
                        Log.Warning("Failed to download wallpaper from {WallpaperPath}. Status: {StatusCode}", wallpaperPath, response.StatusCode);
                        return false;
                    }

                    var contentBytes = await response.Content.ReadAsByteArrayAsync();
                    if (contentBytes.Length == 0)
                    {
                        Log.Warning("Downloaded wallpaper from {WallpaperPath} is empty", wallpaperPath);
                        return false;
                    }

                    var serverHash = Convert.ToHexString(SHA256.HashData(contentBytes));

                    var needsUpdate = true;
                    if (File.Exists(cachedWallpaperPath))
                    {
                        var cachedHash = await CalculateFileHashAsync(cachedWallpaperPath);
                        needsUpdate = serverHash != cachedHash;
                    }

                    if (needsUpdate)
                    {
                        await File.WriteAllBytesAsync(cachedWallpaperPath, contentBytes);
                        Log.Information("Wallpaper downloaded and cached for PC {MacAddress}", _macAddress);
                    }
                }
                else
                {
                    // Check if wallpaper file exists on server
                    if (!File.Exists(wallpaperPath))
                    {
                        Log.Warning("Wallpaper file not found: {WallpaperPath}", wallpaperPath);
                        return false;
                    }

                    // Calculate hash of server wallpaper
                    var serverHash = await CalculateFileHashAsync(wallpaperPath);

                    // Check if we need to update cached wallpaper
                    var needsUpdate = true;
                    if (File.Exists(cachedWallpaperPath))
                    {
                        var cachedHash = await CalculateFileHashAsync(cachedWallpaperPath);
                        needsUpdate = serverHash != cachedHash;
                    }

                    // Copy wallpaper to cache if needed
                    if (needsUpdate)
                    {
                        File.Copy(wallpaperPath, cachedWallpaperPath, true);
                        Log.Information("Wallpaper updated in cache for PC {MacAddress}", _macAddress);
                    }
                }

                // Apply wallpaper using Windows API
                var result = SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, cachedWallpaperPath, 
                    SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);

                if (result != 0)
                {
                    Log.Information("Wallpaper policy enforced successfully for PC {MacAddress}", _macAddress);
                    return true;
                }
                else
                {
                    Log.Warning("Failed to set wallpaper via Windows API for PC {MacAddress}", _macAddress);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to apply wallpaper for PC {MacAddress}", _macAddress);
                return false;
            }
        }

        private static bool IsHttpUrl(string path)
        {
            if (!Uri.IsWellFormedUriString(path, UriKind.Absolute))
            {
                return false;
            }

            var uri = new Uri(path);
            return uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
                || uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<string> CalculateFileHashAsync(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = await sha256.ComputeHashAsync(stream);
            return Convert.ToHexString(hash);
        }

        public async Task<bool> CheckAndEnforceWallpaperComplianceAsync()
        {
            try
            {
                var pc = await _context.PCs
                    .Include(p => p.Room)
                    .ThenInclude(r => r.Policies)
                    .FirstOrDefaultAsync(p => p.MacAddress == _macAddress);

                if (pc?.Room?.Policies == null)
                    return false;

                var wallpaperPolicy = pc.Room.Policies
                    .FirstOrDefault(p => p.IsActive && p.ResetWallpaperOnStartup && !string.IsNullOrEmpty(p.WallpaperPath));

                if (wallpaperPolicy == null)
                    return false;

                // Check current wallpaper against policy
                var cachedWallpaperPath = Path.Combine(_wallpaperCachePath, "active_wallpaper.jpg");
                if (!File.Exists(cachedWallpaperPath))
                {
                    // No cached wallpaper, enforce policy
                    return await ApplyWallpaperAsync(wallpaperPolicy.WallpaperPath!);
                }

                // Check if current wallpaper matches policy
                var currentWallpaper = GetCurrentWallpaperPath();
                if (currentWallpaper != cachedWallpaperPath)
                {
                    Log.Information("Wallpaper compliance violation detected, re-enforcing policy for PC {MacAddress}", _macAddress);
                    return await ApplyWallpaperAsync(wallpaperPolicy.WallpaperPath!);
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to check wallpaper compliance for PC {MacAddress}", _macAddress);
                return false;
            }
        }

        private string? GetCurrentWallpaperPath()
        {
            try
            {
                // Read current wallpaper from registry
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