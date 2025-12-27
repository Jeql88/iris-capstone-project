using System.Windows.Controls;
using IRIS.UI.ViewModels;

namespace IRIS.UI.Views
{
    public partial class UserManagementView : UserControl
    {
        private UserManagementViewModel? _viewModel;

        public UserManagementView(UserManagementViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            _viewModel = viewModel;
            
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(UserManagementViewModel.SelectedUser))
            {
                if (_viewModel?.SelectedUser != null)
                {
                    // Enable fields and populate
                    EditFullNameTextBox.IsEnabled = true;
                    EditUsernameTextBox.IsEnabled = true;
                    EditRoleComboBox.IsEnabled = true;
                    EditUserButton.IsEnabled = true;
                    DeactivateUserButton.IsEnabled = true;
                    ClearSelectionButton.Visibility = System.Windows.Visibility.Visible;
                    HelpIcon.Visibility = System.Windows.Visibility.Collapsed;
                    
                    EditFullNameTextBox.Text = _viewModel.SelectedUser.FullName;
                    EditUsernameTextBox.Text = _viewModel.SelectedUser.Username;
                    
                    foreach (ComboBoxItem item in EditRoleComboBox.Items)
                    {
                        if (item.Content.ToString() == _viewModel.SelectedUser.Role)
                        {
                            EditRoleComboBox.SelectedItem = item;
                            break;
                        }
                    }
                }
                else
                {
                    // Disable fields and clear
                    EditFullNameTextBox.IsEnabled = false;
                    EditUsernameTextBox.IsEnabled = false;
                    EditRoleComboBox.IsEnabled = false;
                    EditUserButton.IsEnabled = false;
                    DeactivateUserButton.IsEnabled = false;
                    ClearSelectionButton.Visibility = System.Windows.Visibility.Collapsed;
                    HelpIcon.Visibility = System.Windows.Visibility.Visible;
                    
                    EditFullNameTextBox.Text = string.Empty;
                    EditUsernameTextBox.Text = string.Empty;
                    EditRoleComboBox.SelectedIndex = -1;
                }
            }
        }

        private void ClearSelection_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.SelectedUser = null;
            }
            EditFullNameTextBox.Text = string.Empty;
            EditUsernameTextBox.Text = string.Empty;
            EditRoleComboBox.SelectedIndex = -1;
        }
    }
}