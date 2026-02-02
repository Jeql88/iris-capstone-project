using System.Windows.Controls;
using System.Windows.Input;
using IRIS.UI.ViewModels;

namespace IRIS.UI.Views
{
    public partial class PolicyEnforcementView : UserControl
    {
        public PolicyEnforcementView(PolicyEnforcementViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void RoomBorder_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is RoomItem room && DataContext is PolicyEnforcementViewModel viewModel)
            {
                viewModel.ToggleRoom(room);
            }
        }
    }
}
