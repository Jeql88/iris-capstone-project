using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using IRIS.Core.Models;
using IRIS.Core.Services.Contracts;
using IRIS.UI.Helpers;
using IRIS.UI.Services;
using IRIS.UI.Views.Dialogs;
using Microsoft.Win32;
using System.Threading;

namespace IRIS.UI.ViewModels
{
    public class AccessLogsViewModel : INotifyPropertyChanged, INavigationAware
    {
        private readonly IAccessLogsService _accessLogsService;
        private readonly RelayCommand _previousPageRelayCommand;
        private readonly RelayCommand _nextPageRelayCommand;
        private readonly SemaphoreSlim _loadLogsSemaphore = new(1, 1);
        private string _searchText = string.Empty;
        private string _selectedRole = "All Roles";
        private DateTime? _startDate;
        private DateTime? _endDate;
        private DateTime? _appliedStartDate;
        private DateTime? _appliedEndDate;
        private int _currentPage = 1;
        private int _pageSize = 10;
        private int _totalPages = 1;
        private int _totalCount = 0;
        private bool _isActive = true;

        public AccessLogsViewModel(IAccessLogsService accessLogsService)
        {
            _accessLogsService = accessLogsService;
            RefreshCommand = new RelayCommand(async () => await LoadLogsAsync(), () => true);
            ExportCommand = new RelayCommand(async () => await ExportLogsAsync(), () => true);
            ApplyFiltersCommand = new RelayCommand(async () => await ApplyFiltersAsync(), () => true);
            ResetFiltersCommand = new RelayCommand(async () => await ResetFiltersAsync(), () => true);
            _previousPageRelayCommand = new RelayCommand(async () => await PreviousPageAsync(), () => HasPreviousPage);
            _nextPageRelayCommand = new RelayCommand(async () => await NextPageAsync(), () => HasNextPage);
            PreviousPageCommand = _previousPageRelayCommand;
            NextPageCommand = _nextPageRelayCommand;

            _ = LoadLogsAsync();
        }

        public ObservableCollection<AccessLogDisplayModel> AccessLogs { get; } = new();
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

        public DateTime? StartDate
        {
            get => _startDate;
            set { _startDate = value; OnPropertyChanged(); }
        }

        public DateTime? EndDate
        {
            get => _endDate;
            set { _endDate = value; OnPropertyChanged(); }
        }

        public int PageSize
        {
            get => _pageSize;
            set { _pageSize = value; OnPropertyChanged(); }
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

        public ICommand RefreshCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand ApplyFiltersCommand { get; }
        public ICommand ResetFiltersCommand { get; }
        public ICommand PreviousPageCommand { get; }
        public ICommand NextPageCommand { get; }

        private async Task ApplyFiltersAsync()
        {
            if (StartDate.HasValue && EndDate.HasValue && EndDate.Value.Date < StartDate.Value.Date)
            {
                MessageBox.Show(
                    "'To' date cannot be earlier than 'From' date.",
                    "Invalid Date Range",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            _appliedStartDate = StartDate?.Date;
            _appliedEndDate = EndDate?.Date;
            CurrentPage = 1;
            await LoadLogsAsync();
        }

        private async Task ResetFiltersAsync()
        {
            SearchText = string.Empty;
            SelectedRole = "All Roles";
            StartDate = null;
            EndDate = null;
            _appliedStartDate = null;
            _appliedEndDate = null;
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

                UserRole? roleFilter = GetRoleFilter();

                var result = await _accessLogsService.GetAccessLogsAsync(
                    CurrentPage, PageSize, SearchText,
                    null,
                    roleFilter,
                    _appliedStartDate.HasValue ? DateTime.SpecifyKind(_appliedStartDate.Value, DateTimeKind.Utc) : null,
                    _appliedEndDate.HasValue ? DateTime.SpecifyKind(_appliedEndDate.Value.AddDays(1).AddSeconds(-1), DateTimeKind.Utc) : null);

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

        private async Task ExportLogsAsync()
        {
            if (StartDate.HasValue && EndDate.HasValue && EndDate.Value.Date < StartDate.Value.Date)
            {
                MessageBox.Show(
                    "'To' date cannot be earlier than 'From' date.",
                    "Invalid Date Range",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var hasFilters = !string.IsNullOrWhiteSpace(SearchText)
                             || SelectedRole != "All Roles"
                             || StartDate.HasValue
                             || EndDate.HasValue;

            var confirmationMessage = hasFilters
                ? "This will export all access logs that match the full filter set (Search, Role, From, and To), not just the current page. Continue?"
                : "No filters are set. This will export all access logs, not just the current page. Continue?";

            var confirmationDialog = new ConfirmationDialog(
                "Export Access Logs",
                confirmationMessage,
                "ArrowDownload24");
            confirmationDialog.Owner = Application.Current.MainWindow;

            if (confirmationDialog.ShowDialog() != true)
            {
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                DefaultExt = "xlsx",
                FileName = $"IRIS_AccessLogs_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            };

            if (saveDialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                var roleFilter = GetRoleFilter();
                var startDate = StartDate?.Date;
                var endDate = EndDate?.Date.AddDays(1).AddSeconds(-1);

                var bytes = await _accessLogsService.ExportAccessLogsToExcelAsync(
                    SearchText,
                    null,
                    roleFilter,
                    startDate.HasValue ? DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc) : null,
                    endDate.HasValue ? DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc) : null);

                await File.WriteAllBytesAsync(saveDialog.FileName, bytes);
                MessageBox.Show("Access logs were exported to an Excel file.", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export access logs: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private UserRole? GetRoleFilter()
        {
            if (SelectedRole == "All Roles")
            {
                return null;
            }

            return SelectedRole switch
            {
                "System Administrator" => UserRole.SystemAdministrator,
                "IT Personnel" => UserRole.ITPersonnel,
                "Faculty" => UserRole.Faculty,
                _ => null
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public void OnNavigatedTo()
        {
            _isActive = true;
            _ = LoadLogsAsync();
        }

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