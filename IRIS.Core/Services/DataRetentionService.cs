using Microsoft.EntityFrameworkCore;
using IRIS.Core.Data;
using IRIS.Core.Models;
using IRIS.Core.Services.Contracts;

namespace IRIS.Core.Services
{
    public class DataRetentionService : IDataRetentionService
    {
        private readonly IRISDbContext _context;

        // Defaults if the DB row is missing or unparseable
        private const int DefaultHardwareDays = 30;
        private const int DefaultNetworkDays = 30;
        private const int DefaultAlertDays = 90;
        private const int DefaultWebsiteUsageDays = 60;
        private const int DefaultSoftwareUsageDays = 60;
        private const int DefaultCleanupHour = 2;

        // Delete in batches to avoid long-running transactions
        private const int BatchSize = 5000;

        public DataRetentionService(IRISDbContext context)
        {
            _context = context;
        }

        public async Task<int> GetRetentionDaysAsync(string settingsKey)
        {
            var setting = await _context.SystemSettings.FindAsync(settingsKey);
            if (setting != null && int.TryParse(setting.Value, out var days) && days > 0)
            {
                return days;
            }

            return settingsKey switch
            {
                SettingsKeys.HardwareMetricRetentionDays => DefaultHardwareDays,
                SettingsKeys.NetworkMetricRetentionDays => DefaultNetworkDays,
                SettingsKeys.AlertRetentionDays => DefaultAlertDays,
                SettingsKeys.WebsiteUsageRetentionDays => DefaultWebsiteUsageDays,
                SettingsKeys.SoftwareUsageRetentionDays => DefaultSoftwareUsageDays,
                _ => 30
            };
        }

        public async Task<int> GetCleanupHourAsync()
        {
            var setting = await _context.SystemSettings.FindAsync(SettingsKeys.CleanupHourUtc);
            if (setting != null && int.TryParse(setting.Value, out var hour) && hour >= 0 && hour <= 23)
            {
                return hour;
            }
            return DefaultCleanupHour;
        }

        public async Task UpdateSettingAsync(string settingsKey, int value)
        {
            var setting = await _context.SystemSettings.FindAsync(settingsKey);
            if (setting != null)
            {
                setting.Value = value.ToString();
                setting.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.SystemSettings.Add(new SystemSettings
                {
                    Key = settingsKey,
                    Value = value.ToString(),
                    UpdatedAt = DateTime.UtcNow
                });
            }
            await _context.SaveChangesAsync();
        }

        public async Task<DataRetentionResult> PurgeOldDataAsync()
        {
            var result = new DataRetentionResult();

            var hwDays = await GetRetentionDaysAsync(SettingsKeys.HardwareMetricRetentionDays);
            var netDays = await GetRetentionDaysAsync(SettingsKeys.NetworkMetricRetentionDays);
            var alertDays = await GetRetentionDaysAsync(SettingsKeys.AlertRetentionDays);
            var webDays = await GetRetentionDaysAsync(SettingsKeys.WebsiteUsageRetentionDays);
            var swDays = await GetRetentionDaysAsync(SettingsKeys.SoftwareUsageRetentionDays);

            var hwCutoff = DateTime.UtcNow.AddDays(-hwDays);
            var netCutoff = DateTime.UtcNow.AddDays(-netDays);
            var alertCutoff = DateTime.UtcNow.AddDays(-alertDays);
            var webCutoff = DateTime.UtcNow.AddDays(-webDays);
            var swCutoff = DateTime.UtcNow.AddDays(-swDays);

            // Purge hardware metrics in batches
            result.HardwareMetricsDeleted = await DeleteInBatchesAsync(
                () => _context.HardwareMetrics
                    .Where(m => m.Timestamp < hwCutoff)
                    .OrderBy(m => m.Timestamp)
                    .Take(BatchSize));

            // Purge network metrics in batches
            result.NetworkMetricsDeleted = await DeleteInBatchesAsync(
                () => _context.NetworkMetrics
                    .Where(m => m.Timestamp < netCutoff)
                    .OrderBy(m => m.Timestamp)
                    .Take(BatchSize));

            // Purge old alerts in batches (all alerts past retention age)
            result.AlertsDeleted = await DeleteInBatchesAsync(
                () => _context.Alerts
                    .Where(a => a.CreatedAt < alertCutoff)
                    .OrderBy(a => a.CreatedAt)
                    .Take(BatchSize));

            // Purge website usage history in batches
            result.WebsiteUsageDeleted = await DeleteInBatchesAsync(
                () => _context.WebsiteUsageHistory
                    .Where(w => w.VisitedAt < webCutoff)
                    .OrderBy(w => w.VisitedAt)
                    .Take(BatchSize));

            // Purge software usage history in batches
            result.SoftwareUsageDeleted = await DeleteInBatchesAsync(
                () => _context.SoftwareUsageHistory
                    .Where(s => s.CreatedAt < swCutoff)
                    .OrderBy(s => s.CreatedAt)
                    .Take(BatchSize));

            return result;
        }

        private async Task<int> DeleteInBatchesAsync<T>(Func<IQueryable<T>> queryFactory) where T : class
        {
            int totalDeleted = 0;
            int deleted;

            do
            {
                var batch = await queryFactory().ToListAsync();
                deleted = batch.Count;

                if (deleted > 0)
                {
                    _context.RemoveRange(batch);
                    await _context.SaveChangesAsync();
                    totalDeleted += deleted;
                }
            }
            while (deleted == BatchSize);

            return totalDeleted;
        }
    }
}
