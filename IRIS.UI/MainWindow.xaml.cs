using System.Windows;
using IRIS.UI.Views;
using IRIS.UI.Services;

namespace IRIS.UI;

public partial class MainWindow : Window
{
    public MainWindow(DashboardView dashboardView, INavigationService navigationService, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        Content = dashboardView;
        
        Loaded += (s, e) =>
        {
            if (navigationService is NavigationService navService)
            {
                var navFrame = dashboardView.FindName("MainContent") as System.Windows.Controls.ContentControl;
                if (navFrame != null)
                {
                    navService.Initialize(navFrame, serviceProvider);
                    dashboardView.SetNavigationService(navigationService);
                }
            }
        };
    }
}