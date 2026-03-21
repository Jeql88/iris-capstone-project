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
        private readonly Dictionary<int, bool> _freezeStatesByPcId = new();
        private readonly object _freezeStateLock = new();

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

        public async Task RefreshPCDataAsync()
        {
            if (!await _pcRefreshSemaphore.WaitAsync(0))
                return;

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

        public async Task RefreshDashboardSummaryAsync()
        {
            if (!await _dashboardRefreshSemaphore.WaitAsync(0))
                return;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IMonitoringService>();

                _cachedDashboardSummary = await service.GetDashboardSummaryAsync(CurrentRoomFilter);
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

        public async Task RefreshLiveAlertsAsync()
        {
            if (!await _alertRefreshSemaphore.WaitAsync(0))
                return;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IMonitoringService>();

                _cachedLiveAlerts = await service.GetLiveAlertsAsync(CurrentRoomFilter, 30);

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
    }
}
