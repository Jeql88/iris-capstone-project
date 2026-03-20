using System.Windows;

namespace IRIS.UI.Views.Faculty
{
    public partial class ExpandedScreenWindow : Window
    {
        public ExpandedScreenWindow()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        }
    }
}
