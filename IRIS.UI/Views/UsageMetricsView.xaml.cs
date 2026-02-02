using System.Windows.Controls;
using IRIS.UI.ViewModels;

namespace IRIS.UI.Views
{
    public partial class UsageMetricsView : UserControl
    {
        public UsageMetricsView()
        {
            InitializeComponent();
        }

        public UsageMetricsView(UsageMetricsViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}