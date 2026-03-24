using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using IRIS.Core.Services.Contracts;
using IRIS.UI.Services;
using IRIS.UI.Views.Shared;
using IRIS.UI.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace IRIS.UI.Views.Common
{
    public partial class UserHeaderControl : UserControl
    {
        private INavigationService? _navigationService;
        private DispatcherTimer? _closeDropdownTimer;

        public UserHeaderControl()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var app = (App)Application.Current;
            var serviceProvider = app.GetServiceProvider();
            var authService = serviceProvider.GetRequiredService<IAuthenticationService>();

            var username = authService.GetCurrentUser()?.Username ?? "User";
            UserGreetingText.Text = $"Hi, {username}!";
        }

        public void SetNavigationService(INavigationService navigationService)
        {
            _navigationService = navigationService;
        }

        public void CloseDropdown()
        {
            UserMenuPopup.IsOpen = false;
        }

        public void SetVisibility(bool isVisible)
        {
            Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UserMenuContainer_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _closeDropdownTimer?.Stop();
            UserMenuPopup.IsOpen = true;
        }

        private void DropdownBorder_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _closeDropdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0.3) };
            _closeDropdownTimer.Tick += (s, args) =>
            {
                UserMenuPopup.IsOpen = false;
                _closeDropdownTimer.Stop();
            };
            _closeDropdownTimer.Start();
        }

        private void DropdownBorder_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _closeDropdownTimer?.Stop();
            UserMenuPopup.IsOpen = true;
        }

        private void SettingsMenuBtn_Click(object sender, RoutedEventArgs e)
        {
            UserMenuPopup.IsOpen = false;
            SetVisibility(false);
            var dashboardView = FindAncestor<DashboardView>(this);
            dashboardView?.ClearButtonsAndPanel();
            _navigationService?.NavigateTo("Settings");
        }

        private async void LogoutMenuBtn_Click(object sender, RoutedEventArgs e)
        {
            UserMenuPopup.IsOpen = false;

            var dialog = new ConfirmationDialog(
                "Confirm Logout",
                "Are you sure you want to logout?",
                "Warning24",
                "Yes",
                "No");
            dialog.Owner = Application.Current.MainWindow;

            if (dialog.ShowDialog() == true)
            {
                var serviceProvider = ((App)Application.Current).GetServiceProvider();
                var authService = serviceProvider.GetRequiredService<IAuthenticationService>();
                await authService.LogoutAsync();

                var loginWindow = new LoginWindow(authService);
                loginWindow.Show();

                Window.GetWindow(this)?.Close();
            }
        }

        private static T? FindAncestor<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is T ancestor)
                    return ancestor;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }
    }
}
