using System.Windows;

namespace IRIS.UI.Views.Dialogs
{
    public partial class FreezeMessageDialog : Window
    {
        public const string DefaultFreezeMessage = "This PC is temporarily frozen by the administrator.";

        public FreezeMessageDialog(string title, string instruction, string? defaultMessage = null)
        {
            InitializeComponent();
            Title = title;
            TitleTextBlock.Text = title;
            MessageTextBlock.Text = instruction;
            FreezeMessageTextBox.Text = string.IsNullOrWhiteSpace(defaultMessage)
                ? DefaultFreezeMessage
                : defaultMessage;
            FreezeMessageTextBox.Focus();
            FreezeMessageTextBox.SelectAll();
        }

        public string FreezeMessage => string.IsNullOrWhiteSpace(FreezeMessageTextBox.Text)
            ? DefaultFreezeMessage
            : FreezeMessageTextBox.Text.Trim();

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