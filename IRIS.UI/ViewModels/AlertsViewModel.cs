using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using System.Windows.Threading;
using IRIS.Core.DTOs;
using IRIS.Core.Services.Contracts;
using IRIS.Core.Models;
using IRIS.UI.Helpers;
using IRIS.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace IRIS.UI.ViewModels
{
    public class AlertsViewModel : INotifyPropertyChanged, INavigationAware
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IAuthenticationService _authenticationService;
        private readonly DispatcherTimer _refreshTimer;
        private readonly SemaphoreSlim _loadSemaphore = new(1, 1);
        private bool _isActive = true;
        private RoomDto? _selectedRoom;
        private string _severityFilter = "All";
        private string _typeFilter = "All";
        private string _stateFilter = "Open";
        private string _searchText = string.Empty;
        private RoomDto? _appliedRoom;
        private string _appliedSeverityFilter = "All";
        private string _appliedTypeFilter = "All";
        private string _appliedStateFilter = "Open";
        private string _appliedSearchText = string.Empty;
        private bool _isLoading;
        private DateTime _lastUpdatedUtc = DateTime.MinValue;
        private AlertRow? _selectedAlert;
        private bool _isAllSelected;
        private int _pageSize = 10;
        private int _currentPage = 1;
        private int _totalPages = 1;

        public AlertsViewModel(IServiceScopeFactory scopeFactory, IAuthenticationService authenticationService)
        {
            _scopeFactory = scopeFactory;
            _authenticationService = authenticationService;
            RefreshCommand = new RelayCommand(async () => await LoadAlertsAsync(), () => true);
            ExportCommand = new RelayCommand(async () => await ExportCsvAsync(), () => true);
            AcknowledgeCommand = new RelayCommand(async () => await AcknowledgeSelectedAsync(), () => true);
            ResolveCommand = new RelayCommand(async () => await ResolveSelectedAsync(), () => true);
            AcknowledgeVisibleCommand = new RelayCommand(async () => await AcknowledgeVisibleAsync(), () => FilteredAlerts.Any(a => !a.IsAcknowledged && !a.IsResolved));
            ResolveVisibleCommand = new RelayCommand(async () => await ResolveVisibleAsync(), () => FilteredAlerts.Any(a => !a.IsResolved));
            ToggleSelectAllCommand = new RelayCommand(ToggleSelectAll, () => true);
            ApplyFiltersCommand = new RelayCommand(async () => await ApplyFiltersAsync(), () => true);
            ResetFiltersCommand = new RelayCommand(async () => await ResetFiltersAsync(), () => true);
            NextPageCommand = new RelayCommand(() => GoToPage(_currentPage + 1), () => _currentPage < _totalPages);
            PreviousPageCommand = new RelayCommand(() => GoToPage(_currentPage - 1), () => _currentPage > 1);
            FirstPageCommand = new RelayCommand(() => GoToPage(1), () => _currentPage > 1);
            LastPageCommand = new RelayCommand(() => GoToPage(_totalPages), () => _currentPage < _totalPages);

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _refreshTimer.Tick += async (_, _) => await HandleAutoRefreshTickAsync();

            _ = InitializeAsync();
        }

        public ObservableCollection<RoomDto> Rooms { get; } = new();
        public ObservableCollection<AlertRow> Alerts { get; } = new();
        public ObservableCollection<AlertRow> FilteredAlerts { get; } = new();
        public ObservableCollection<AlertRow> PagedAlerts { get; } = new();
        public string[] SeverityOptions { get; } = { "All", "Critical", "High", "Medium", "Low" };
        public string[] TypeOptions { get; } = { "All", "Hardware", "Network", "Thermal", "System" };
        public string[] StateOptions { get; } = { "Open", "Resolved", "All" };
        public int[] PageSizeOptions { get; } = { 10, 25, 50, 100 };

        public RoomDto? SelectedRoom
        {
            get => _selectedRoom;
            set
            {
                _selectedRoom = value;
                OnPropertyChanged();
            }
        }

        public string SeverityFilter
        {
            get => _severityFilter;
            set
            {
                _severityFilter = value;
                OnPropertyChanged();
            }
        }

        public string TypeFilter
        {
            get => _typeFilter;
            set
            {
                _typeFilter = value;
                OnPropertyChanged();
            }
        }

        public string StateFilter
        {
            get => _stateFilter;
            set
            {
                _stateFilter = value;
                OnPropertyChanged();
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        public string LastUpdatedText => _lastUpdatedUtc == DateTime.MinValue
            ? "Not yet updated"
            : $"Updated {TimeZoneInfo.ConvertTimeFromUtc(_lastUpdatedUtc, TimeZoneInfo.Local):HH:mm:ss}";

        public int AlertCount => FilteredAlerts.Count;
        public int CriticalCount => FilteredAlerts.Count(a => a.Severity == "Critical");
        public int HighCount => FilteredAlerts.Count(a => a.Severity == "High");
        public int OpenCount => FilteredAlerts.Count(a => !a.IsResolved);

        public AlertRow? SelectedAlert
        {
            get => _selectedAlert;
            set
            {
                _selectedAlert = value;
                OnPropertyChanged();
                (AcknowledgeCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (ResolveCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public ICommand RefreshCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand AcknowledgeCommand { get; }
        public ICommand ResolveCommand { get; }
        public ICommand AcknowledgeVisibleCommand { get; }
        public ICommand ResolveVisibleCommand { get; }
        public ICommand ToggleSelectAllCommand { get; }
        public ICommand ApplyFiltersCommand { get; }
        public ICommand ResetFiltersCommand { get; }
        public ICommand NextPageCommand { get; }
        public ICommand PreviousPageCommand { get; }
        public ICommand FirstPageCommand { get; }
        public ICommand LastPageCommand { get; }

        public int PageSize
        {
            get => _pageSize;
            set
            {
                if (_pageSize != value)
                {
                    _pageSize = value;
                    OnPropertyChanged();
                    GoToPage(1);
                }
            }
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

        public string PageInfo => $"Page {CurrentPage} of {TotalPages} ({FilteredAlerts.Count} total entries)";

        public bool IsAllSelected
        {
            get => _isAllSelected;
            set
            {
                if (_isAllSelected == value)
                {
                    return;
                }

                _isAllSelected = value;
                foreach (var alert in PagedAlerts)
                {
                    alert.IsSelected = value;
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedCount));
            }
        }

        public int SelectedCount => FilteredAlerts.Count(a => a.IsSelected);

        private void ToggleSelectAll()
        {
            IsAllSelected = !IsAllSelected;
        }

        private async Task ApplyFiltersAsync()
        {
            _appliedRoom = SelectedRoom;
            _appliedSeverityFilter = SeverityFilter;
            _appliedTypeFilter = TypeFilter;
            _appliedStateFilter = StateFilter;
            _appliedSearchText = SearchText;

            await LoadAlertsAsync();
        }

        private async Task ResetFiltersAsync()
        {
            SelectedRoom = Rooms.FirstOrDefault();
            SeverityFilter = "All";
            TypeFilter = "All";
            StateFilter = "Open";
            SearchText = string.Empty;

            _appliedRoom = SelectedRoom;
            _appliedSeverityFilter = SeverityFilter;
            _appliedTypeFilter = TypeFilter;
            _appliedStateFilter = StateFilter;
            _appliedSearchText = SearchText;

            await LoadAlertsAsync();
        }

        private async Task InitializeAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var monitoringService = scope.ServiceProvider.GetRequiredService<IMonitoringService>();
            var rooms = await monitoringService.GetRoomsAsync();
            Rooms.Clear();
            Rooms.Add(new RoomDto(-1, "All Rooms", "", 0, true, DateTime.UtcNow));
            foreach (var room in rooms)
            {
                Rooms.Add(room);
            }

            if (SelectedRoom == null)
            {
                SelectedRoom = Rooms.FirstOrDefault();
            }

            _appliedRoom = SelectedRoom;
            _appliedSeverityFilter = SeverityFilter;
            _appliedTypeFilter = TypeFilter;
            _appliedStateFilter = StateFilter;
            _appliedSearchText = SearchText;

            await LoadAlertsAsync();
            _refreshTimer.Start();
        }

        private async Task HandleAutoRefreshTickAsync()
        {
            if (FilteredAlerts.Any(a => a.IsSelected))
            {
                return;
            }

            await LoadAlertsAsync(preserveCurrentPage: true);
        }

        private async Task LoadAlertsAsync(bool preserveCurrentPage = false)
        {
            if (!_isActive)
            {
                return;
            }

            if (!await _loadSemaphore.WaitAsync(0))
            {
                return;
            }

            IsLoading = true;
            try
            {
                if (!_isActive)
                {
                    return;
                }

                int? roomId = _appliedRoom != null && _appliedRoom.Id > 0 ? _appliedRoom.Id : null;
                var includeResolved = string.Equals(_appliedStateFilter, "Resolved", StringComparison.OrdinalIgnoreCase) ||
                                      string.Equals(_appliedStateFilter, "All", StringComparison.OrdinalIgnoreCase);

                using var scope = _scopeFactory.CreateScope();
                var monitoringService = scope.ServiceProvider.GetRequiredService<IMonitoringService>();
                var alerts = await monitoringService.GetAlertFeedAsync(roomId, 300, includeResolved);

                Alerts.Clear();
                foreach (var alert in alerts.OrderByDescending(a => a.CreatedAt))
                {
                    Alerts.Add(new AlertRow
                    {
                        AlertId = alert.AlertId,
                        AlertKey = alert.AlertKey,
                        Timestamp = alert.CreatedAt,
                        Severity = alert.Severity,
                        Type = alert.Type,
                        PCName = alert.PCName,
                        RoomName = alert.RoomName,
                        Message = alert.Message,
                        IsAcknowledged = alert.IsAcknowledged,
                        AcknowledgedAt = alert.AcknowledgedAt,
                        IsResolved = alert.IsResolved,
                        ResolvedAt = alert.ResolvedAt
                    });
                }

                _lastUpdatedUtc = DateTime.UtcNow;
                OnPropertyChanged(nameof(LastUpdatedText));
                ApplyFilters(preserveCurrentPage);
            }
            finally
            {
                IsLoading = false;
                _loadSemaphore.Release();
            }
        }

        private void ApplyFilters(bool preserveCurrentPage = false)
        {
            var filtered = Alerts.AsEnumerable();

            if (!string.Equals(_appliedSeverityFilter, "All", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(a => a.Severity.Equals(_appliedSeverityFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.Equals(_appliedTypeFilter, "All", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(a => a.Type.Equals(_appliedTypeFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (string.Equals(_appliedStateFilter, "Open", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(a => !a.IsResolved);
            }
            else if (string.Equals(_appliedStateFilter, "Resolved", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(a => a.IsResolved);
            }

            if (!string.IsNullOrWhiteSpace(_appliedSearchText))
            {
                filtered = filtered.Where(a =>
                    a.PCName.Contains(_appliedSearchText, StringComparison.OrdinalIgnoreCase) ||
                    a.RoomName.Contains(_appliedSearchText, StringComparison.OrdinalIgnoreCase) ||
                    a.Message.Contains(_appliedSearchText, StringComparison.OrdinalIgnoreCase));
            }

            FilteredAlerts.Clear();
            foreach (var alert in filtered)
            {
                FilteredAlerts.Add(alert);
            }

            OnPropertyChanged(nameof(AlertCount));
            OnPropertyChanged(nameof(CriticalCount));
            OnPropertyChanged(nameof(HighCount));
            OnPropertyChanged(nameof(OpenCount));
            OnPropertyChanged(nameof(SelectedCount));
            IsAllSelected = false;
            (AcknowledgeVisibleCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ResolveVisibleCommand as RelayCommand)?.RaiseCanExecuteChanged();
            GoToPage(preserveCurrentPage ? CurrentPage : 1);
        }

        private void GoToPage(int page)
        {
            TotalPages = Math.Max(1, (int)Math.Ceiling(FilteredAlerts.Count / (double)_pageSize));
            CurrentPage = Math.Clamp(page, 1, TotalPages);

            PagedAlerts.Clear();
            foreach (var row in FilteredAlerts.Skip((CurrentPage - 1) * _pageSize).Take(_pageSize))
                PagedAlerts.Add(row);

            _isAllSelected = false;
            OnPropertyChanged(nameof(IsAllSelected));
            OnPropertyChanged(nameof(PageInfo));
            (NextPageCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (PreviousPageCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (FirstPageCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (LastPageCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private async Task AcknowledgeSelectedAsync()
        {
            var ids = FilteredAlerts
                .Where(a => a.IsSelected && !a.IsAcknowledged && !a.IsResolved)
                .Select(a => a.AlertId)
                .Distinct()
                .ToList();

            if (!ids.Any()) return;

            var currentUser = _authenticationService.GetCurrentUser();
            var userId = currentUser?.Id ?? 1;

            using var scope = _scopeFactory.CreateScope();
            var monitoringService = scope.ServiceProvider.GetRequiredService<IMonitoringService>();
            await monitoringService.AcknowledgeAlertsAsync(ids, userId);
            await LoadAlertsAsync();
        }

        private async Task ResolveSelectedAsync()
        {
            var ids = FilteredAlerts
                .Where(a => a.IsSelected && !a.IsResolved)
                .Select(a => a.AlertId)
                .Distinct()
                .ToList();

            if (!ids.Any()) return;

            var currentUser = _authenticationService.GetCurrentUser();
            var userId = currentUser?.Id ?? 1;

            using var scope = _scopeFactory.CreateScope();
            var monitoringService = scope.ServiceProvider.GetRequiredService<IMonitoringService>();
            await monitoringService.ResolveAlertsAsync(ids, userId);
            await LoadAlertsAsync();
        }

        private async Task AcknowledgeVisibleAsync()
        {
            var ids = FilteredAlerts
                .Where(a => !a.IsAcknowledged && !a.IsResolved)
                .Select(a => a.AlertId)
                .Distinct()
                .ToList();

            if (!ids.Any()) return;

            var currentUser = _authenticationService.GetCurrentUser();
            var userId = currentUser?.Id ?? 1;

            using var scope = _scopeFactory.CreateScope();
            var monitoringService = scope.ServiceProvider.GetRequiredService<IMonitoringService>();
            await monitoringService.AcknowledgeAlertsAsync(ids, userId);
            await LoadAlertsAsync();
        }

        private async Task ResolveVisibleAsync()
        {
            var ids = FilteredAlerts
                .Where(a => !a.IsResolved)
                .Select(a => a.AlertId)
                .Distinct()
                .ToList();

            if (!ids.Any()) return;

            var currentUser = _authenticationService.GetCurrentUser();
            var userId = currentUser?.Id ?? 1;

            using var scope = _scopeFactory.CreateScope();
            var monitoringService = scope.ServiceProvider.GetRequiredService<IMonitoringService>();
            await monitoringService.ResolveAlertsAsync(ids, userId);
            await LoadAlertsAsync();
        }

        private async Task ExportCsvAsync()
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                DefaultExt = "csv",
                FileName = $"IRIS_Alerts_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (saveDialog.ShowDialog() != true)
            {
                return;
            }

            var csv = new StringBuilder();
            csv.AppendLine("Timestamp,Severity,Type,PC,Room,Message");
            foreach (var alert in FilteredAlerts)
            {
                csv.AppendLine(string.Join(",",
                    EscapeCsv(TimeZoneInfo.ConvertTimeFromUtc(alert.Timestamp, TimeZoneInfo.Local).ToString("yyyy-MM-dd HH:mm:ss")),
                    EscapeCsv(alert.Severity),
                    EscapeCsv(alert.Type),
                    EscapeCsv(alert.PCName),
                    EscapeCsv(alert.RoomName),
                    EscapeCsv(alert.Message)));
            }

            await File.WriteAllTextAsync(saveDialog.FileName, csv.ToString());
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }

        public void OnNavigatedTo()
        {
            _isActive = true;
            _refreshTimer.Start();
            _ = LoadAlertsAsync();
        }

        public void OnNavigatedFrom()
        {
            _isActive = false;
            _refreshTimer.Stop();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class AlertRow : INotifyPropertyChanged
    {
        private bool _isSelected;

        public int AlertId { get; set; }
        public string AlertKey { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Severity { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string PCName { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool IsAcknowledged { get; set; }
        public DateTime? AcknowledgedAt { get; set; }
        public bool IsResolved { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string State => IsResolved ? "Resolved" : (IsAcknowledged ? "Acknowledged" : "New");

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
