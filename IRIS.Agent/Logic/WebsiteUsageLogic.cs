using System.Runtime.Versioning;
using System.Text.Json;
using IRIS.Core.Data;
using IRIS.Core.DTOs;
using IRIS.Core.Services;
using Serilog;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace IRIS.Agent.Logic;

[SupportedOSPlatform("windows")]
public class WebsiteUsageLogic : IDisposable
{
    private readonly IRISDbContext _context;
    private readonly string _macAddress;
    private readonly WebsiteUsageIngestService _ingestService;
    private readonly int _bucketMinutes;
    private readonly string _watermarkFilePath;
    private readonly int _collectIntervalSeconds;
    private readonly int _syncIntervalSeconds;

    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private readonly Dictionary<string, DateTime> _watermarks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(string Browser, string Domain, DateTime Bucket), int> _pending = new();

    private System.Threading.Timer? _collectTimer;
    private System.Threading.Timer? _syncTimer;
    private int? _pcId;

    public WebsiteUsageLogic(
        IRISDbContext context,
        string macAddress,
        int collectIntervalSeconds,
        int syncIntervalSeconds,
        int bucketMinutes)
    {
        _context = context;
        _macAddress = macAddress;
        _ingestService = new WebsiteUsageIngestService(context);
        _bucketMinutes = Math.Max(1, bucketMinutes);

        var stateDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "state");
        Directory.CreateDirectory(stateDirectory);
        _watermarkFilePath = Path.Combine(stateDirectory, "website_usage_watermarks.json");

        _collectIntervalSeconds = Math.Max(15, collectIntervalSeconds);
        _syncIntervalSeconds = Math.Max(15, syncIntervalSeconds);
    }

    public async Task StartMonitoringAsync()
    {
        _pcId = await ResolvePcIdAsync();

        if (_pcId == null)
        {
            Log.Warning("PC not registered yet for MAC {MacAddress}. Website usage monitoring will keep retrying.", _macAddress);
        }

        await LoadWatermarksAsync();

        await CollectUsageAsync();
        await SyncPendingAsync();

        _collectTimer = new System.Threading.Timer(
            _ => _ = RunCollectSafelyAsync(),
            null,
            TimeSpan.FromSeconds(_collectIntervalSeconds),
            TimeSpan.FromSeconds(_collectIntervalSeconds));

        _syncTimer = new System.Threading.Timer(
            _ => _ = RunSyncSafelyAsync(),
            null,
            TimeSpan.FromSeconds(_syncIntervalSeconds),
            TimeSpan.FromSeconds(_syncIntervalSeconds));

        if (_pcId != null)
        {
            Log.Information("Website usage monitoring started for PC {PCId}", _pcId.Value);
        }
    }

    public async Task StopMonitoringAsync()
    {
        _collectTimer?.Dispose();
        _syncTimer?.Dispose();
        await SyncPendingAsync();
        await SaveWatermarksAsync();
        Log.Information("Website usage monitoring stopped");
    }

    private async Task CollectUsageAsync()
    {
        if (!await _syncLock.WaitAsync(0))
        {
            return;
        }

        try
        {
            await EnsurePcIdAsync();
            if (_pcId == null)
            {
                return;
            }

            await CollectChromiumBrowserAsync("Chrome", Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Google", "Chrome", "User Data"));

            await CollectChromiumBrowserAsync("Edge", Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "Edge", "User Data"));

            await CollectFirefoxAsync();
            await SaveWatermarksAsync();
            Log.Information("Website usage collection buffered {Count} bucket(s)", _pending.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Website usage collection cycle failed");
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task CollectChromiumBrowserAsync(string browser, string userDataPath)
    {
        if (!Directory.Exists(userDataPath))
        {
            return;
        }

        foreach (var profileDir in Directory.GetDirectories(userDataPath))
        {
            var profileName = Path.GetFileName(profileDir);
            if (string.IsNullOrWhiteSpace(profileName))
            {
                continue;
            }

            var historyPath = Path.Combine(profileDir, "History");
            if (!File.Exists(historyPath))
            {
                continue;
            }

            var watermarkKey = $"{browser}|{profileDir}";
            var watermark = GetWatermark(watermarkKey);
            var visits = await ReadChromiumVisitsAsync(historyPath, watermark);
            if (visits.Count == 0)
            {
                continue;
            }

            var maxVisitUtc = watermark;
            foreach (var visit in visits)
            {
                var domain = GetDomain(visit.Url);
                if (string.IsNullOrWhiteSpace(domain))
                {
                    continue;
                }

                var bucket = ToBucketStartUtc(visit.VisitedAtUtc);
                var key = (browser, domain, bucket);
                _pending[key] = _pending.TryGetValue(key, out var existing) ? existing + 1 : 1;

                if (visit.VisitedAtUtc > maxVisitUtc)
                {
                    maxVisitUtc = visit.VisitedAtUtc;
                }
            }

            _watermarks[watermarkKey] = maxVisitUtc;
        }
    }

    private async Task CollectFirefoxAsync()
    {
        var profilesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Mozilla", "Firefox", "Profiles");

        if (!Directory.Exists(profilesPath))
        {
            return;
        }

        foreach (var profileDir in Directory.GetDirectories(profilesPath))
        {
            var placesPath = Path.Combine(profileDir, "places.sqlite");
            if (!File.Exists(placesPath))
            {
                continue;
            }

            var watermarkKey = $"Firefox|{profileDir}";
            var watermark = GetWatermark(watermarkKey);
            var visits = await ReadFirefoxVisitsAsync(placesPath, watermark);
            if (visits.Count == 0)
            {
                continue;
            }

            var maxVisitUtc = watermark;
            foreach (var visit in visits)
            {
                var domain = GetDomain(visit.Url);
                if (string.IsNullOrWhiteSpace(domain))
                {
                    continue;
                }

                var bucket = ToBucketStartUtc(visit.VisitedAtUtc);
                var key = ("Firefox", domain, bucket);
                _pending[key] = _pending.TryGetValue(key, out var existing) ? existing + 1 : 1;

                if (visit.VisitedAtUtc > maxVisitUtc)
                {
                    maxVisitUtc = visit.VisitedAtUtc;
                }
            }

            _watermarks[watermarkKey] = maxVisitUtc;
        }
    }

    private async Task SyncPendingAsync()
    {
        if (!await _syncLock.WaitAsync(0))
        {
            return;
        }

        List<WebsiteUsageIngestDto> payload;

        try
        {
            await EnsurePcIdAsync();
            if (_pcId == null)
            {
                return;
            }

            if (_pending.Count == 0)
            {
                return;
            }

            payload = _pending.Select(item => new WebsiteUsageIngestDto
            {
                PCId = _pcId.Value,
                Browser = item.Key.Browser,
                Domain = item.Key.Domain,
                VisitedAt = item.Key.Bucket,
                VisitCount = item.Value
            }).ToList();

            _pending.Clear();
        }
        finally
        {
            _syncLock.Release();
        }

        try
        {
            await _ingestService.UpsertBatchAsync(payload);
            Log.Information("Synced {Count} website usage bucket(s)", payload.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to sync website usage buckets, restoring pending buffer");

            await _syncLock.WaitAsync();
            try
            {
                foreach (var item in payload)
                {
                    var key = (item.Browser, item.Domain, item.VisitedAt);
                    _pending[key] = _pending.TryGetValue(key, out var existing) ? existing + item.VisitCount : item.VisitCount;
                }
            }
            finally
            {
                _syncLock.Release();
            }
        }
    }

    private async Task<List<VisitRow>> ReadChromiumVisitsAsync(string historyPath, DateTime watermarkUtc)
    {
        var tempPath = CopyDatabaseToTemp(historyPath);

        try
        {
            await using var connection = new SqliteConnection($"Data Source={tempPath};Mode=ReadOnly;");
            await connection.OpenAsync();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT u.url, v.visit_time
                FROM visits v
                INNER JOIN urls u ON u.id = v.url
                WHERE v.visit_time > $watermark
                ORDER BY v.visit_time ASC;";
            cmd.Parameters.AddWithValue("$watermark", ToChromiumMicroseconds(watermarkUtc));

            var visits = new List<VisitRow>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var url = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                if (string.IsNullOrWhiteSpace(url))
                {
                    continue;
                }

                var chromiumMicros = reader.GetInt64(1);
                visits.Add(new VisitRow(url, FromChromiumMicroseconds(chromiumMicros)));
            }

            return visits;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to read Chromium history from {HistoryPath}", historyPath);
            return new List<VisitRow>();
        }
        finally
        {
            TryDeleteTemp(tempPath);
        }
    }

    private async Task<List<VisitRow>> ReadFirefoxVisitsAsync(string placesPath, DateTime watermarkUtc)
    {
        var tempPath = CopyDatabaseToTemp(placesPath);

        try
        {
            await using var connection = new SqliteConnection($"Data Source={tempPath};Mode=ReadOnly;");
            await connection.OpenAsync();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT p.url, v.visit_date
                FROM moz_historyvisits v
                INNER JOIN moz_places p ON p.id = v.place_id
                WHERE v.visit_date > $watermark
                ORDER BY v.visit_date ASC;";
            cmd.Parameters.AddWithValue("$watermark", ToUnixMicroseconds(watermarkUtc));

            var visits = new List<VisitRow>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var url = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                if (string.IsNullOrWhiteSpace(url))
                {
                    continue;
                }

                var unixMicros = reader.GetInt64(1);
                visits.Add(new VisitRow(url, FromUnixMicroseconds(unixMicros)));
            }

            return visits;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to read Firefox history from {PlacesPath}", placesPath);
            return new List<VisitRow>();
        }
        finally
        {
            TryDeleteTemp(tempPath);
        }
    }

    private DateTime GetWatermark(string key)
    {
        return _watermarks.TryGetValue(key, out var existing)
            ? DateTime.SpecifyKind(existing, DateTimeKind.Utc)
            : DateTime.UtcNow.AddHours(-2);
    }

    private async Task LoadWatermarksAsync()
    {
        if (!File.Exists(_watermarkFilePath))
        {
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_watermarkFilePath);
            var parsed = JsonSerializer.Deserialize<Dictionary<string, DateTime>>(json);
            if (parsed == null)
            {
                return;
            }

            _watermarks.Clear();
            foreach (var item in parsed)
            {
                _watermarks[item.Key] = DateTime.SpecifyKind(item.Value, DateTimeKind.Utc);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Unable to load website usage watermarks, starting fresh");
        }
    }

    private async Task SaveWatermarksAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_watermarks);
            await File.WriteAllTextAsync(_watermarkFilePath, json);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Unable to persist website usage watermarks");
        }
    }

    private DateTime ToBucketStartUtc(DateTime timestampUtc)
    {
        var utc = timestampUtc.Kind == DateTimeKind.Utc ? timestampUtc : timestampUtc.ToUniversalTime();
        var bucketMinute = (utc.Minute / _bucketMinutes) * _bucketMinutes;

        return new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, bucketMinute, 0, DateTimeKind.Utc);
    }

    private static string? GetDomain(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var host = uri.Host.Trim().ToLowerInvariant();
        if (host.StartsWith("www."))
        {
            host = host[4..];
        }

        return string.IsNullOrWhiteSpace(host) ? null : host;
    }

    private static string CopyDatabaseToTemp(string originalPath)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"iris-web-{Guid.NewGuid():N}.db");
        File.Copy(originalPath, tempPath, true);
        return tempPath;
    }

    private static void TryDeleteTemp(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch
        {
            // Ignore temp cleanup errors.
        }
    }

    private static long ToChromiumMicroseconds(DateTime utc)
    {
        var epoch = new DateTime(1601, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var value = utc.Kind == DateTimeKind.Utc ? utc : utc.ToUniversalTime();
        return (long)(value - epoch).TotalMilliseconds * 1000;
    }

    private static DateTime FromChromiumMicroseconds(long microseconds)
    {
        var epoch = new DateTime(1601, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return epoch.AddMilliseconds(microseconds / 1000.0);
    }

    private static long ToUnixMicroseconds(DateTime utc)
    {
        var value = utc.Kind == DateTimeKind.Utc ? utc : utc.ToUniversalTime();
        return new DateTimeOffset(value).ToUnixTimeMilliseconds() * 1000;
    }

    private static DateTime FromUnixMicroseconds(long microseconds)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(microseconds / 1000).UtcDateTime;
    }

    public void Dispose()
    {
        _collectTimer?.Dispose();
        _syncTimer?.Dispose();
        _syncLock.Dispose();
    }

    private async Task EnsurePcIdAsync()
    {
        if (_pcId != null)
        {
            return;
        }

        _pcId = await ResolvePcIdAsync();
    }

    private Task<int?> ResolvePcIdAsync()
    {
        return _context.PCs
            .Where(p => p.MacAddress == _macAddress)
            .Select(p => (int?)p.Id)
            .FirstOrDefaultAsync();
    }

    private async Task RunCollectSafelyAsync()
    {
        try
        {
            await CollectUsageAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unhandled exception in website usage collect timer");
        }
    }

    private async Task RunSyncSafelyAsync()
    {
        try
        {
            await SyncPendingAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unhandled exception in website usage sync timer");
        }
    }

    private sealed record VisitRow(string Url, DateTime VisitedAtUtc);
}
