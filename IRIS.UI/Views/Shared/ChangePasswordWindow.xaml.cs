using System.Windows;
using IRIS.Core.Models;
using IRIS.Core.Services.Contracts;
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
            var currentPassword = CurrentPasswordBox.Password;
            var newPassword = NewPasswordBox.Password;
            var confirmPassword = ConfirmPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(currentPassword))
            {
                await ShowMessageAsync("Validation Error", "Current password is required.");
                return;
            }

            if (string.IsNullOrWhiteSpace(newPassword))
            {
                await ShowMessageAsync("Validation Error", "New password is required.");
                return;
            }

            if (newPassword.Length < 8)
            {
                await ShowMessageAsync("Validation Error", "New password must be at least 8 characters long.");
                return;
            }

            if (newPassword != confirmPassword)
            {
                await ShowMessageAsync("Validation Error", "New password and confirmation do not match.");
                return;
            }

            var success = await _authService.ChangePasswordAsync(_user.Id, currentPassword, newPassword);

            if (success)
            {
                await ShowMessageAsync("Success", "Password changed successfully! You can now access the system.");
                PasswordChanged = true;
                DialogResult = true;
                Close();
            }
            else
            {
                await ShowMessageAsync("Error", "Failed to change password. Please check your current password.");
            }
        }

        private async Task ShowMessageAsync(string title, string message)
        {
            var messageBox = new Wpf.Ui.Controls.MessageBox
            {
                Title = title,
                Content = message,
                PrimaryButtonText = "OK"
            };
            await messageBox.ShowDialogAsync();
        }
    }
}
