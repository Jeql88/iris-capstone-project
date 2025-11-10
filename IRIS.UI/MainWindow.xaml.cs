using System.Windows;
using IRIS.UI.Views;

namespace IRIS.UI;

public partial class MainWindow : Window
{
    public MainWindow(DashboardView dashboardView)
    {
        InitializeComponent();
        Content = dashboardView;
    }
}