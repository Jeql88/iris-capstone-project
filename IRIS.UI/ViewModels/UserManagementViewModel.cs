using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using IRIS.Core.Models;
using IRIS.Core.Services.Contracts;
using IRIS.UI.Helpers;
using IRIS.UI.Services;
using IRIS.UI.Views.Dialogs;
using System.Threading;

namespace IRIS.UI.ViewModels
{
    public class UserManagementViewModel : INotifyPropertyChanged, INavigationAware
    {
        private readonly IUserManagementService _userManagementService;
        private readonly RelayCommand _previousPageRelayCommand;
        private readonly RelayCommand _nextPageRelayCommand;
        private readonly SemaphoreSlim _loadUsersSemaphore = new(1, 1);
        private string _searchText = string.Empty;
        private string _appliedSearchText = string.Empty;
        private string _selectedRole = "All Roles";
        private string _appliedRole = "All Roles";
        private int _currentPage = 1;
        private int _pageSize = 10;
        private int _totalPages = 1;
        private int _totalCount = 0;
        private bool _isActive = true;
        private bool _isAddModalOpen = false;
        private bool _isEditModalOpen = false;
        private int _editUserId = 0;
        private string _editFullName = string.Empty;
        private string _editUsername = string.Empty;
        private string _editRole = string.Empty;

        public UserManagementViewModel(IUserManagementService userManagementService)
        {
            _userManagementService = userManagementService;
            RefreshCommand = new RelayCommand(async () => await LoadUsersAsync(), () => true);
            ApplyFiltersCommand = new RelayCommand(async () => await ApplyFiltersAsync(), () => true);
            ResetFiltersCommand = new RelayCommand(async () => await ResetFiltersAsync(), () => true);
            _previousPageRelayCommand = new RelayCommand(async () => await PreviousPageAsync(), () => HasPreviousPage);
            _nextPageRelayCommand = new RelayCommand(async () => await NextPageAsync(), () => HasNextPage);
            PreviousPageCommand = _previousPageRelayCommand;
            NextPageCommand = _nextPageRelayCommand;
            OpenAddModalCommand = new RelayCommand(() => IsAddModalOpen = true, () => true);
            CloseAddModalCommand = new RelayCommand(() => IsAddModalOpen = false, () => true);
            OpenEditModalCommand = new RelayCommand<UserDisplayModel>(user => OpenEditModal(user), _ => true);
            CloseEditModalCommand = new RelayCommand(() => IsEditModalOpen = false, () => true);
            DeleteUserCommand = new RelayCommand<UserDisplayModel>(user => DeleteUserClick(user), _ => true);

            _ = LoadUsersAsync();
        }

        public ObservableCollection<UserDisplayModel> Users { get; } = new();
        public List<int> PageSizeOptions { get; } = new() { 10, 25, 50 };

        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); }
        }

        public string SelectedRole
        {
            get => _selectedRole;
            set { _selectedRole = value; OnPropertyChanged(); }
        }

        public int PageSize
        {
            get => _pageSize;
            set
            {
                if (_pageSize == value)
                {
                    return;
                }

                _pageSize = value;
                OnPropertyChanged();
                CurrentPage = 1;
                _ = LoadUsersAsync();
            }
        }

        public int CurrentPage
        {
            get => _currentPage;
            set
            {
                _currentPage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PageInfo));
                OnPropertyChanged(nameof(HasPreviousPage));
                OnPropertyChanged(nameof(HasNextPage));
                _previousPageRelayCommand.RaiseCanExecuteChanged();
                _nextPageRelayCommand.RaiseCanExecuteChanged();
            }
        }

        public int TotalPages
        {
            get => _totalPages;
            set
            {
                _totalPages = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PageInfo));
                OnPropertyChanged(nameof(HasPreviousPage));
                OnPropertyChanged(nameof(HasNextPage));
                _previousPageRelayCommand.RaiseCanExecuteChanged();
                _nextPageRelayCommand.RaiseCanExecuteChanged();
            }
        }

        public int TotalCount
        {
            get => _totalCount;
            set { _totalCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(PageInfo)); }
        }

        public string PageInfo => $"Page {CurrentPage} of {TotalPages} ({TotalCount} total entries)";
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;

        public bool IsAddModalOpen
        {
            get => _isAddModalOpen;
            set { _isAddModalOpen = value; OnPropertyChanged(); }
        }

        public bool IsEditModalOpen
        {
            get => _isEditModalOpen;
            set { _isEditModalOpen = value; OnPropertyChanged(); }
        }

        public int EditUserId
        {
            get => _editUserId;
            set { _editUserId = value; OnPropertyChanged(); }
        }

        public string EditFullName
        {
            get => _editFullName;
            set { _editFullName = value; OnPropertyChanged(); }
        }

        public string EditUsername
        {
            get => _editUsername;
            set { _editUsername = value; OnPropertyChanged(); }
        }

        public string EditRole
        {
            get => _editRole;
            set { _editRole = value; OnPropertyChanged(); }
        }

        public ICommand RefreshCommand { get; }
        public ICommand ApplyFiltersCommand { get; }
        public ICommand ResetFiltersCommand { get; }
        public ICommand PreviousPageCommand { get; }
        public ICommand NextPageCommand { get; }
        public ICommand OpenAddModalCommand { get; }
        public ICommand CloseAddModalCommand { get; }
        public ICommand OpenEditModalCommand { get; }
        public ICommand CloseEditModalCommand { get; }
        public ICommand DeleteUserCommand { get; }

        private async Task ApplyFiltersAsync()
        {
            _appliedSearchText = SearchText?.Trim() ?? string.Empty;
            _appliedRole = SelectedRole;
            CurrentPage = 1;
            await LoadUsersAsync();
        }

        private async Task ResetFiltersAsync()
        {
            SearchText = string.Empty;
            SelectedRole = "All Roles";
            _appliedSearchText = string.Empty;
            _appliedRole = "All Roles";
            PageSize = 10;
            CurrentPage = 1;
            await LoadUsersAsync();
        }

        private async Task LoadUsersAsync()
        {
            if (!_isActive)
            {
                return;
            }

            if (!await _loadUsersSemaphore.WaitAsync(0))
            {
                return;
            }

            try
            {
                if (!_isActive)
                {
                    return;
                }

                UserRole? roleFilter = null;
                if (_appliedRole != "All Roles")
                {
                    roleFilter = _appliedRole switch
                    {
                        "System Administrator" => UserRole.SystemAdministrator,
                        "IT Personnel" => UserRole.ITPersonnel,
                        "Faculty" => UserRole.Faculty,
                        _ => null
                    };
                }

                var result = await _userManagementService.GetUsersAsync(
                    CurrentPage, PageSize, _appliedSearchText, roleFilter);

                Users.Clear();
                foreach (var user in result.Items)
                {
                    Users.Add(new UserDisplayModel
                    {
                        Id = user.Id,
                        Username = user.Username,
                        FullName = user.FullName ?? "N/A",
                        Role = user.Role.ToString().Replace("SystemAdministrator", "System Administrator").Replace("ITPersonnel", "IT Personnel")
                    });
                }

                TotalCount = result.TotalCount;
                TotalPages = result.TotalPages;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load users: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _loadUsersSemaphore.Release();
            }
        }

        private async Task PreviousPageAsync()
        {
            if (HasPreviousPage)
            {
                CurrentPage--;
                await LoadUsersAsync();
            }
        }

        private void OpenEditModal(UserDisplayModel user)
        {
            if (user != null)
            {
                EditUserId = user.Id;
                EditFullName = user.FullName;
                EditUsername = user.Username;
                EditRole = user.Role;
                IsEditModalOpen = true;
            }
        }

        private void DeleteUserClick(UserDisplayModel user)
        {
            if (user == null) return;

            var deleteDialog = new ConfirmationDialog(
                "Delete User",
                $"Are you sure you want to delete user '{user.Username}'?\n\nThis action cannot be undone.",
                "Delete24");
            deleteDialog.Owner = Application.Current.MainWindow;

            if (deleteDialog.ShowDialog() != true) return;

            _ = DeleteUserAsync(user);
        }

        private async Task DeleteUserAsync(UserDisplayModel user)
        {
            if (user == null) return;

            try
            {
                await _userManagementService.DeleteUserAsync(user.Id);
                await LoadUsersAsync();

                var deleteSuccessDialog = new ConfirmationDialog(
                    "User Deleted",
                    $"User '{user.Username}' deleted successfully.",
                    "Checkmark24",
                    "OK",
                    "Cancel",
                    false);
                deleteSuccessDialog.Owner = Application.Current.MainWindow;
                deleteSuccessDialog.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete user: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task NextPageAsync()
        {
            if (HasNextPage)
            {
                CurrentPage++;
                await LoadUsersAsync();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public void OnNavigatedTo()
        {
            _isActive = true;
            _ = LoadUsersAsync();
        }

        public void OnNavigatedFrom()
        {
            _isActive = false;
        }
    }

    public class UserDisplayModel : INotifyPropertyChanged
    {
        private int _id;
        private string _username = string.Empty;
        private string _fullName = string.Empty;
        private string _role = string.Empty;

        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); }
        }

        public string FullName
        {
            get => _fullName;
            set { _fullName = value; OnPropertyChanged(); }
        }

        public string Role
        {
            get => _role;
            set { _role = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}