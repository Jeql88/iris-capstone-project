namespace IRIS.Core.Services.Contracts
{
    /// <summary>
    /// Manages data retention settings and performs cleanup of old metrics/alerts.
    /// </summary>
    public interface IDataRetentionService
    {
        /// <summary>Gets the current retention setting value (in days) for the given key.</summary>
        Task<int> GetRetentionDaysAsync(string settingsKey);

        /// <summary>Gets the UTC hour (0-23) when the daily cleanup job should run.</summary>
        Task<int> GetCleanupHourAsync();

        /// <summary>Updates a retention setting.</summary>
        Task UpdateSettingAsync(string settingsKey, int value);

        /// <summary>
        /// Purges hardware metrics, network metrics, and old alerts
        /// older than their configured retention period. Returns total rows deleted.
        /// </summary>
        Task<DataRetentionResult> PurgeOldDataAsync();
    }

    public class DataRetentionResult
    {
        public int HardwareMetricsDeleted { get; set; }
        public int NetworkMetricsDeleted { get; set; }
        public int AlertsDeleted { get; set; }
        public int WebsiteUsageDeleted { get; set; }
        public int SoftwareUsageDeleted { get; set; }
        public int PendingCommandsDeleted { get; set; }
        public int TotalDeleted => HardwareMetricsDeleted + NetworkMetricsDeleted + AlertsDeleted + WebsiteUsageDeleted + SoftwareUsageDeleted + PendingCommandsDeleted;
    }
}
