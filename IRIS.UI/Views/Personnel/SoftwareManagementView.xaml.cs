using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using IRIS.UI.ViewModels;

namespace IRIS.UI.Views.Personnel
{
    public partial class SoftwareManagementView : UserControl
    {
        public SoftwareManagementView(SoftwareManagementViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void DeployModeButton_Click(object sender, RoutedEventArgs e)
        {
            DeployModePanel.Visibility = Visibility.Visible;
            UninstallModePanel.Visibility = Visibility.Collapsed;
            DeployModeBtn.Appearance = Wpf.Ui.Controls.ControlAppearance.Primary;
            UninstallModeBtn.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;
            ActionButton.Content = "Deploy to Selected PCs";
            ActionButton.Icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.Rocket24 };
            
            if (DataContext is SoftwareManagementViewModel vm)
            {
                vm.IsDeployMode = true;
            }
        }

        private void UninstallModeButton_Click(object sender, RoutedEventArgs e)
        {
            DeployModePanel.Visibility = Visibility.Collapsed;
            UninstallModePanel.Visibility = Visibility.Visible;
            DeployModeBtn.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;
            UninstallModeBtn.Appearance = Wpf.Ui.Controls.ControlAppearance.Primary;
            ActionButton.Content = "Uninstall from Selected PCs";
            ActionButton.Icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.Delete24 };
            
            if (DataContext is SoftwareManagementViewModel vm)
            {
                vm.IsDeployMode = false;
            }
        }

        private void FileDropZone_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (DataContext is SoftwareManagementViewModel vm)
                {
                    // Add files to the uploaded files collection
                    foreach (var file in files)
                    {
                        vm.UploadedFiles.Add(System.IO.Path.GetFileName(file));
                    }
                }
            }
        }

        private void FileDropZone_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void AppItem_Click(object sender, MouseButtonEventArgs e)
        {
            // Application selection is handled via data binding
        }

        private void PCItem_Click(object sender, MouseButtonEventArgs e)
        {
            // PC selection is handled via data binding
        }
    }
}
