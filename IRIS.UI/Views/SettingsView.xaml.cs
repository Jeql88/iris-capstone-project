using System.Windows;
using System.Windows.Controls;
using IRIS.Core.Services;
using IRIS.UI.Services;
using IRIS.UI.ViewModels;

namespace IRIS.UI.Views
{
    public partial class SettingsView : UserControl
    {
        private readonly SettingsViewModel _viewModel;
        private readonly IAuthenticationService _authService;
        private readonly INavigationService _navigationService;

        public SettingsView(SettingsViewModel viewModel, IAuthenticationService authService, INavigationService navigationService)
        {
            InitializeComponent();
            DataContext = viewModel;
            _viewModel = viewModel;
            _authService = authService;
            _navigationService = navigationService;
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
    }
}
