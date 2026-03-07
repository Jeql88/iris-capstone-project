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
    public class AccessLogsViewModel : INotifyPropertyChanged, INavigationAware
    {
        private readonly IAccessLogsService _accessLogsService;
        private readonly SemaphoreSlim _loadLogsSemaphore = new(1, 1);
        private string _searchText = string.Empty;
        private string _selectedAction = "All Actions";
        private string _selectedRole = "All Roles";
        private int _currentPage = 1;
        private int _pageSize = 10;
        private int _totalPages = 1;
        private int _totalCount = 0;
        private bool _isActive = true;

        public AccessLogsViewModel(IAccessLogsService accessLogsService)
        {
            _accessLogsService = accessLogsService;
            RefreshCommand = new RelayCommand(async () => await LoadLogsAsync(), () => true);
            ApplyFiltersCommand = new RelayCommand(async () => await ApplyFiltersAsync(), () => true);
            ResetFiltersCommand = new RelayCommand(async () => await ResetFiltersAsync(), () => true);
            PreviousPageCommand = new RelayCommand(async () => await PreviousPageAsync(), () => true);
            NextPageCommand = new RelayCommand(async () => await NextPageAsync(), () => true);

            _ = LoadLogsAsync();
        }

        public ObservableCollection<AccessLogDisplayModel> AccessLogs { get; } = new();
        public List<int> PageSizeOptions { get; } = new() { 10, 25, 50 };

        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); }
        }

        public string SelectedAction
        {
            get => _selectedAction;
            set { _selectedAction = value; OnPropertyChanged(); }
        }

        public string SelectedRole
        {
            get => _selectedRole;
            set { _selectedRole = value; OnPropertyChanged(); }
        }

        public int PageSize
        {
            get => _pageSize;
            set { _pageSize = value; OnPropertyChanged(); }
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

        public string PageInfo => $"Page {CurrentPage} of {TotalPages} ({TotalCount} total entries)";
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;

        public ICommand RefreshCommand { get; }
        public ICommand ApplyFiltersCommand { get; }
        public ICommand ResetFiltersCommand { get; }
        public ICommand PreviousPageCommand { get; }
        public ICommand NextPageCommand { get; }

        private async Task ApplyFiltersAsync()
        {
            CurrentPage = 1;
            await LoadLogsAsync();
        }

        private async Task ResetFiltersAsync()
        {
            SearchText = string.Empty;
            SelectedAction = "All Actions";
            SelectedRole = "All Roles";
            PageSize = 10;
            CurrentPage = 1;
            await LoadLogsAsync();
        }

        private async Task LoadLogsAsync()
        {
            if (!_isActive)
            {
                return;
            }

            if (!await _loadLogsSemaphore.WaitAsync(0))
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

                var result = await _accessLogsService.GetAccessLogsAsync(
                    CurrentPage, PageSize, SearchText,
                    SelectedAction == "All Actions" ? null : SelectedAction,
                    roleFilter);

                AccessLogs.Clear();
                foreach (var log in result.Items)
                {
                    AccessLogs.Add(new AccessLogDisplayModel
                    {
                        Id = log.Id,
                        Timestamp = log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                        Username = log.User?.Username ?? "Unknown",
                        UserRole = log.User?.Role.ToString().Replace("SystemAdministrator", "System Administrator").Replace("ITPersonnel", "IT Personnel") ?? "Unknown",
                        Action = log.Action,
                        Details = log.Details ?? "N/A",
                        IpAddress = log.IpAddress ?? "N/A"
                    });
                }

                TotalCount = result.TotalCount;
                TotalPages = result.TotalPages;
                OnPropertyChanged(nameof(HasPreviousPage));
                OnPropertyChanged(nameof(HasNextPage));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load access logs: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _loadLogsSemaphore.Release();
            }
        }

        private async Task PreviousPageAsync()
        {
            if (HasPreviousPage)
            {
                CurrentPage--;
                await LoadLogsAsync();
            }
        }

        private async Task NextPageAsync()
        {
            if (HasNextPage)
            {
                CurrentPage++;
                await LoadLogsAsync();
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

    public class AccessLogDisplayModel : INotifyPropertyChanged
    {
        private int _id;
        private string _timestamp = string.Empty;
        private string _username = string.Empty;
        private string _userRole = string.Empty;
        private string _action = string.Empty;
        private string _details = string.Empty;
        private string _ipAddress = string.Empty;

        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public string Timestamp
        {
            get => _timestamp;
            set { _timestamp = value; OnPropertyChanged(); }
        }

        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); }
        }

        public string UserRole
        {
            get => _userRole;
            set { _userRole = value; OnPropertyChanged(); }
        }

        public string Action
        {
            get => _action;
            set { _action = value; OnPropertyChanged(); }
        }

        public string Details
        {
            get => _details;
            set { _details = value; OnPropertyChanged(); }
        }

        public string IpAddress
        {
            get => _ipAddress;
            set { _ipAddress = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}