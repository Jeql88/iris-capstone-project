using System.Windows;
using System.Windows.Controls;

namespace IRIS.UI.Views
{
    public partial class ViewScreenPage : UserControl
    {
        private bool isDetailsExpanded = false;

        public ViewScreenPage()
        {
            InitializeComponent();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            var dashboardView = FindParent<DashboardView>(this);
            if (dashboardView != null)
            {
                dashboardView.ShowMonitorView();
            }
        }

        private void ViewDetailsButton_Click(object sender, RoutedEventArgs e)
        {
            isDetailsExpanded = !isDetailsExpanded;
            DetailsPanel.Visibility = isDetailsExpanded ? Visibility.Visible : Visibility.Collapsed;
            ExpandIcon.Text = isDetailsExpanded ? "▲" : "▼";
        }

        private void LockScreenButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Lock Screen functionality will be implemented.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ShutDownButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Shut Down functionality will be implemented.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void RemoteDesktopButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Remote Desktop functionality will be implemented.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = System.Windows.Media.VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            T parent = parentObject as T;
            return parent ?? FindParent<T>(parentObject);
        }
    }
}
