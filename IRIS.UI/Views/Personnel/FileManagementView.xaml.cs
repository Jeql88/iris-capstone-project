using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using IRIS.UI.Models;
using IRIS.UI.ViewModels;

namespace IRIS.UI.Views.Personnel
{
    public partial class FileManagementView : UserControl
    {
        private Point _dragStartPoint;

        public FileManagementView(FileManagementViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        // ═══ Local pane: double-click ═══
        private async void LocalFilesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is FileManagementViewModel vm && vm.SelectedLocalFile != null)
            {
                await vm.OpenLocalItemAsync(vm.SelectedLocalFile);
            }
        }

        // ═══ Remote pane: double-click ═══
        private async void RemoteFilesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is FileManagementViewModel vm && vm.SelectedRemoteFile != null)
            {
                await vm.OpenRemoteItemAsync(vm.SelectedRemoteFile);
            }
        }

        // ═══ Local pane: drag start ═══
        private void LocalFilesGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void LocalFilesGrid_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;

            var diff = _dragStartPoint - e.GetPosition(null);
            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                if (DataContext is FileManagementViewModel vm &&
                    vm.SelectedLocalFile != null &&
                    !vm.SelectedLocalFile.IsDrive)
                {
                    var data = new DataObject(DataFormats.FileDrop,
                        new[] { vm.SelectedLocalFile.FullPath });
                    DragDrop.DoDragDrop(LocalFilesGrid, data, DragDropEffects.Copy);
                }
            }
        }

        // ═══ Remote pane: drop (upload) ═══
        private async void RemotePane_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (DataContext is FileManagementViewModel vm)
                {
                    await vm.UploadDroppedFilesToRemoteAsync(files);
                }
            }
        }

        private void RemotePane_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
            e.Handled = true;
        }

        // ═══ Right-click: select row under cursor ═══
        private void LocalFilesGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            SelectRowUnderCursor(sender as DataGrid, e);
        }

        private void RemoteFilesGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            SelectRowUnderCursor(sender as DataGrid, e);
        }

        private static void SelectRowUnderCursor(DataGrid? grid, MouseButtonEventArgs e)
        {
            if (grid == null) return;
            var dep = (DependencyObject)e.OriginalSource;
            while (dep != null && dep is not DataGridRow)
                dep = VisualTreeHelper.GetParent(dep);

            if (dep is DataGridRow row)
            {
                row.IsSelected = true;
                grid.CurrentItem = row.DataContext;
            }
        }

        // ═══ Path box: Enter key ═══
        private void LocalPathBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is FileManagementViewModel vm)
                vm.NavigateToLocalPathCommand.Execute(null);
        }

        private void RemotePathBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is FileManagementViewModel vm)
                vm.NavigateToRemotePathCommand.Execute(null);
        }

        // ═══ Bulk upload: drop zone ═══
        private void BulkDropZone_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (DataContext is FileManagementViewModel vm)
                {
                    vm.AddBulkFiles(files);
                }
            }
        }

        private void BulkDropZone_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
            e.Handled = true;
        }
    }
}
