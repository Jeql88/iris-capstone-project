using System.Windows.Controls;
using System.Windows.Input;
using IRIS.UI.ViewModels;

namespace IRIS.UI.Views
{
    public partial class PolicyEnforcementView : UserControl
    {
        public PolicyEnforcementView()
        {
            InitializeComponent();
        }

        private void LabBorder_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is LabItem lab && DataContext is PolicyEnforcementViewModel viewModel)
            {
                viewModel.ToggleLab(lab);
            }
        }
    }
}
