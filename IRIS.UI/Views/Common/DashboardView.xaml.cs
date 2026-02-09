using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IRIS.Core.Services.Contracts;
using IRIS.UI.ViewModels;
using IRIS.UI.Services;
using IRIS.UI.Views.Shared;

namespace IRIS.UI.Views.Common
{
    public partial class DashboardView : UserControl
    {
        private INavigationService? _navigationService;
        private ScrollViewer? dashboardContent;
        private static readonly SolidColorBrush ActiveBrush = new(Color.FromArgb(50, 255, 255, 255));

        public DashboardView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        public void SetNavigationService(INavigationService navigationService)
        {
            _navigationService = navigationService;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            dashboardContent = MainContent.Content as ScrollViewer;
        }

        /// <summary>
        /// Resets all sidebar buttons to transparent, then highlights the active one.
        /// </summary>
        private void SetActiveButton(Button activeButton)
        {
            var allButtons = new[]
            {
                DashboardBtn, MonitorBtn, SoftwareManagementBtn,
                PolicyBtn, AccessLogsBtn, UserManagementBtn,
                UsageMetricsBtn, SettingsBtn
            };

            foreach (var btn in allButtons)
            {
                btn.Background = Brushes.Transparent;
            }

            activeButton.Background = ActiveBrush;
        }

        private void CollapseRightPanel()
        {
            if (MainGrid.ColumnDefinitions.Count > 2)
            {
                MainGrid.ColumnDefinitions[2].Width = new GridLength(0);
            }
        }

        private void ShowRightPanel()
        {
            if (MainGrid.ColumnDefinitions.Count > 2)
            {
                MainGrid.ColumnDefinitions[2].Width = new GridLength(320);
            }
        }

        private void DashboardBtn_Click(object sender, RoutedEventArgs e)
        {
            SetActiveButton(DashboardBtn);
            ShowRightPanel();
            MainContent.Content = dashboardContent;
        }

        private void MonitorBtn_Click(object sender, RoutedEventArgs e)
        {
            SetActiveButton(MonitorBtn);
            CollapseRightPanel();
            _navigationService?.NavigateTo("Monitor");
        }

        private void PolicyBtn_Click(object sender, RoutedEventArgs e)
        {
            SetActiveButton(PolicyBtn);
            CollapseRightPanel();
            _navigationService?.NavigateTo("PolicyEnforcement");
        }

        private void SoftwareManagementBtn_Click(object sender, RoutedEventArgs e)
        {
            SetActiveButton(SoftwareManagementBtn);
            CollapseRightPanel();
            _navigationService?.NavigateTo("SoftwareManagement");
        }

        private void UserManagementBtn_Click(object sender, RoutedEventArgs e)
        {
            SetActiveButton(UserManagementBtn);
            CollapseRightPanel();
            _navigationService?.NavigateTo("UserManagement");
        }

        private void UsageMetricsBtn_Click(object sender, RoutedEventArgs e)
        {
            SetActiveButton(UsageMetricsBtn);
            CollapseRightPanel();
            _navigationService?.NavigateTo("UsageMetrics");
        }

        private void AccessLogsBtn_Click(object sender, RoutedEventArgs e)
        {
            SetActiveButton(AccessLogsBtn);
            CollapseRightPanel();
            _navigationService?.NavigateTo("AccessLogs");
        }

        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            SetActiveButton(SettingsBtn);
            CollapseRightPanel();
            _navigationService?.NavigateTo("Settings");
        }

        private async void LogoutBtn_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to logout?", "Confirm Logout",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var serviceProvider = ((App)Application.Current).GetServiceProvider();
                var authService = (IAuthenticationService)serviceProvider.GetService(typeof(IAuthenticationService))!;
                await authService.LogoutAsync();

                var loginWindow = new LoginWindow(authService);
                loginWindow.Show();

                Window.GetWindow(this)?.Close();
            }
        }
    }
}
