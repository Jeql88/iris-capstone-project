using System.Windows.Controls;
using IRIS.UI.ViewModels;

namespace IRIS.UI.Views
{
    public partial class ViewScreenPage : UserControl
    {
        public ViewScreenPage(ViewScreenViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        public void LoadPCData(PCDisplayModel pc)
        {
            if (DataContext is ViewScreenViewModel vm)
            {
                vm.LoadPCData(pc);
            }
        }
    }
}
