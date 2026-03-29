using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using IRIS.UI.ViewModels;

namespace IRIS.UI.Views.Personnel
{
    public partial class MonitorView : UserControl
    {
        public MonitorView(MonitorViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void PCCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject source && FindParent<Button>(source) != null)
                return;

            if (sender is FrameworkElement element && element.Tag is PCDisplayModel pc)
            {
                if (DataContext is MonitorViewModel vm)
                {
                    vm.ToggleCardDetailsCommand.Execute(pc);
                }
            }
        }

        private void Page_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not MonitorViewModel vm || vm.SelectedPC == null)
                return;

            // Walk visual tree — if click is inside a card or a popup, don't deselect
            if (e.OriginalSource is DependencyObject source)
            {
                var current = source;
                while (current != null)
                {
                    if (current is Border b && b.Tag is PCDisplayModel)
                        return;
                    if (current is System.Windows.Controls.Primitives.Popup)
                        return;
                    current = VisualTreeHelper.GetParent(current);
                }
            }

            vm.SelectedPC.IsFlipped = false;
            vm.SelectedPC = null;
        }

        private void RoomBorder_Click(object sender, MouseButtonEventArgs e)
        {
            // Room selection logic - if needed
        }

        private void AlertCountBadge_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is PCDisplayModel pc && DataContext is MonitorViewModel vm)
            {
                vm.OpenPcAlertsForPCCommand.Execute(pc);
            }
        }

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;
            var border = FindParent<Border>(button);
            if (border == null) return;
            var popup = FindChild<Popup>(border, "ContextMenuPopup");
            if (popup != null)
            {
                popup.IsOpen = true;
            }
        }

        private void ViewScreen_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;
            var border = FindParent<Border>(button);
            if (border != null)
            {
                var popup = FindChild<Popup>(border, "ContextMenuPopup");
                if (popup != null)
                {
                    popup.IsOpen = false;
                }
            }

            if (button.DataContext is PCDisplayModel pc && DataContext is MonitorViewModel vm)
            {
                vm.SelectedPC = pc;
                vm.ViewScreenCommand.Execute(null);
            }
        }

        private void Freeze_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;
            var border = FindParent<Border>(button);
            if (border != null)
            {
                var popup = FindChild<Popup>(border, "ContextMenuPopup");
                if (popup != null)
                {
                    popup.IsOpen = false;
                }
            }

            if (button.DataContext is PCDisplayModel pc && DataContext is MonitorViewModel vm)
            {
                vm.SelectedPC = pc;
                vm.FreezeCommand.Execute(null);
            }
        }

        private void RemoteDesktop_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;
            var border = FindParent<Border>(button);
            if (border != null)
            {
                var popup = FindChild<Popup>(border, "ContextMenuPopup");
                if (popup != null)
                {
                    popup.IsOpen = false;
                }
            }

            if (button.DataContext is PCDisplayModel pc && DataContext is MonitorViewModel vm)
            {
                vm.SelectedPC = pc;
                vm.RemoteDesktopCommand.Execute(null);
            }
        }

        private void HealthTimeline_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;
            var border = FindParent<Border>(button);
            if (border != null)
            {
                var popup = FindChild<Popup>(border, "ContextMenuPopup");
                if (popup != null)
                {
                    popup.IsOpen = false;
                }
            }

            if (button.DataContext is PCDisplayModel pc && DataContext is MonitorViewModel vm)
            {
                vm.ShowTimelineForPCCommand.Execute(pc);
            }
        }

        private T? FindParent<T>(DependencyObject? child) where T : DependencyObject
        {
            if (child == null) return null;
            DependencyObject? parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindParent<T>(parentObject);
        }

        private T? FindChild<T>(DependencyObject? parent, string childName) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T typedChild && (typedChild as FrameworkElement)?.Name == childName)
                {
                    return typedChild;
                }

                var childOfChild = FindChild<T>(child, childName);
                if (childOfChild != null)
                    return childOfChild;
            }

            return null;
        }
    }
}