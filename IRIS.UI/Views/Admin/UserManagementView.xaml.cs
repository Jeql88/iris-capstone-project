using System.Windows;
using System.Windows.Controls;
using IRIS.Core.Models;
using IRIS.Core.Services.Contracts;
using IRIS.UI.ViewModels;
using IRIS.UI.Views.Dialogs;

namespace IRIS.UI.Views.Admin
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
        }

        private void UsersDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is DataGrid dataGrid && dataGrid.SelectedItem != null)
            {
                dataGrid.SelectedItem = null;
            }
        }

        private async void AddUser_Click(object sender, RoutedEventArgs e)
        {
            var username = AddUsernameTextBox.Text.Trim();
            var fullName = AddFullNameTextBox.Text.Trim();
            var roleItem = AddRoleComboBox.SelectedItem as ComboBoxItem;

            if (string.IsNullOrWhiteSpace(username) && string.IsNullOrWhiteSpace(fullName))
            {
                MessageBox.Show("Username and Full Name are required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                MessageBox.Show("Username is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(fullName))
            {
                MessageBox.Show("Full Name is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
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

            var createDialog = new ConfirmationDialog(
                "Create User",
                $"Are you sure you want to create user '{username}'?",
                "Add24");
            createDialog.Owner = Application.Current.MainWindow;

            if (createDialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                await _userService.CreateUserAsync(username, defaultPassword, role, fullName);

                MessageBox.Show($"User '{username}' created successfully!\n\nDefault Password: {defaultPassword}\n\nUser must change password on first login.",
                    "User Created", MessageBoxButton.OK, MessageBoxImage.Information);

                AddUsernameTextBox.Clear();
                AddFullNameTextBox.Clear();
                AddRoleComboBox.SelectedIndex = 2;

                _viewModel!.RefreshCommand.Execute(null);
                _viewModel.IsAddModalOpen = false;
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
            var username = _viewModel!.EditUsername.Trim();
            var fullName = _viewModel.EditFullName.Trim();
            var role = _viewModel.EditRole;

            if (string.IsNullOrWhiteSpace(username))
            {
                MessageBox.Show("Username is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(fullName))
            {
                MessageBox.Show("Full Name is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(role))
            {
                MessageBox.Show("Please select a role.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var userRole = role switch
            {
                "System Administrator" => UserRole.SystemAdministrator,
                "IT Personnel" => UserRole.ITPersonnel,
                "Faculty" => UserRole.Faculty,
                _ => UserRole.Faculty
            };

            var updateDialog = new ConfirmationDialog(
                "Update User",
                $"Are you sure you want to update user '{username}'?",
                "Edit24");
            updateDialog.Owner = Application.Current.MainWindow;

            if (updateDialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                await _userService.UpdateUserAsync(_viewModel.EditUserId, username, fullName, userRole);

                MessageBox.Show("User updated successfully!",
                    "User Updated", MessageBoxButton.OK, MessageBoxImage.Information);

                _viewModel.RefreshCommand.Execute(null);
                _viewModel.IsEditModalOpen = false;
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
    }
}