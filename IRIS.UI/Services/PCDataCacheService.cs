using IRIS.Core.DTOs;
using IRIS.Core.Services.Contracts;
using IRIS.Core.Services.ServiceModels;
using Microsoft.Extensions.DependencyInjection;

namespace IRIS.UI.Services
{
    /// <summary>
    /// Singleton cache that shares PC/dashboard data between Dashboard and Monitor pages.
    /// Each refresh creates its own DI scope → its own DbContext, so:
    ///   - No concurrent access to a single DbContext
    ///   - No disposed-context errors during page navigation
    /// </summary>
    public class PCDataCacheService : IPCDataCacheService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly SemaphoreSlim _pcRefreshSemaphore = new(1, 1);
        private readonly SemaphoreSlim _dashboardRefreshSemaphore = new(1, 1);
        private readonly SemaphoreSlim _alertRefreshSemaphore = new(1, 1);

        private List<PCMonitorInfo> _cachedPCs = new();
        private PCStatusCounts _cachedStatusCounts = new();
        private DashboardSummary? _cachedDashboardSummary;
        private List<RoomDto> _cachedRooms = new();
        private List<LiveAlertItem> _cachedLiveAlerts = new();
        private readonly Dictionary<string, DateTime> _alertLastSeenUtc = new();
        private readonly Dictionary<int, bool> _freezeStatesByPcId = new();
        private readonly Dictionary<int, string> _snapshotsByPcId = new();
        private readonly object _freezeStateLock = new();
        private readonly object _snapshotCacheLock = new();

        public PCDataCacheService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        public IReadOnlyList<PCMonitorInfo> CachedPCs => _cachedPCs;
        public PCStatusCounts CachedStatusCounts => _cachedStatusCounts;
        public DashboardSummary? CachedDashboardSummary => _cachedDashboardSummary;
        public IReadOnlyList<RoomDto> CachedRooms => _cachedRooms;
        public IReadOnlyList<LiveAlertItem> CachedLiveAlerts => _cachedLiveAlerts;

        public DateTime LastPCRefreshUtc { get; private set; } = DateTime.MinValue;
        public DateTime LastDashboardRefreshUtc { get; private set; } = DateTime.MinValue;
        public bool HasData => LastPCRefreshUtc > DateTime.MinValue;

        public int? CurrentRoomFilter { get; set; }

        public event Action? DataChanged;

        public async Task RefreshPCDataAsync(bool forceWait = false)
        {
            if (forceWait)
            {
                await _pcRefreshSemaphore.WaitAsync();
            }
            else if (!await _pcRefreshSemaphore.WaitAsync(0))
            {
                return;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IMonitoringService>();

                var pcs = await service.GetPCsForMonitorAsync(CurrentRoomFilter);
                var counts = await service.GetPCStatusCountsAsync(CurrentRoomFilter);

                _cachedPCs = pcs;
                _cachedStatusCounts = counts;
                LastPCRefreshUtc = DateTime.UtcNow;

                DataChanged?.Invoke();
            }
            catch
            {
                // Non-critical — keep stale data
            }
            finally
            {
                _pcRefreshSemaphore.Release();
            }
        }

        public async Task RefreshDashboardSummaryAsync(DateTime? startUtc = null, DateTime? endUtc = null)
        {
            if (!await _dashboardRefreshSemaphore.WaitAsync(0))
                return;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IMonitoringService>();

                _cachedDashboardSummary = await service.GetDashboardSummaryAsync(CurrentRoomFilter, startUtc, endUtc);
                LastDashboardRefreshUtc = DateTime.UtcNow;

                DataChanged?.Invoke();
            }
            catch
            {
                // Non-critical — keep stale data
            }
            finally
            {
                _dashboardRefreshSemaphore.Release();
            }
        }

        public async Task RefreshLiveAlertsAsync(bool forceWait = false)
        {
            if (forceWait)
            {
                await _alertRefreshSemaphore.WaitAsync();
            }
            else if (!await _alertRefreshSemaphore.WaitAsync(0))
            {
                return;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IMonitoringService>();

                var allAlerts = await service.GetLiveAlertsAsync(CurrentRoomFilter, 30);
                // Filter to only Critical and High severity
                var freshAlerts = allAlerts
                    .Where(a => a.Severity == "Critical" || a.Severity == "High")
                    .ToList();

                // Track when each alert was last seen in the live set.
                // Carry forward alerts for a grace period (60s) so they don't
                // flicker on/off when metrics oscillate near the threshold.
                var nowUtc = DateTime.UtcNow;
                var freshKeys = new HashSet<string>(freshAlerts.Select(a => a.AlertKey));
                foreach (var key in freshKeys)
                    _alertLastSeenUtc[key] = nowUtc;

                // Carry forward cached alerts whose key is still within the grace period
                const int graceSeconds = 60;
                var carriedForward = _cachedLiveAlerts
                    .Where(a => !freshKeys.Contains(a.AlertKey)
                                && _alertLastSeenUtc.TryGetValue(a.AlertKey, out var lastSeen)
                                && (nowUtc - lastSeen).TotalSeconds < graceSeconds)
                    .ToList();

                _cachedLiveAlerts = freshAlerts.Concat(carriedForward)
                    .OrderByDescending(a => a.SeverityRank)
                    .ThenByDescending(a => a.Timestamp)
                    .ToList();

                // Prune stale tracking entries
                var staleKeys = _alertLastSeenUtc
                    .Where(kv => (nowUtc - kv.Value).TotalSeconds > graceSeconds * 2)
                    .Select(kv => kv.Key)
                    .ToList();
                foreach (var key in staleKeys)
                    _alertLastSeenUtc.Remove(key);

                DataChanged?.Invoke();
            }
            catch
            {
                // Non-critical — keep stale data
            }
            finally
            {
                _alertRefreshSemaphore.Release();
            }
        }

        public async Task RefreshRoomsAsync()
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IMonitoringService>();

                _cachedRooms = await service.GetRoomsAsync();

                DataChanged?.Invoke();
            }
            catch
            {
                // Non-critical — keep stale data
            }
        }

        public bool? GetFreezeState(int pcId)
        {
            lock (_freezeStateLock)
            {
                return _freezeStatesByPcId.TryGetValue(pcId, out var isFrozen)
                    ? isFrozen
                    : null;
            }
        }

        public void SetFreezeState(int pcId, bool isFrozen)
        {
            lock (_freezeStateLock)
            {
                _freezeStatesByPcId[pcId] = isFrozen;
            }

            DataChanged?.Invoke();
        }

        public string? GetCachedSnapshot(int pcId)
        {
            lock (_snapshotCacheLock)
            {
                return _snapshotsByPcId.TryGetValue(pcId, out var snapshot)
                    ? snapshot
                    : null;
            }
        }

        public void SetCachedSnapshot(int pcId, string snapshotBase64)
        {
            if (string.IsNullOrWhiteSpace(snapshotBase64))
            {
                return;
            }

            lock (_snapshotCacheLock)
            {
                _snapshotsByPcId[pcId] = snapshotBase64;
            }
        }
    }
}
