using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using IRIS.UI.ViewModels;

namespace IRIS.UI.Views
{
    public partial class MonitorView : UserControl
    {
        public MonitorView(MonitorViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var border = FindParent<Border>(button);
            var popup = FindChild<Popup>(border, "ContextMenuPopup");
            if (popup != null)
            {
                popup.IsOpen = true;
            }
        }

        private void ViewScreen_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var border = FindParent<Border>(button);
            var popup = FindChild<Popup>(border, "ContextMenuPopup");
            if (popup != null)
            {
                popup.IsOpen = false;
            }

            if (button?.DataContext is PCDisplayModel pc && DataContext is MonitorViewModel vm)
            {
                vm.SelectedPC = pc;
                vm.ViewScreenCommand.Execute(null);
            }
        }

        private void LockScreen_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var border = FindParent<Border>(button);
            var popup = FindChild<Popup>(border, "ContextMenuPopup");
            if (popup != null)
            {
                popup.IsOpen = false;
            }

            if (button?.DataContext is PCDisplayModel pc && DataContext is MonitorViewModel vm)
            {
                vm.SelectedPC = pc;
                vm.LockScreenCommand.Execute(null);
            }
        }

        private T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            T parent = parentObject as T;
            return parent ?? FindParent<T>(parentObject);
        }

        private T FindChild<T>(DependencyObject parent, string childName) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T && (child as FrameworkElement)?.Name == childName)
                {
                    return (T)child;
                }

                T childOfChild = FindChild<T>(child, childName);
                if (childOfChild != null)
                    return childOfChild;
            }

            return null;
        }
    }
}