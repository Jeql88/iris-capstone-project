using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IRIS.Core.Services.Contracts;
using IRIS.UI.ViewModels;
using IRIS.UI.Services;
using IRIS.UI.Views.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace IRIS.UI.Views.Personnel
{
    public partial class PersonnelDashboardView : UserControl
    {
        private readonly DashboardViewModel _viewModel;
        private INavigationService? _navigationService;
        private ScrollViewer? dashboardContent;
        private static readonly SolidColorBrush ActiveBrush = new(Color.FromRgb(180, 40, 40));
        private static readonly SolidColorBrush DefaultForeground = Brushes.White;

        public PersonnelDashboardView() : this(((App)Application.Current).GetServiceProvider().GetRequiredService<DashboardViewModel>()) { }

        public PersonnelDashboardView(DashboardViewModel viewModel)
        {
            _viewModel = viewModel;
            InitializeComponent();
            DataContext = _viewModel;
            Loaded += (s, e) => dashboardContent = MainContent.Content as ScrollViewer;
        }

        public void SetNavigationService(INavigationService navigationService) 
        {
            _navigationService = navigationService;
            UserHeader.SetNavigationService(navigationService);
        }

        private void SetActiveButton(Button activeButton)
        {
            foreach (var btn in new[] { DashboardBtn, MonitorBtn, FileManagementBtn, PolicyBtn, LabsBtn, UsageMetricsBtn, AlertsBtn })
            {
                btn.Background = Brushes.Transparent;
                btn.Foreground = DefaultForeground;
            }
            activeButton.Background = ActiveBrush;
            activeButton.Foreground = DefaultForeground;
        }

        private void CollapseRightPanel() => MainGrid.ColumnDefinitions[2].Width = new GridLength(0);
        private void ShowRightPanel() => MainGrid.ColumnDefinitions[2].Width = new GridLength(320);

        private void DashboardBtn_Click(object sender, RoutedEventArgs e)
        {
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

        private void UsageMetricsBtn_Click(object sender, RoutedEventArgs e)
        {
            UserHeader.SetVisibility(true);
            UserHeader.CloseDropdown();
            SetActiveButton(UsageMetricsBtn);
            CollapseRightPanel();
            _navigationService?.NavigateTo("UsageMetrics");
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
