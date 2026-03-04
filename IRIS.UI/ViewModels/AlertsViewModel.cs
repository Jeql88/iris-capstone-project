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
using Microsoft.Win32;

namespace IRIS.UI.ViewModels
{
    public class AlertsViewModel : INotifyPropertyChanged, INavigationAware
    {
        private readonly IMonitoringService _monitoringService;
        private readonly IAuthenticationService _authenticationService;
        private readonly DispatcherTimer _refreshTimer;
        private readonly SemaphoreSlim _loadSemaphore = new(1, 1);
        private bool _isActive = true;
        private RoomDto? _selectedRoom;
        private string _severityFilter = "All";
        private string _typeFilter = "All";
        private string _stateFilter = "Open";
        private string _searchText = string.Empty;
        private bool _isLoading;
        private DateTime _lastUpdatedUtc = DateTime.MinValue;
        private AlertRow? _selectedAlert;

        public AlertsViewModel(IMonitoringService monitoringService, IAuthenticationService authenticationService)
        {
            _monitoringService = monitoringService;
            _authenticationService = authenticationService;
            RefreshCommand = new RelayCommand(async () => await LoadAlertsAsync(), () => true);
            ExportCommand = new RelayCommand(async () => await ExportCsvAsync(), () => true);
            AcknowledgeCommand = new RelayCommand(async () => await AcknowledgeSelectedAsync(), () => SelectedAlert is { IsAcknowledged: false, IsResolved: false });
            ResolveCommand = new RelayCommand(async () => await ResolveSelectedAsync(), () => SelectedAlert is { IsResolved: false });
            AcknowledgeVisibleCommand = new RelayCommand(async () => await AcknowledgeVisibleAsync(), () => FilteredAlerts.Any(a => !a.IsAcknowledged && !a.IsResolved));
            ResolveVisibleCommand = new RelayCommand(async () => await ResolveVisibleAsync(), () => FilteredAlerts.Any(a => !a.IsResolved));

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _refreshTimer.Tick += async (_, _) => await LoadAlertsAsync();

            _ = InitializeAsync();
        }

        public ObservableCollection<RoomDto> Rooms { get; } = new();
        public ObservableCollection<AlertRow> Alerts { get; } = new();
        public ObservableCollection<AlertRow> FilteredAlerts { get; } = new();
        public string[] SeverityOptions { get; } = { "All", "Critical", "High", "Medium", "Low" };
        public string[] TypeOptions { get; } = { "All", "Hardware", "Network", "Thermal", "System" };
        public string[] StateOptions { get; } = { "Open", "Resolved", "All" };

        public RoomDto? SelectedRoom
        {
            get => _selectedRoom;
            set
            {
                _selectedRoom = value;
                OnPropertyChanged();
                _ = LoadAlertsAsync();
            }
        }

        public string SeverityFilter
        {
            get => _severityFilter;
            set
            {
                _severityFilter = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        public string TypeFilter
        {
            get => _typeFilter;
            set
            {
                _typeFilter = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        public string StateFilter
        {
            get => _stateFilter;
            set
            {
                _stateFilter = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                ApplyFilters();
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

        private async Task InitializeAsync()
        {
            var rooms = await _monitoringService.GetRoomsAsync();
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

            await LoadAlertsAsync();
            _refreshTimer.Start();
        }

        private async Task LoadAlertsAsync()
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

                int? roomId = SelectedRoom != null && SelectedRoom.Id > 0 ? SelectedRoom.Id : null;
                var includeResolved = string.Equals(StateFilter, "Resolved", StringComparison.OrdinalIgnoreCase) ||
                                      string.Equals(StateFilter, "All", StringComparison.OrdinalIgnoreCase);
                var alerts = await _monitoringService.GetAlertFeedAsync(roomId, 300, includeResolved);

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
                ApplyFilters();
            }
            finally
            {
                IsLoading = false;
                _loadSemaphore.Release();
            }
        }

        private void ApplyFilters()
        {
            var filtered = Alerts.AsEnumerable();

            if (!string.Equals(SeverityFilter, "All", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(a => a.Severity.Equals(SeverityFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.Equals(TypeFilter, "All", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(a => a.Type.Equals(TypeFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (string.Equals(StateFilter, "Open", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(a => !a.IsResolved);
            }
            else if (string.Equals(StateFilter, "Resolved", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(a => a.IsResolved);
            }

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                filtered = filtered.Where(a =>
                    a.PCName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    a.RoomName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    a.Message.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
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
            (AcknowledgeVisibleCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ResolveVisibleCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private async Task AcknowledgeSelectedAsync()
        {
            if (SelectedAlert == null || SelectedAlert.IsAcknowledged || SelectedAlert.IsResolved)
            {
                return;
            }

            var currentUser = _authenticationService.GetCurrentUser();
            var userId = currentUser?.Id ?? 1;
            var ok = await _monitoringService.AcknowledgeAlertAsync(SelectedAlert.AlertId, userId);
            if (ok)
            {
                await LoadAlertsAsync();
            }
        }

        private async Task ResolveSelectedAsync()
        {
            if (SelectedAlert == null || SelectedAlert.IsResolved)
            {
                return;
            }

            var currentUser = _authenticationService.GetCurrentUser();
            var userId = currentUser?.Id ?? 1;
            var ok = await _monitoringService.ResolveAlertAsync(SelectedAlert.AlertId, userId);
            if (ok)
            {
                await LoadAlertsAsync();
            }
        }

        private async Task AcknowledgeVisibleAsync()
        {
            var ids = FilteredAlerts
                .Where(a => !a.IsAcknowledged && !a.IsResolved)
                .Select(a => a.AlertId)
                .Distinct()
                .ToList();

            if (!ids.Any())
            {
                return;
            }

            var currentUser = _authenticationService.GetCurrentUser();
            var userId = currentUser?.Id ?? 1;
            await _monitoringService.AcknowledgeAlertsAsync(ids, userId);
            await LoadAlertsAsync();
        }

        private async Task ResolveVisibleAsync()
        {
            var ids = FilteredAlerts
                .Where(a => !a.IsResolved)
                .Select(a => a.AlertId)
                .Distinct()
                .ToList();

            if (!ids.Any())
            {
                return;
            }

            var currentUser = _authenticationService.GetCurrentUser();
            var userId = currentUser?.Id ?? 1;
            await _monitoringService.ResolveAlertsAsync(ids, userId);
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

        public void OnNavigatedFrom()
        {
            _isActive = false;
            _refreshTimer.Stop();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class AlertRow
    {
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
    }
}
