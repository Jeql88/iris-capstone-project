using System.Windows;
using System.Windows.Controls;
using IRIS.Core.Models;
using IRIS.Core.Services.Contracts;
using IRIS.UI.Services;
using IRIS.UI.Views.Dialogs;
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

            var currentUser = _authService.GetCurrentUser();
            if (currentUser?.Role == UserRole.Faculty)
            {
                DataRetentionHeader.Visibility = Visibility.Collapsed;
                DataRetentionCard.Visibility = Visibility.Collapsed;
            }
        }

        private async void ChangePassword_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ChangePasswordStatusMessage = string.Empty;

            var currentPassword = CurrentPasswordBox.Password;
            var newPassword = NewPasswordBox.Password;
            var confirmPassword = ConfirmPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(currentPassword) ||
                string.IsNullOrWhiteSpace(newPassword) ||
                string.IsNullOrWhiteSpace(confirmPassword))
            {
                _viewModel.ChangePasswordStatusMessage = "Please fill in all fields.";
                return;
            }

            if (newPassword.Length < 8)
            {
                _viewModel.ChangePasswordStatusMessage = "New password must be at least 8 characters long.";
                return;
            }

            if (newPassword != confirmPassword)
            {
                _viewModel.ChangePasswordStatusMessage = "New password and confirmation do not match.";
                return;
            }

            var currentUser = _authService.GetCurrentUser();
            if (currentUser == null)
            {
                _viewModel.ChangePasswordStatusMessage = "No user is currently logged in.";
                return;
            }

            var confirmDialog = new ConfirmationDialog(
                "Confirm Password Change",
                "Are you sure you want to change your password?",
                "Warning24",
                "Yes",
                "No");
            confirmDialog.Owner = Application.Current.MainWindow;

            if (confirmDialog.ShowDialog() != true)
            {
                return;
            }

            var success = await _authService.ChangePasswordAsync(currentUser.Id, currentPassword, newPassword);

            if (success)
            {
                var successDialog = new ConfirmationDialog(
                    "Success",
                    "Password changed successfully!",
                    "Checkmark24",
                    "Close",
                    "Cancel",
                    false);
                successDialog.Owner = Application.Current.MainWindow;
                successDialog.ShowDialog();

                _viewModel.ChangePasswordStatusMessage = string.Empty;
                ClearPasswordInputs();
            }
            else
            {
                _viewModel.ChangePasswordStatusMessage = "Failed to change password. Please check your current password.";
            }
        }

        private async void Logout_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ConfirmationDialog(
                "Confirm Logout",
                "Are you sure you want to logout?",
                "Warning24",
                "Yes",
                "No");
            dialog.Owner = Application.Current.MainWindow;

            if (dialog.ShowDialog() == true)
            {
                await _authService.LogoutAsync();

                var serviceProvider = ((App)Application.Current).GetServiceProvider();
                var authService = (IAuthenticationService)serviceProvider.GetService(typeof(IAuthenticationService))!;
                var loginWindow = new LoginWindow(authService);
                loginWindow.Show();

                Window.GetWindow(this)?.Close();
            }
        }

        private async void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            if (HardwareRetentionBox.Value == null ||
                NetworkRetentionBox.Value == null ||
                AlertRetentionBox.Value == null ||
                WebsiteRetentionBox.Value == null ||
                SoftwareRetentionBox.Value == null ||
                CleanupHourBox.Value == null)
            {
                _viewModel.SetRetentionStatus("Please fill in all fields.", true);
                return;
            }

            var confirmationDialog = new ConfirmationDialog(
                "Confirm Save Settings",
                "Do you want to save the updated data retention settings?",
                "Warning24",
                "Yes",
                "No");
            confirmationDialog.Owner = Application.Current.MainWindow;

            if (confirmationDialog.ShowDialog() != true)
            {
                return;
            }

            // Read values directly from NumberBox controls to bypass any binding issues
            _viewModel.HardwareRetentionDays = HardwareRetentionBox.Value ?? _viewModel.HardwareRetentionDays;
            _viewModel.NetworkRetentionDays = NetworkRetentionBox.Value ?? _viewModel.NetworkRetentionDays;
            _viewModel.AlertRetentionDays = AlertRetentionBox.Value ?? _viewModel.AlertRetentionDays;
            _viewModel.WebsiteUsageRetentionDays = WebsiteRetentionBox.Value ?? _viewModel.WebsiteUsageRetentionDays;
            _viewModel.SoftwareUsageRetentionDays = SoftwareRetentionBox.Value ?? _viewModel.SoftwareUsageRetentionDays;
            _viewModel.CleanupHourUtc = CleanupHourBox.Value ?? _viewModel.CleanupHourUtc;

            var success = await _viewModel.SaveRetentionSettingsAsync();
            if (!success)
            {
                return;
            }

            var successDialog = new ConfirmationDialog(
                "Success",
                "Settings saved successfully!",
                "Checkmark24",
                "Close",
                "Cancel",
                false);
            successDialog.Owner = Application.Current.MainWindow;
            successDialog.ShowDialog();
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

            var confirmationDialog = new ConfirmationDialog(
                "Confirm Cleanup",
                "This will permanently delete monitoring data older than the configured retention periods.\n\nDo you want to continue?",
                "Warning24",
                "Yes",
                "No");
            confirmationDialog.Owner = Application.Current.MainWindow;

            if (confirmationDialog.ShowDialog() != true)
            {
                return;
            }

            if (_viewModel.RunCleanupNowCommand.CanExecute(null))
                _viewModel.RunCleanupNowCommand.Execute(null);
        }

        private void ClearPasswordInputs()
        {
            CurrentPasswordBox.Clear();
            NewPasswordBox.Clear();
            ConfirmPasswordBox.Clear();
        }
    }
}
