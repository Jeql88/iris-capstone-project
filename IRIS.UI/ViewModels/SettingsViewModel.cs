using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using IRIS.Core.Models;
using IRIS.Core.Services.Contracts;
using IRIS.UI.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace IRIS.UI.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private readonly IAuthenticationService _authService;
        private readonly IServiceScopeFactory _scopeFactory;
        private string _currentPassword = string.Empty;
        private string _newPassword = string.Empty;
        private string _confirmPassword = string.Empty;

        // Data retention properties
        private double _hardwareRetentionDays = 30;
        private double _networkRetentionDays = 30;
        private double _alertRetentionDays = 90;
        private double _websiteUsageRetentionDays = 60;
        private double _softwareUsageRetentionDays = 60;
        private double _cleanupHourUtc = 2;
        private bool _isRetentionLoading;
        private string _retentionStatusMessage = string.Empty;
        private bool _isRetentionStatusError;
        private string _changePasswordStatusMessage = string.Empty;

        public SettingsViewModel(IAuthenticationService authService, IServiceScopeFactory scopeFactory)
        {
            _authService = authService;
            _scopeFactory = scopeFactory;
            ChangePasswordCommand = new RelayCommand(async () => await ChangePasswordAsync(), () => true);
            _saveRetentionCommand = new RelayCommand(async () => await SaveRetentionSettingsAsync(), () => !_isRetentionLoading);
            _runCleanupNowCommand = new RelayCommand(async () => await RunCleanupNowAsync(), () => !_isRetentionLoading);

            _ = LoadRetentionSettingsAsync();
        }

        // --- User Profile ---
        public string? Username => _authService.GetCurrentUser()?.Username;
        public string? FullName => _authService.GetCurrentUser()?.FullName;
        public string? Role => _authService.GetCurrentUser()?.Role.ToString()
            .Replace("SystemAdministrator", "System Administrator")
            .Replace("ITPersonnel", "IT Personnel");

        // --- Password ---
        public string CurrentPassword
        {
            get => _currentPassword;
            set { _currentPassword = value; OnPropertyChanged(); }
        }

        public string NewPassword
        {
            get => _newPassword;
            set { _newPassword = value; OnPropertyChanged(); }
        }

        public string ConfirmPassword
        {
            get => _confirmPassword;
            set { _confirmPassword = value; OnPropertyChanged(); }
        }

        public ICommand ChangePasswordCommand { get; }

        // --- Data Retention ---
        public double HardwareRetentionDays
        {
            get => _hardwareRetentionDays;
            set { _hardwareRetentionDays = value; OnPropertyChanged(); }
        }

        public double NetworkRetentionDays
        {
            get => _networkRetentionDays;
            set { _networkRetentionDays = value; OnPropertyChanged(); }
        }

        public double AlertRetentionDays
        {
            get => _alertRetentionDays;
            set { _alertRetentionDays = value; OnPropertyChanged(); }
        }

        public double WebsiteUsageRetentionDays
        {
            get => _websiteUsageRetentionDays;
            set { _websiteUsageRetentionDays = value; OnPropertyChanged(); }
        }

        public double SoftwareUsageRetentionDays
        {
            get => _softwareUsageRetentionDays;
            set { _softwareUsageRetentionDays = value; OnPropertyChanged(); }
        }

        public double CleanupHourUtc
        {
            get => _cleanupHourUtc;
            set { _cleanupHourUtc = value; OnPropertyChanged(); }
        }

        public bool IsRetentionLoading
        {
            get => _isRetentionLoading;
            set
            {
                _isRetentionLoading = value;
                OnPropertyChanged();
                _saveRetentionCommand.RaiseCanExecuteChanged();
                _runCleanupNowCommand.RaiseCanExecuteChanged();
            }
        }

        public string RetentionStatusMessage
        {
            get => _retentionStatusMessage;
            set { _retentionStatusMessage = value; OnPropertyChanged(); }
        }

        public bool IsRetentionStatusError
        {
            get => _isRetentionStatusError;
            set { _isRetentionStatusError = value; OnPropertyChanged(); }
        }

        public string ChangePasswordStatusMessage
        {
            get => _changePasswordStatusMessage;
            set { _changePasswordStatusMessage = value; OnPropertyChanged(); }
        }

        private readonly RelayCommand _saveRetentionCommand;
        private readonly RelayCommand _runCleanupNowCommand;
        public ICommand SaveRetentionCommand => _saveRetentionCommand;
        public ICommand RunCleanupNowCommand => _runCleanupNowCommand;

        // --- Password Logic ---
        private async Task ChangePasswordAsync()
        {
            if (string.IsNullOrWhiteSpace(CurrentPassword))
            {
                MessageBox.Show("Current password is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(NewPassword))
            {
                MessageBox.Show("New password is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (NewPassword.Length < 8)
            {
                MessageBox.Show("New password must be at least 8 characters long.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (NewPassword != ConfirmPassword)
            {
                MessageBox.Show("New password and confirmation do not match.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var currentUser = _authService.GetCurrentUser();
            if (currentUser == null)
            {
                MessageBox.Show("No user is currently logged in.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var success = await _authService.ChangePasswordAsync(currentUser.Id, CurrentPassword, NewPassword);

            if (success)
            {
                MessageBox.Show("Password changed successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                CurrentPassword = string.Empty;
                NewPassword = string.Empty;
                ConfirmPassword = string.Empty;
            }
            else
            {
                MessageBox.Show("Failed to change password. Please check your current password.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // --- Data Retention Logic ---
        private async Task LoadRetentionSettingsAsync()
        {
            try
            {
                IsRetentionLoading = true;
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IDataRetentionService>();

                HardwareRetentionDays = await service.GetRetentionDaysAsync(SettingsKeys.HardwareMetricRetentionDays);
                NetworkRetentionDays = await service.GetRetentionDaysAsync(SettingsKeys.NetworkMetricRetentionDays);
                AlertRetentionDays = await service.GetRetentionDaysAsync(SettingsKeys.AlertRetentionDays);
                WebsiteUsageRetentionDays = await service.GetRetentionDaysAsync(SettingsKeys.WebsiteUsageRetentionDays);
                SoftwareUsageRetentionDays = await service.GetRetentionDaysAsync(SettingsKeys.SoftwareUsageRetentionDays);
                CleanupHourUtc = await service.GetCleanupHourAsync();
            }
            catch
            {
                // Use defaults if DB isn't reachable yet
            }
            finally
            {
                IsRetentionLoading = false;
            }
        }

        public async Task<bool> SaveRetentionSettingsAsync()
        {
            var hwDays = (int)HardwareRetentionDays;
            var netDays = (int)NetworkRetentionDays;
            var alertDays = (int)AlertRetentionDays;
            var webDays = (int)WebsiteUsageRetentionDays;
            var swDays = (int)SoftwareUsageRetentionDays;
            var cleanupHour = (int)CleanupHourUtc;

            if (hwDays < 1 || netDays < 1 || alertDays < 1)
            {
                SetRetentionStatus("Retention days must be at least 1.", true);
                return false;
            }

            if (cleanupHour < 0 || cleanupHour > 23)
            {
                SetRetentionStatus("Cleanup hour must be between 0 and 23.", true);
                return false;
            }

            try
            {
                IsRetentionLoading = true;
                SetRetentionStatus("Saving...", false);

                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IDataRetentionService>();

                await service.UpdateSettingAsync(SettingsKeys.HardwareMetricRetentionDays, hwDays);
                await service.UpdateSettingAsync(SettingsKeys.NetworkMetricRetentionDays, netDays);
                await service.UpdateSettingAsync(SettingsKeys.AlertRetentionDays, alertDays);
                await service.UpdateSettingAsync(SettingsKeys.WebsiteUsageRetentionDays, webDays);
                await service.UpdateSettingAsync(SettingsKeys.SoftwareUsageRetentionDays, swDays);
                await service.UpdateSettingAsync(SettingsKeys.CleanupHourUtc, cleanupHour);

                SetRetentionStatus(string.Empty, false);
                return true;
            }
            catch (Exception ex)
            {
                SetRetentionStatus($"Error: {ex.Message}", true);
                return false;
            }
            finally
            {
                IsRetentionLoading = false;
            }
        }

        private async Task RunCleanupNowAsync()
        {
            try
            {
                IsRetentionLoading = true;
                SetRetentionStatus("Running cleanup...", false);

                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IDataRetentionService>();

                var result = await service.PurgeOldDataAsync();

                if (result.TotalDeleted == 0)
                {
                    SetRetentionStatus("Cleanup completed - no stale data found.", false);
                }
                else
                {
                    var parts = new List<string>();
                    if (result.HardwareMetricsDeleted > 0) parts.Add($"{result.HardwareMetricsDeleted} hardware");
                    if (result.NetworkMetricsDeleted > 0) parts.Add($"{result.NetworkMetricsDeleted} network");
                    if (result.AlertsDeleted > 0) parts.Add($"{result.AlertsDeleted} alert");
                    if (result.WebsiteUsageDeleted > 0) parts.Add($"{result.WebsiteUsageDeleted} website usage");
                    if (result.SoftwareUsageDeleted > 0) parts.Add($"{result.SoftwareUsageDeleted} app usage");
                    SetRetentionStatus($"Cleanup completed - deleted {string.Join(", ", parts)} records.", false);
                }
            }
            catch (Exception ex)
            {
                SetRetentionStatus($"Cleanup failed: {ex.Message}", true);
            }
            finally
            {
                IsRetentionLoading = false;
            }
        }

        public void SetRetentionStatus(string message, bool isError)
        {
            RetentionStatusMessage = message;
            IsRetentionStatusError = isError;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
