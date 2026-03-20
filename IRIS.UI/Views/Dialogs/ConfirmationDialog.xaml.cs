using System.Windows;

namespace IRIS.UI.Views.Dialogs
{
    public partial class ConfirmationDialog : Window
    {
        public ConfirmationDialog(
            string title,
            string message,
            string iconSymbol = "Warning24",
            string confirmButtonText = "Confirm",
            string cancelButtonText = "Cancel",
            bool showCancelButton = true)
        {
            InitializeComponent();
            TitleTextBlock.Text = title;
            MessageTextBlock.Text = message;
            ConfirmButton.Content = confirmButtonText;
            CancelButton.Content = cancelButtonText;
            CancelButton.Visibility = showCancelButton ? Visibility.Visible : Visibility.Collapsed;
            ConfirmButton.Margin = showCancelButton ? new Thickness(0, 0, 8, 0) : new Thickness(0);
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
