using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IRIS.Core.Services.Contracts;
using IRIS.UI.ViewModels;
using IRIS.UI.Services;
using IRIS.UI.Views.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace IRIS.UI.Views.Common
{
    public partial class DashboardView : UserControl
    {
        private readonly DashboardViewModel _viewModel;
        private INavigationService? _navigationService;
        private ScrollViewer? dashboardContent;
        private static readonly SolidColorBrush ActiveBrush = new(Color.FromRgb(180, 40, 40));
        private static readonly SolidColorBrush DefaultForeground = Brushes.White;
        private static readonly SolidColorBrush ActiveForeground = Brushes.White;

        public DashboardView() : this(((App)Application.Current).GetServiceProvider().GetRequiredService<DashboardViewModel>())
        {
        }

        public DashboardView(DashboardViewModel viewModel)
        {
            _viewModel = viewModel;
            InitializeComponent();
            DataContext = _viewModel;
            Loaded += OnLoaded;
        }

        public void SetNavigationService(INavigationService navigationService)
        {
            _navigationService = navigationService;
            UserHeader.SetNavigationService(navigationService);
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
                DashboardBtn, MonitorBtn, FileManagementBtn,
                PolicyBtn, LabsBtn, AccessLogsBtn, UserManagementBtn,
                UsageMetricsBtn, AlertsBtn
            };

            foreach (var btn in allButtons)
            {
                btn.Background = Brushes.Transparent;
                btn.Foreground = DefaultForeground;
            }

            activeButton.Background = ActiveBrush;
            activeButton.Foreground = ActiveForeground;
        }

        private void ClearActiveButton()
        {
            var allButtons = new[]
            {
                DashboardBtn, MonitorBtn, FileManagementBtn,
                PolicyBtn, LabsBtn, AccessLogsBtn, UserManagementBtn,
                UsageMetricsBtn, AlertsBtn
            };

            foreach (var btn in allButtons)
            {
                btn.Background = Brushes.Transparent;
                btn.Foreground = DefaultForeground;
            }
        }

        public void ClearButtonsAndPanel()
        {
            ClearActiveButton();
            CollapseRightPanel();
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
            RestoreDashboardContent();
        }

        /// <summary>
        /// Restores the dashboard scroll content and right panel.
        /// Called by child pages (e.g. NetworkAnalyticsView) to navigate back.
        /// </summary>
        public void RestoreDashboardContent()
        {
            UserHeader.SetVisibility(true);
            SetActiveButton(DashboardBtn);
            ShowRightPanel();
            MainContent.Content = dashboardContent;
        }

        private void MonitorBtn_Click(object sender, RoutedEventArgs e)
        {
            UserHeader.SetVisibility(true);
            UserHeader.CloseDropdown();
            SetActiveButton(MonitorBtn);
            CollapseRightPanel();
            _navigationService?.NavigateTo("Monitor");
        }

        private void PolicyBtn_Click(object sender, RoutedEventArgs e)
        {
            UserHeader.SetVisibility(true);
            UserHeader.CloseDropdown();
            SetActiveButton(PolicyBtn);
            CollapseRightPanel();
            _navigationService?.NavigateTo("PolicyEnforcement");
        }

        private void LabsBtn_Click(object sender, RoutedEventArgs e)
        {
            UserHeader.SetVisibility(true);
            UserHeader.CloseDropdown();
            SetActiveButton(LabsBtn);
            CollapseRightPanel();
            _navigationService?.NavigateTo("Labs");
        }

        private void FileManagementBtn_Click(object sender, RoutedEventArgs e)
        {
            UserHeader.SetVisibility(true);
            UserHeader.CloseDropdown();
            SetActiveButton(FileManagementBtn);
            CollapseRightPanel();
            _navigationService?.NavigateTo("FileManagement");
        }

        private void UserManagementBtn_Click(object sender, RoutedEventArgs e)
        {
            UserHeader.SetVisibility(true);
            UserHeader.CloseDropdown();
            SetActiveButton(UserManagementBtn);
            CollapseRightPanel();
            _navigationService?.NavigateTo("UserManagement");
        }

        private void UsageMetricsBtn_Click(object sender, RoutedEventArgs e)
        {
            UserHeader.SetVisibility(true);
            UserHeader.CloseDropdown();
            SetActiveButton(UsageMetricsBtn);
            CollapseRightPanel();
            _navigationService?.NavigateTo("UsageMetrics");
        }

        private void AccessLogsBtn_Click(object sender, RoutedEventArgs e)
        {
            UserHeader.SetVisibility(true);
            UserHeader.CloseDropdown();
            SetActiveButton(AccessLogsBtn);
            CollapseRightPanel();
            _navigationService?.NavigateTo("AccessLogs");
        }

        private void AlertsBtn_Click(object sender, RoutedEventArgs e)
        {
            UserHeader.SetVisibility(true);
            UserHeader.CloseDropdown();
            SetActiveButton(AlertsBtn);
            CollapseRightPanel();
            _navigationService?.NavigateTo("Alerts");
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

        private void LatencyChartBorder_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) NavigateToAnalytics("Latency");
        }

        private void BandwidthChartBorder_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) NavigateToAnalytics("Bandwidth");
        }

        private void PacketLossChartBorder_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) NavigateToAnalytics("PacketLoss");
        }

        private void NavigateToAnalytics(string chartType)
        {
            if (_navigationService == null) return;

            // Replicate the same date range logic as DashboardViewModel.GetRangeUtc()
            DateTime startUtc, endUtc;
            var preset = _viewModel.SelectedRangePreset;

            if (string.Equals(preset, "Last 24h", StringComparison.OrdinalIgnoreCase))
            {
                endUtc = DateTime.UtcNow;
                startUtc = endUtc.AddHours(-24);
            }
            else if (string.Equals(preset, "Last 7d", StringComparison.OrdinalIgnoreCase))
            {
                endUtc = DateTime.UtcNow;
                startUtc = endUtc.AddDays(-7);
            }
            else
            {
                var startLocal = _viewModel.StartDate.Date;
                var endLocal = _viewModel.EndDate.Date.AddDays(1).AddTicks(-1);
                if (endLocal < startLocal) endLocal = startLocal.AddDays(1).AddTicks(-1);
                startUtc = DateTime.SpecifyKind(startLocal, DateTimeKind.Local).ToUniversalTime();
                endUtc = DateTime.SpecifyKind(endLocal, DateTimeKind.Local).ToUniversalTime();
            }

            var param = new ViewModels.NetworkAnalyticsParameter
            {
                ChartType = chartType,
                RoomId = _viewModel.SelectedRoom != null && _viewModel.SelectedRoom.Id > 0
                    ? _viewModel.SelectedRoom.Id
                    : null,
                RoomDescription = _viewModel.ActiveRoomDescription,
                RangeDescription = _viewModel.ActiveRangeDescription,
                StartUtc = startUtc,
                EndUtc = endUtc
            };

            CollapseRightPanel();
            _navigationService.NavigateTo("NetworkAnalytics", param);
        }
    }
}
