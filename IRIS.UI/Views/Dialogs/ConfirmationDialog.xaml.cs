using System.Windows;

namespace IRIS.UI.Views.Dialogs
{
    public partial class ConfirmationDialog : Window
    {
        public ConfirmationDialog(string title, string message, string iconSymbol = "Warning24")
        {
            InitializeComponent();
            TitleTextBlock.Text = title;
            MessageTextBlock.Text = message;
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
