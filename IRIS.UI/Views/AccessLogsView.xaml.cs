using System.Windows.Controls;
using IRIS.UI.ViewModels;

namespace IRIS.UI.Views
{
    public partial class AccessLogsView : UserControl
    {
        public AccessLogsView(AccessLogsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}