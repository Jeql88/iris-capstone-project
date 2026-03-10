using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows;
using IRIS.UI.ViewModels;

namespace IRIS.UI.Views.Admin
{
    public partial class LabsView : UserControl
    {
        private object? _lastSelectedItem;

        public LabsView(LabsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void ListBox_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var listBox = (ListBox)sender;
            var hitTestResult = VisualTreeHelper.HitTest(listBox, e.GetPosition(listBox));
            
            if (hitTestResult?.VisualHit != null)
            {
                var item = listBox.InputHitTest(e.GetPosition(listBox));
                var container = listBox.ContainerFromElement((DependencyObject)item) as ListBoxItem;
                
                if (container != null)
                {
                    var selectedItem = container.Content;
                    if (_lastSelectedItem == selectedItem)
                    {
                        listBox.SelectedItem = null;
                        _lastSelectedItem = null;
                    }
                    else
                    {
                        _lastSelectedItem = selectedItem;
                    }
                }
            }
        }

        private void CloseModal_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = DataContext as LabsViewModel;
            if (viewModel != null)
                viewModel.IsAssignedPCsModalOpen = false;
        }
    }
}
