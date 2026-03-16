using System.Windows.Controls;
using System.Windows;
using IRIS.UI.ViewModels;

namespace IRIS.UI.Views.Admin
{
    public partial class LabsView : UserControl
    {
        public LabsView(LabsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void CloseModal_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = DataContext as LabsViewModel;
            if (viewModel != null)
                viewModel.IsAssignedPCsModalOpen = false;
        }
    }
}
