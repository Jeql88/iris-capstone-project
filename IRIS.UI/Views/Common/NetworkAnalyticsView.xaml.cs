using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IRIS.UI.Services;
using IRIS.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace IRIS.UI.Views.Common
{
    public partial class NetworkAnalyticsView : UserControl
    {
        private readonly NetworkAnalyticsViewModel _viewModel;

        public NetworkAnalyticsView(NetworkAnalyticsViewModel viewModel)
        {
            _viewModel = viewModel;
            InitializeComponent();
            DataContext = _viewModel;
        }

        public void LoadParameter(NetworkAnalyticsParameter param)
        {
            _ = _viewModel.LoadAsync(param);
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Walk up the visual tree to find the parent DashboardView and restore its dashboard content
            var parent = FindParentDashboardView(this);
            if (parent != null)
            {
                parent.RestoreDashboardContent();
            }
        }

        private static DashboardView? FindParentDashboardView(DependencyObject child)
        {
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is DashboardView dashboard)
                    return dashboard;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }
    }
}
