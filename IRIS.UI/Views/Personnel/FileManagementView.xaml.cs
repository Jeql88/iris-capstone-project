using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using IRIS.UI.Models;
using IRIS.UI.ViewModels;
using IRIS.UI.Views.Dialogs;

namespace IRIS.UI.Views.Personnel
{
    public partial class FileManagementView : UserControl
    {
        private Point _dragStartPoint;
        private List<FileItemModel> _dragSelectionSnapshot = [];

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
                // Keep directory/drive navigation immediate; only confirm before file download.
                if (!vm.SelectedRemoteFile.IsDirectory && !vm.SelectedRemoteFile.IsDrive)
                {
                    var dialog = new ConfirmationDialog(
                        "Download Remote File",
                        $"Download '{vm.SelectedRemoteFile.Name}' to local files?",
                        "ArrowDownload24",
                        "Download",
                        "Cancel");
                    dialog.Owner = Application.Current.MainWindow;
                    if (dialog.ShowDialog() != true) return;
                }

                await vm.OpenRemoteItemAsync(vm.SelectedRemoteFile);
            }
        }

        // ═══ Local pane: drag start ═══
        private void LocalFilesGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            // Snapshot the full selection immediately (tunnel phase) — before the DataGrid
            // has a chance to collapse a multi-selection down to a single row.
            _dragSelectionSnapshot = DataContext is FileManagementViewModel vm
                ? vm.SelectedLocalFiles.ToList()
                : [];
        }

        private void LocalFilesGrid_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;

            var diff = _dragStartPoint - e.GetPosition(null);
            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                // Use the snapshot taken at MouseDown so the full multi-selection is
                // preserved even if the DataGrid changed SelectedItems after the click.
                var paths = _dragSelectionSnapshot
                    .Where(f => !f.IsDrive)
                    .Select(f => f.FullPath)
                    .ToArray();

                if (paths.Length > 0)
                {
                    // Clear both guards so subsequent mouse-moves don't restart a drag.
                    _dragStartPoint = default;
                    _dragSelectionSnapshot = [];

                    var data = new DataObject(DataFormats.FileDrop, paths);
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
                // If the right-clicked row is already part of a multi-selection, keep
                // the whole selection so that Delete/Upload/Download act on all of them.
                // Otherwise, collapse to just this row (standard right-click behavior).
                if (!row.IsSelected)
                {
                    grid.SelectedItems.Clear();
                    row.IsSelected = true;
                }
                grid.CurrentItem = row.DataContext;
            }
        }

        // ═══ Selection tracking (feeds SelectedLocalFiles / SelectedRemoteFiles in VM) ═══
        private void LocalFilesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is FileManagementViewModel vm)
                vm.SelectedLocalFiles = LocalFilesGrid.SelectedItems.Cast<FileItemModel>().ToList();
        }

        private void RemoteFilesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is FileManagementViewModel vm)
                vm.SelectedRemoteFiles = RemoteFilesGrid.SelectedItems.Cast<FileItemModel>().ToList();
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
