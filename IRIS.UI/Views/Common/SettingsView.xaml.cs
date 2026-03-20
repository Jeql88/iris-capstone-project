using System.Windows;
using System.Windows.Controls;
using IRIS.Core.Services.Contracts;
using IRIS.UI.Services;
using IRIS.UI.ViewModels;
using IRIS.UI.Views.Shared;

namespace IRIS.UI.Views.Common
{
    public partial class SettingsView : UserControl
    {
        private readonly SettingsViewModel _viewModel;
        private readonly IAuthenticationService _authService;

        public SettingsView(SettingsViewModel viewModel, IAuthenticationService authService)
        {
            InitializeComponent();
            DataContext = viewModel;
            _viewModel = viewModel;
            _authService = authService;
        }

        private async void ChangePassword_Click(object sender, RoutedEventArgs e)
        {
            var currentPassword = CurrentPasswordBox.Password;
            var newPassword = NewPasswordBox.Password;
            var confirmPassword = ConfirmPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(currentPassword))
            {
                MessageBox.Show("Current password is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(newPassword))
            {
                MessageBox.Show("New password is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (newPassword.Length < 8)
            {
                MessageBox.Show("New password must be at least 8 characters long.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (newPassword != confirmPassword)
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

            var success = await _authService.ChangePasswordAsync(currentUser.Id, currentPassword, newPassword);

            if (success)
            {
                MessageBox.Show("Password changed successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                CurrentPasswordBox.Clear();
                NewPasswordBox.Clear();
                ConfirmPasswordBox.Clear();
            }
            else
            {
                MessageBox.Show("Failed to change password. Please check your current password.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Logout_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to logout?", "Confirm Logout", 
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                await _authService.LogoutAsync();
                
                var serviceProvider = ((App)Application.Current).GetServiceProvider();
                var authService = (IAuthenticationService)serviceProvider.GetService(typeof(IAuthenticationService))!;
                var loginWindow = new LoginWindow(authService);
                loginWindow.Show();
                
                Window.GetWindow(this)?.Close();
            }
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            // Read values directly from NumberBox controls to bypass any binding issues
            _viewModel.HardwareRetentionDays = HardwareRetentionBox.Value ?? _viewModel.HardwareRetentionDays;
            _viewModel.NetworkRetentionDays = NetworkRetentionBox.Value ?? _viewModel.NetworkRetentionDays;
            _viewModel.AlertRetentionDays = AlertRetentionBox.Value ?? _viewModel.AlertRetentionDays;
            _viewModel.WebsiteUsageRetentionDays = WebsiteRetentionBox.Value ?? _viewModel.WebsiteUsageRetentionDays;
            _viewModel.SoftwareUsageRetentionDays = SoftwareRetentionBox.Value ?? _viewModel.SoftwareUsageRetentionDays;
            _viewModel.CleanupHourUtc = CleanupHourBox.Value ?? _viewModel.CleanupHourUtc;

            if (_viewModel.SaveRetentionCommand.CanExecute(null))
                _viewModel.SaveRetentionCommand.Execute(null);
        }

        private void RunCleanupNow_Click(object sender, RoutedEventArgs e)
        {
            // Read values directly from NumberBox controls first
            _viewModel.HardwareRetentionDays = HardwareRetentionBox.Value ?? _viewModel.HardwareRetentionDays;
            _viewModel.NetworkRetentionDays = NetworkRetentionBox.Value ?? _viewModel.NetworkRetentionDays;
            _viewModel.AlertRetentionDays = AlertRetentionBox.Value ?? _viewModel.AlertRetentionDays;
            _viewModel.WebsiteUsageRetentionDays = WebsiteRetentionBox.Value ?? _viewModel.WebsiteUsageRetentionDays;
            _viewModel.SoftwareUsageRetentionDays = SoftwareRetentionBox.Value ?? _viewModel.SoftwareUsageRetentionDays;
            _viewModel.CleanupHourUtc = CleanupHourBox.Value ?? _viewModel.CleanupHourUtc;

            if (_viewModel.RunCleanupNowCommand.CanExecute(null))
                _viewModel.RunCleanupNowCommand.Execute(null);
        }
    }
}
