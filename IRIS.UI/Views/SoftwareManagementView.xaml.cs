using System.Windows.Controls;
using IRIS.UI.ViewModels;

namespace IRIS.UI.Views
{
    public partial class SoftwareManagementView : UserControl
    {
        public SoftwareManagementView(SoftwareManagementViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void DeployModeButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is SoftwareManagementViewModel vm)
            {
                vm.IsDeployMode = true;
            }
        }

        private void UninstallModeButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is SoftwareManagementViewModel vm)
            {
                vm.IsDeployMode = false;
            }
        }
    }
}
