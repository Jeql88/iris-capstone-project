using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IRIS.Core.Services.Contracts;
using IRIS.UI.Services;
using IRIS.UI.Views.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace IRIS.UI.Views.Faculty
{
    public partial class FacultyDashboardView : UserControl
    {
        private INavigationService? _navigationService;
        private static readonly SolidColorBrush ActiveBrush = new(Color.FromRgb(180, 40, 40));
        private static readonly SolidColorBrush DefaultForeground = Brushes.White;

        public FacultyDashboardView()
        {
            InitializeComponent();
            Loaded += (s, e) => _navigationService?.NavigateTo("Monitor");
        }

        public void SetNavigationService(INavigationService navigationService) 
        {
            _navigationService = navigationService;
            UserHeader.SetNavigationService(navigationService);
        }

        private void SetActiveButton(Button activeButton)
        {
            foreach (var btn in new[] { MonitorBtn, FileManagementBtn })
            {
                btn.Background = Brushes.Transparent;
                btn.Foreground = DefaultForeground;
            }
            activeButton.Background = ActiveBrush;
            activeButton.Foreground = DefaultForeground;
        }

        private void MonitorBtn_Click(object sender, RoutedEventArgs e)
        {
            UserHeader.SetVisibility(true);
            UserHeader.CloseDropdown();
            SetActiveButton(MonitorBtn);
            _navigationService?.NavigateTo("Monitor");
        }

        private void FileManagementBtn_Click(object sender, RoutedEventArgs e)
        {
            UserHeader.SetVisibility(true);
            UserHeader.CloseDropdown();
            SetActiveButton(FileManagementBtn);
            _navigationService?.NavigateTo("FileManagement");
        }

        private async void LogoutBtn_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to logout?", "Confirm Logout", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                var serviceProvider = ((App)Application.Current).GetServiceProvider();
                var authService = (IAuthenticationService)serviceProvider.GetService(typeof(IAuthenticationService))!;
                await authService.LogoutAsync();
                new LoginWindow(authService).Show();
                Window.GetWindow(this)?.Close();
            }
        }
    }
}
