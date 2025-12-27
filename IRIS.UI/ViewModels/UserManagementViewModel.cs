using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using IRIS.Core.Models;
using IRIS.Core.Services;
using IRIS.UI.Helpers;

namespace IRIS.UI.ViewModels
{
    public class UserManagementViewModel : INotifyPropertyChanged
    {
        private readonly IUserManagementService _userService;
        private UserDisplayModel? _selectedUser;
        private string _searchText = string.Empty;
        private string _selectedRole = "All Roles";
        private string _selectedStatus = "All Status";

        public UserManagementViewModel(IUserManagementService userService)
        {
            _userService = userService;
            RefreshCommand = new RelayCommand(async () => await LoadUsersAsync(), () => true);
            SearchCommand = new RelayCommand(async () => await FilterUsersAsync(), () => true);
            
            _ = LoadUsersAsync();
        }

        public ObservableCollection<UserDisplayModel> Users { get; } = new();
        public ObservableCollection<UserDisplayModel> FilteredUsers { get; } = new();

        public UserDisplayModel? SelectedUser
        {
            get => _selectedUser;
            set { _selectedUser = value; OnPropertyChanged(); }
        }

        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); _ = FilterUsersAsync(); }
        }

        public string SelectedRole
        {
            get => _selectedRole;
            set 
            { 
                _selectedRole = value; 
                OnPropertyChanged(); 
                _ = FilterUsersAsync(); 
            }
        }

        public string SelectedStatus
        {
            get => _selectedStatus;
            set { _selectedStatus = value; OnPropertyChanged(); _ = FilterUsersAsync(); }
        }

        public ICommand RefreshCommand { get; }
        public ICommand SearchCommand { get; }

        private async Task LoadUsersAsync()
        {
            try
            {
                var users = await _userService.GetAllUsersAsync();
                Users.Clear();
                foreach (var user in users)
                {
                    Users.Add(new UserDisplayModel
                    {
                        Id = user.Id,
                        Username = user.Username,
                        FullName = user.FullName ?? "N/A",
                        Role = user.Role.ToString().Replace("SystemAdministrator", "System Administrator").Replace("ITPersonnel", "IT Personnel"),
                        IsActive = user.IsActive,
                        Status = user.IsActive ? "Active" : "Inactive",
                        LastLogin = user.LastLoginAt?.ToString("yyyy-MM-dd HH:mm") ?? "Never"
                    });
                }
                await FilterUsersAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load users: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task FilterUsersAsync()
        {
            await Task.CompletedTask;
            
            FilteredUsers.Clear();
            var filtered = Users.AsEnumerable();

            // Filter by search text
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                filtered = filtered.Where(u => 
                    u.Username.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    u.FullName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }

            // Filter by role
            if (SelectedRole != "All Roles")
            {
                filtered = filtered.Where(u => u.Role == SelectedRole);
            }

            // Filter by status
            if (SelectedStatus != "All Status")
            {
                filtered = filtered.Where(u => u.Status == SelectedStatus);
            }

            foreach (var user in filtered)
            {
                FilteredUsers.Add(user);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class UserDisplayModel : INotifyPropertyChanged
    {
        private int _id;
        private string _username = string.Empty;
        private string _fullName = string.Empty;
        private string _role = string.Empty;
        private bool _isActive;
        private string _status = string.Empty;
        private string _lastLogin = string.Empty;

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

        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public string LastLogin
        {
            get => _lastLogin;
            set { _lastLogin = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
