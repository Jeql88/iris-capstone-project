using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using IRIS.UI.ViewModels;

namespace IRIS.UI.Views.Faculty
{
    public partial class ViewScreenPage : UserControl
    {
        public ViewScreenPage(ViewScreenViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            Loaded += ViewScreenPage_Loaded;
            Unloaded += ViewScreenPage_Unloaded;
        }

        public void LoadPCData(PCDisplayModel pc)
        {
            if (DataContext is ViewScreenViewModel vm)
            {
                vm.LoadPCData(pc);
            }
        }

        private void MoreActionsButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            MoreActionsPopup.IsOpen = true;
        }

        private void MoreActionsMenuItem_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            MoreActionsPopup.IsOpen = false;
        }

        private async void ViewScreenPage_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is ViewScreenViewModel vm)
            {
                await vm.OnActivatedAsync();
            }
        }

        private void ViewScreenPage_Unloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is ViewScreenViewModel vm)
            {
                vm.OnDeactivated();
            }
        }
    }
}
