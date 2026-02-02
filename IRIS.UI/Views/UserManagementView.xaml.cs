using System.Windows;
using System.Windows.Controls;
using IRIS.Core.Models;
using IRIS.Core.Services;
using IRIS.UI.ViewModels;

namespace IRIS.UI.Views
{
    public partial class UserManagementView : UserControl
    {
        private UserManagementViewModel? _viewModel;
        private readonly IUserManagementService _userService;

        public UserManagementView(UserManagementViewModel viewModel, IUserManagementService userService)
        {
            InitializeComponent();
            DataContext = viewModel;
            _viewModel = viewModel;
            _userService = userService;
            
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

        private async void AddUser_Click(object sender, RoutedEventArgs e)
        {
            var username = AddUsernameTextBox.Text.Trim();
            var fullName = AddFullNameTextBox.Text.Trim();
            var roleItem = AddRoleComboBox.SelectedItem as ComboBoxItem;

            if (string.IsNullOrWhiteSpace(username))
            {
                MessageBox.Show("Username is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (roleItem == null)
            {
                MessageBox.Show("Please select a role.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var role = roleItem.Content.ToString() switch
            {
                "System Administrator" => UserRole.SystemAdministrator,
                "IT Personnel" => UserRole.ITPersonnel,
                "Faculty" => UserRole.Faculty,
                _ => UserRole.Faculty
            };

            const string defaultPassword = "IRIS@2025";

            try
            {
                await _userService.CreateUserAsync(username, defaultPassword, role, string.IsNullOrWhiteSpace(fullName) ? null : fullName);

                MessageBox.Show($"User '{username}' created successfully!\n\nDefault Password: {defaultPassword}\n\nUser must change password on first login.",
                    "User Created", MessageBoxButton.OK, MessageBoxImage.Information);

                AddUsernameTextBox.Clear();
                AddFullNameTextBox.Clear();
                AddRoleComboBox.SelectedIndex = 2;

                _viewModel!.RefreshCommand.Execute(null);
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to create user: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void EditUser_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel?.SelectedUser == null) return;

            var username = EditUsernameTextBox.Text.Trim();
            var fullName = EditFullNameTextBox.Text.Trim();
            var roleItem = EditRoleComboBox.SelectedItem as ComboBoxItem;

            if (string.IsNullOrWhiteSpace(username))
            {
                MessageBox.Show("Username is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (roleItem == null)
            {
                MessageBox.Show("Please select a role.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var role = roleItem.Content.ToString() switch
            {
                "System Administrator" => UserRole.SystemAdministrator,
                "IT Personnel" => UserRole.ITPersonnel,
                "Faculty" => UserRole.Faculty,
                _ => UserRole.Faculty
            };

            try
            {
                await _userService.UpdateUserAsync(
                    _viewModel.SelectedUser.Id,
                    username,
                    string.IsNullOrWhiteSpace(fullName) ? null : fullName,
                    role
                );

                MessageBox.Show($"User updated successfully!",
                    "User Updated", MessageBoxButton.OK, MessageBoxImage.Information);

                _viewModel.RefreshCommand.Execute(null);
                _viewModel.SelectedUser = null;
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to update user: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void DeactivateUser_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel?.SelectedUser == null) return;

            var result = MessageBox.Show(
                $"Are you sure you want to deactivate user '{_viewModel.SelectedUser.Username}'?\n\nThis user will no longer be able to log in.",
                "Confirm Deactivation", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _userService.DeleteUserAsync(_viewModel.SelectedUser.Id);

                MessageBox.Show($"User '{_viewModel.SelectedUser.Username}' deactivated successfully!",
                    "User Deactivated", MessageBoxButton.OK, MessageBoxImage.Information);

                _viewModel.RefreshCommand.Execute(null);
                _viewModel.SelectedUser = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to deactivate user: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}