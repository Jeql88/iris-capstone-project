using System.Windows;
using IRIS.Core.Models;
using IRIS.Core.Services.Contracts;
using IRIS.UI.Views.Dialogs;
using Wpf.Ui.Controls;

namespace IRIS.UI.Views.Shared
{
    public partial class ChangePasswordWindow : FluentWindow
    {
        private readonly IAuthenticationService _authService;
        private readonly User _user;

        public bool PasswordChanged { get; private set; }

        public ChangePasswordWindow(IAuthenticationService authService, User user)
        {
            InitializeComponent();
            _authService = authService;
            _user = user;
        }

        private async void ChangePassword_Click(object sender, RoutedEventArgs e)
        {
            SetError(string.Empty);

            var currentPassword = CurrentPasswordBox.Password;
            var newPassword = NewPasswordBox.Password;
            var confirmPassword = ConfirmPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(currentPassword))
            {
                SetError("Current password is required.");
                return;
            }

            if (string.IsNullOrWhiteSpace(newPassword))
            {
                SetError("New password is required.");
                return;
            }

            if (newPassword.Length < 8)
            {
                SetError("New password must be at least 8 characters long.");
                return;
            }

            if (newPassword != confirmPassword)
            {
                SetError("New password and confirmation do not match.");
                return;
            }

            try
            {
                var success = await _authService.ChangePasswordAsync(_user.Id, currentPassword, newPassword);

                if (success)
                {
                    ShowSuccessDialog();
                    PasswordChanged = true;
                    DialogResult = true;
                    Close();
                }
                else
                {
                    SetError("Failed to change password. Please check your current password.");
                }
            }
            catch
            {
                SetError("An unexpected error occurred while changing password.");
            }
        }

        private void SetError(string message)
        {
            ErrorMessageTextBlock.Text = message;
            ErrorMessageTextBlock.Visibility = string.IsNullOrWhiteSpace(message)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void ShowSuccessDialog()
        {
            var successDialog = new ConfirmationDialog(
                "Success",
                "Password changed successfully! You can now access the system.",
                "Checkmark24",
                "Close",
                "Cancel",
                false);

            successDialog.Owner = this;
            successDialog.ShowDialog();
        }
    }
}
