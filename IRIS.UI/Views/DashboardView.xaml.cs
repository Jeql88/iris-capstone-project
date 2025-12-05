using System.Windows;
using System.Windows.Controls;

namespace IRIS.UI.Views
{
    public partial class DashboardView : UserControl
    {
        private ScrollViewer dashboardContent;
        private MonitorView monitorView;

        public DashboardView()
        {
            InitializeComponent();
            
            // Store the original dashboard content (the ScrollViewer)
            dashboardContent = MainContent.Content as ScrollViewer;
            
            // Initialize monitor view
            monitorView = new MonitorView();
        }

        private void DashboardBtn_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // Reset button styles
            DashboardBtn.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 0, 0));
            MonitorBtn.Background = System.Windows.Media.Brushes.Transparent;
            
            // Restore right sidebar by setting its width back to 320
            var grid = this.FindName("MainGrid") as Grid;
            if (grid != null && grid.ColumnDefinitions.Count > 2)
            {
                grid.ColumnDefinitions[2].Width = new GridLength(320);
            }
            
            // Show dashboard content
            MainContent.Content = dashboardContent;
        }

        private void MonitorBtn_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // Reset button styles
            DashboardBtn.Background = System.Windows.Media.Brushes.Transparent;
            MonitorBtn.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 0, 0));
            
            // Hide right sidebar by setting its width to 0
            var grid = this.FindName("MainGrid") as Grid;
            if (grid != null && grid.ColumnDefinitions.Count > 2)
            {
                grid.ColumnDefinitions[2].Width = new GridLength(0);
            }
            
            // Show monitor view
            MainContent.Content = monitorView;
        }
    }
}
