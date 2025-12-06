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
            
            var grid = this.FindName("MainGrid") as Grid;
            if (grid != null && grid.ColumnDefinitions.Count > 2)
            {
                grid.ColumnDefinitions[2].Width = new GridLength(0);
            }
            
            _navigationService?.NavigateTo("Monitor");
        }
    }
}
