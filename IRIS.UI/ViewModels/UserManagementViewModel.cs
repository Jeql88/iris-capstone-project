using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using IRIS.Core.Models;
using IRIS.Core.Services.Contracts;
using IRIS.UI.Helpers;
using IRIS.UI.Services;
using System.Threading;

namespace IRIS.UI.ViewModels
{
    public class UserManagementViewModel : INotifyPropertyChanged, INavigationAware
    {
        private readonly IUserManagementService _userManagementService;
        private readonly SemaphoreSlim _loadUsersSemaphore = new(1, 1);
        private string _searchText = string.Empty;
        private string _selectedRole = "All Roles";
        private string _selectedStatus = "All Status";
        private int _currentPage = 1;
        private int _pageSize = 10;
        private int _totalPages = 1;
        private int _totalCount = 0;
        private UserDisplayModel? _selectedUser;
        private bool _isActive = true;

        public UserManagementViewModel(IUserManagementService userManagementService)
        {
            _userManagementService = userManagementService;
            RefreshCommand = new RelayCommand(async () => await LoadUsersAsync(), () => true);
            PreviousPageCommand = new RelayCommand(async () => await PreviousPageAsync(), () => true);
            NextPageCommand = new RelayCommand(async () => await NextPageAsync(), () => true);
            
            _ = LoadUsersAsync();
        }

        public ObservableCollection<UserDisplayModel> Users { get; } = new();
        public List<int> PageSizeOptions { get; } = new() { 10, 25, 50 };

        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); CurrentPage = 1; _ = LoadUsersAsync(); }
        }

        public string SelectedRole
        {
            get => _selectedRole;
            set { _selectedRole = value; OnPropertyChanged(); CurrentPage = 1; _ = LoadUsersAsync(); }
        }

        public string SelectedStatus
        {
            get => _selectedStatus;
            set { _selectedStatus = value; OnPropertyChanged(); CurrentPage = 1; _ = LoadUsersAsync(); }
        }

        public int PageSize
        {
            get => _pageSize;
            set { _pageSize = value; OnPropertyChanged(); CurrentPage = 1; _ = LoadUsersAsync(); }
        }

        public UserDisplayModel? SelectedUser
        {
            get => _selectedUser;
            set { _selectedUser = value; OnPropertyChanged(); }
        }

        public int CurrentPage
        {
            get => _currentPage;
            set { _currentPage = value; OnPropertyChanged(); OnPropertyChanged(nameof(PageInfo)); }
        }

        public int TotalPages
        {
            get => _totalPages;
            set { _totalPages = value; OnPropertyChanged(); OnPropertyChanged(nameof(PageInfo)); }
        }

        public int TotalCount
        {
            get => _totalCount;
            set { _totalCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(PageInfo)); }
        }

        public string PageInfo => $"Page {CurrentPage} of {TotalPages} ({TotalCount} total users)";
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;

        public ICommand RefreshCommand { get; }
        public ICommand PreviousPageCommand { get; }
        public ICommand NextPageCommand { get; }

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
                if (SelectedRole != "All Roles")
                {
                    roleFilter = SelectedRole switch
                    {
                        "System Administrator" => UserRole.SystemAdministrator,
                        "IT Personnel" => UserRole.ITPersonnel,
                        "Faculty" => UserRole.Faculty,
                        _ => null
                    };
                }

                bool? statusFilter = null;
                if (SelectedStatus != "All Status")
                {
                    statusFilter = SelectedStatus == "Active";
                }

                var result = await _userManagementService.GetUsersAsync(
                    CurrentPage, PageSize, SearchText, roleFilter, statusFilter);

                Users.Clear();
                foreach (var user in result.Items)
                {
                    Users.Add(new UserDisplayModel
                    {
                        Id = user.Id,
                        Username = user.Username,
                        FullName = user.FullName ?? "N/A",
                        Role = user.Role.ToString().Replace("SystemAdministrator", "System Administrator").Replace("ITPersonnel", "IT Personnel"),
                        Status = user.IsActive ? "Active" : "Inactive",
                        IsActive = user.IsActive
                    });
                }

                TotalCount = result.TotalCount;
                TotalPages = result.TotalPages;
                OnPropertyChanged(nameof(HasPreviousPage));
                OnPropertyChanged(nameof(HasNextPage));
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
        private string _status = string.Empty;
        private bool _isActive;

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

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}