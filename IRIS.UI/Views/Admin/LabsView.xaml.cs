using System.Windows.Controls;
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
    }
}
