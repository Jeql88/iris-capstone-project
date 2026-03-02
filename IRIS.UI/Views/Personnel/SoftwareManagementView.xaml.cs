using System.Windows;
using System.Windows.Controls;
using IRIS.UI.ViewModels;

namespace IRIS.UI.Views.Personnel
{
    public partial class SoftwareManagementView : UserControl
    {
        public SoftwareManagementView(DeploymentViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void FileDropZone_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (DataContext is DeploymentViewModel vm)
                {
                    vm.AddPendingUploadFiles(files);
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
    }
}
