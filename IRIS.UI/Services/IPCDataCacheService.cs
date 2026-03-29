using IRIS.Core.DTOs;
using IRIS.Core.Services.ServiceModels;

namespace IRIS.UI.Services
{
    /// <summary>
    /// Singleton cache that holds PC data shared between Dashboard and Monitor pages.
    /// Uses its own DI scopes for DB access, so data survives page navigation and
    /// avoids the concurrent/disposed DbContext issues that cause Npgsql errors.
    /// </summary>
    public interface IPCDataCacheService
    {
        // Cached data (read-only snapshots)
        IReadOnlyList<PCMonitorInfo> CachedPCs { get; }
        PCStatusCounts CachedStatusCounts { get; }
        DashboardSummary? CachedDashboardSummary { get; }
        IReadOnlyList<RoomDto> CachedRooms { get; }
        IReadOnlyList<LiveAlertItem> CachedLiveAlerts { get; }

        DateTime LastPCRefreshUtc { get; }
        DateTime LastDashboardRefreshUtc { get; }
        bool HasData { get; }

        /// <summary>Current room filter applied to data refreshes. null = all rooms.</summary>
        int? CurrentRoomFilter { get; set; }

        /// <summary>Refresh PC list + status counts. Creates its own scope.</summary>
        Task RefreshPCDataAsync();

        /// <summary>Refresh dashboard summary (KPIs, labs, heavy apps). Creates its own scope.</summary>
        Task RefreshDashboardSummaryAsync();

        /// <summary>Refresh live alerts. Creates its own scope.</summary>
        Task RefreshLiveAlertsAsync(bool forceWait = false);

        /// <summary>Refresh rooms list. Creates its own scope.</summary>
        Task RefreshRoomsAsync();

        /// <summary>Gets cached freeze state for a PC, if known.</summary>
        bool? GetFreezeState(int pcId);

        /// <summary>Sets cached freeze state for a PC so UI pages stay in sync.</summary>
        void SetFreezeState(int pcId, bool isFrozen);

        /// <summary>Fired on the calling thread after any data changes.</summary>
        event Action? DataChanged;
    }
}
