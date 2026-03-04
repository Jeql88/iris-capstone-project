using System.Windows.Controls;
using IRIS.UI.ViewModels;

namespace IRIS.UI.Views.Common
{
    public partial class AlertsView : UserControl
    {
        public AlertsView(AlertsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
