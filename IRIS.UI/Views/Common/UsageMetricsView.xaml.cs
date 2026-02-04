using System.Windows;
using System.Windows.Controls;
using IRIS.UI.ViewModels;

namespace IRIS.UI.Views.Common
{
    public partial class UsageMetricsView : UserControl
    {
        public UsageMetricsView()
        {
            InitializeComponent();
        }

        public UsageMetricsView(UsageMetricsViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        private void AppUsageTab_Click(object sender, RoutedEventArgs e)
        {
            ApplicationUsagePanel.Visibility = Visibility.Visible;
            WebsiteUsagePanel.Visibility = Visibility.Collapsed;
            AppUsageTab.Appearance = Wpf.Ui.Controls.ControlAppearance.Primary;
            WebUsageTab.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;
        }

        private void WebUsageTab_Click(object sender, RoutedEventArgs e)
        {
            ApplicationUsagePanel.Visibility = Visibility.Collapsed;
            WebsiteUsagePanel.Visibility = Visibility.Visible;
            AppUsageTab.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;
            WebUsageTab.Appearance = Wpf.Ui.Controls.ControlAppearance.Primary;
        }
    }
}