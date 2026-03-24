using System.ComponentModel.DataAnnotations;

namespace IRIS.Core.Models
{
    /// <summary>
    /// Stores key-value system settings persisted in the database.
    /// Used for data retention policies and other admin-configurable options.
    /// </summary>
    public class SystemSettings
    {
        [Key]
        [MaxLength(100)]
        public string Key { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string Value { get; set; } = string.Empty;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Well-known setting keys.</summary>
    public static class SettingsKeys
    {
        /// <summary>Days to retain hardware metrics (default: 30).</summary>
        public const string HardwareMetricRetentionDays = "DataRetention.HardwareMetricDays";

        /// <summary>Days to retain network metrics (default: 30).</summary>
        public const string NetworkMetricRetentionDays = "DataRetention.NetworkMetricDays";

        /// <summary>Days to retain alerts (default: 90). All alerts older than this are deleted.</summary>
        public const string AlertRetentionDays = "DataRetention.ResolvedAlertDays";

        /// <summary>Days to retain website usage history (default: 60).</summary>
        public const string WebsiteUsageRetentionDays = "DataRetention.WebsiteUsageDays";

        /// <summary>Days to retain software/application usage history (default: 60).</summary>
        public const string SoftwareUsageRetentionDays = "DataRetention.SoftwareUsageDays";

        /// <summary>Hour of day (0-23 UTC) when the cleanup job runs (default: 2).</summary>
        public const string CleanupHourUtc = "DataRetention.CleanupHourUtc";
    }
}
