using System.Windows;
using System.Windows.Controls;
using IRIS.UI.ViewModels;
using IRIS.UI.Services;

namespace IRIS.UI.Views
{
    public partial class DashboardView : UserControl
    {
        private INavigationService? _navigationService;
        private ScrollViewer? dashboardContent;

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

        private void DashboardBtn_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            DashboardBtn.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 0, 0));
            MonitorBtn.Background = System.Windows.Media.Brushes.Transparent;
            
            var softwareMgmtBtn = this.FindName("SoftwareManagementBtn") as System.Windows.Controls.Button;
            if (softwareMgmtBtn != null) softwareMgmtBtn.Background = System.Windows.Media.Brushes.Transparent;
            PolicyBtn.Background = System.Windows.Media.Brushes.Transparent;
            UserManagementBtn.Background = System.Windows.Media.Brushes.Transparent;
            SettingsBtn.Background = System.Windows.Media.Brushes.Transparent;
            
            var accessLogsBtn = this.FindName("AccessLogsBtn") as System.Windows.Controls.Button;
            if (accessLogsBtn != null) accessLogsBtn.Background = System.Windows.Media.Brushes.Transparent;
            
            var grid = this.FindName("MainGrid") as Grid;
            if (grid != null && grid.ColumnDefinitions.Count > 2)
            {
                grid.ColumnDefinitions[2].Width = new GridLength(320);
            }
            
            MainContent.Content = dashboardContent;
        }

        private void MonitorBtn_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            DashboardBtn.Background = System.Windows.Media.Brushes.Transparent;
            MonitorBtn.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 0, 0));
            
            var softwareMgmtBtn = this.FindName("SoftwareManagementBtn") as System.Windows.Controls.Button;
            if (softwareMgmtBtn != null) softwareMgmtBtn.Background = System.Windows.Media.Brushes.Transparent;
            PolicyBtn.Background = System.Windows.Media.Brushes.Transparent;
            UserManagementBtn.Background = System.Windows.Media.Brushes.Transparent;
            SettingsBtn.Background = System.Windows.Media.Brushes.Transparent;
            
            var accessLogsBtn = this.FindName("AccessLogsBtn") as System.Windows.Controls.Button;
            if (accessLogsBtn != null) accessLogsBtn.Background = System.Windows.Media.Brushes.Transparent;
            
            var grid = this.FindName("MainGrid") as Grid;
            if (grid != null && grid.ColumnDefinitions.Count > 2)
            {
                grid.ColumnDefinitions[2].Width = new GridLength(0);
            }
            
            _navigationService?.NavigateTo("Monitor");
        }

        private void PolicyBtn_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            DashboardBtn.Background = System.Windows.Media.Brushes.Transparent;
            MonitorBtn.Background = System.Windows.Media.Brushes.Transparent;
            
            var softwareMgmtBtn = this.FindName("SoftwareManagementBtn") as System.Windows.Controls.Button;
            if (softwareMgmtBtn != null) softwareMgmtBtn.Background = System.Windows.Media.Brushes.Transparent;
            PolicyBtn.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 0, 0));
            UserManagementBtn.Background = System.Windows.Media.Brushes.Transparent;
            SettingsBtn.Background = System.Windows.Media.Brushes.Transparent;
            
            var accessLogsBtn = this.FindName("AccessLogsBtn") as System.Windows.Controls.Button;
            if (accessLogsBtn != null) accessLogsBtn.Background = System.Windows.Media.Brushes.Transparent;
            
            var grid = this.FindName("MainGrid") as Grid;
            if (grid != null && grid.ColumnDefinitions.Count > 2)
            {
                grid.ColumnDefinitions[2].Width = new GridLength(0);
            }
            
            _navigationService?.NavigateTo("PolicyEnforcement");
        }

        private void SoftwareManagementBtn_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            DashboardBtn.Background = System.Windows.Media.Brushes.Transparent;
            MonitorBtn.Background = System.Windows.Media.Brushes.Transparent;
            PolicyBtn.Background = System.Windows.Media.Brushes.Transparent;
            UserManagementBtn.Background = System.Windows.Media.Brushes.Transparent;
            SettingsBtn.Background = System.Windows.Media.Brushes.Transparent;
            
            var softwareMgmtBtn = sender as System.Windows.Controls.Button;
            if (softwareMgmtBtn != null)
            {
                softwareMgmtBtn.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 0, 0));
            }
            
            var accessLogsBtn = this.FindName("AccessLogsBtn") as System.Windows.Controls.Button;
            if (accessLogsBtn != null) accessLogsBtn.Background = System.Windows.Media.Brushes.Transparent;
            
            var grid = this.FindName("MainGrid") as Grid;
            if (grid != null && grid.ColumnDefinitions.Count > 2)
            {
                grid.ColumnDefinitions[2].Width = new GridLength(0);
            }
            
            _navigationService?.NavigateTo("SoftwareManagement");
        }

        private void UserManagementBtn_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            DashboardBtn.Background = System.Windows.Media.Brushes.Transparent;
            MonitorBtn.Background = System.Windows.Media.Brushes.Transparent;
            PolicyBtn.Background = System.Windows.Media.Brushes.Transparent;
            SettingsBtn.Background = System.Windows.Media.Brushes.Transparent;
            
            var softwareMgmtBtn = this.FindName("SoftwareManagementBtn") as System.Windows.Controls.Button;
            if (softwareMgmtBtn != null) softwareMgmtBtn.Background = System.Windows.Media.Brushes.Transparent;
            
            var accessLogsBtn = this.FindName("AccessLogsBtn") as System.Windows.Controls.Button;
            if (accessLogsBtn != null) accessLogsBtn.Background = System.Windows.Media.Brushes.Transparent;
            
            UserManagementBtn.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 0, 0));
            
            var grid = this.FindName("MainGrid") as Grid;
            if (grid != null && grid.ColumnDefinitions.Count > 2)
            {
                grid.ColumnDefinitions[2].Width = new GridLength(0);
            }
            
            _navigationService?.NavigateTo("UserManagement");
        }

        private void AccessLogsBtn_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            DashboardBtn.Background = System.Windows.Media.Brushes.Transparent;
            MonitorBtn.Background = System.Windows.Media.Brushes.Transparent;
            PolicyBtn.Background = System.Windows.Media.Brushes.Transparent;
            UserManagementBtn.Background = System.Windows.Media.Brushes.Transparent;
            SettingsBtn.Background = System.Windows.Media.Brushes.Transparent;
            
            var softwareMgmtBtn = this.FindName("SoftwareManagementBtn") as System.Windows.Controls.Button;
            if (softwareMgmtBtn != null) softwareMgmtBtn.Background = System.Windows.Media.Brushes.Transparent;
            
            var accessLogsBtn = this.FindName("AccessLogsBtn") as System.Windows.Controls.Button;
            if (accessLogsBtn != null) accessLogsBtn.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 0, 0));
            
            var grid = this.FindName("MainGrid") as Grid;
            if (grid != null && grid.ColumnDefinitions.Count > 2)
            {
                grid.ColumnDefinitions[2].Width = new GridLength(0);
            }
            
            _navigationService?.NavigateTo("AccessLogs");
        }

        private void SettingsBtn_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            DashboardBtn.Background = System.Windows.Media.Brushes.Transparent;
            MonitorBtn.Background = System.Windows.Media.Brushes.Transparent;
            PolicyBtn.Background = System.Windows.Media.Brushes.Transparent;
            UserManagementBtn.Background = System.Windows.Media.Brushes.Transparent;
            
            var softwareMgmtBtn = this.FindName("SoftwareManagementBtn") as System.Windows.Controls.Button;
            if (softwareMgmtBtn != null) softwareMgmtBtn.Background = System.Windows.Media.Brushes.Transparent;
            
            var accessLogsBtn = this.FindName("AccessLogsBtn") as System.Windows.Controls.Button;
            if (accessLogsBtn != null) accessLogsBtn.Background = System.Windows.Media.Brushes.Transparent;
            
            SettingsBtn.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 0, 0));
            
            var grid = this.FindName("MainGrid") as Grid;
            if (grid != null && grid.ColumnDefinitions.Count > 2)
            {
                grid.ColumnDefinitions[2].Width = new GridLength(0);
            }
            
            _navigationService?.NavigateTo("Settings");
        }
    }
}
