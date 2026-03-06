using System.Windows;
using IRIS.UI.Services;

namespace IRIS.UI.Views.Faculty
{
    public partial class FacultyMainWindow : Window
    {
        public FacultyMainWindow(FacultyDashboardView dashboardView, INavigationService navigationService, IServiceProvider serviceProvider)
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
}
