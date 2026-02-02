using System.Windows;
using IRIS.Core.Models;
using IRIS.Core.Services;

namespace IRIS.UI.Views
{
    public partial class ChangePasswordWindow : Window
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

            var success = await _authService.ChangePasswordAsync(_user.Id, currentPassword, newPassword);

            if (success)
            {
                MessageBox.Show("Password changed successfully! You can now access the system.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                PasswordChanged = true;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Failed to change password. Please check your current password.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
