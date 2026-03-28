using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Threading;
using System.Collections.Specialized;
using System.Windows;
using System.Text;
using IRIS.UI.Helpers;
using IRIS.UI.Services;
using IRIS.UI.Views.Dialogs;
using IRIS.Core.Services.Contracts;
using IRIS.Core.Services.ServiceModels;
using IRIS.Core.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IRIS.UI.ViewModels
{
    public class MonitorViewModel : INotifyPropertyChanged, INavigationAware
    {
        private readonly INavigationService _navigationService;
        private readonly IPCDataCacheService _cache;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IPowerCommandQueueService _powerCommandQueueService;
        private readonly int _screenStreamPort;
        private readonly string? _screenStreamToken;
        private readonly DispatcherTimer _refreshTimer;
        private static readonly HttpClient SnapshotHttpClient = new() { Timeout = TimeSpan.FromMilliseconds(2200) };
        private string _searchText = string.Empty;
        private RoomDto? _selectedRoom;
        private string _selectedPcStatus = "All Statuses";
        private RoomDto? _appliedRoom;
        private string _appliedSearchText = string.Empty;
        private string _appliedPcStatus = "All Statuses";
        private int _totalPCCount;
        private int _onlinePCCount;
        private int _offlinePCCount;
        private bool _hasNoPCs = true;
        private int _criticalAlertCount;
        private int _highAlertCount;
        private string _topAlertMessage = "No active alerts";
        private string _alertHeaderText = "All clear";
        private bool _isPcAlertsPanelOpen;
        private bool _isTimelinePanelOpen;
        private string _selectedPcAlertsTitle = "Device Alerts";
        private string _selectedPcTimelineTitle = "Device Timeline";
        private DateTime _lastAlertRefreshUtc = DateTime.MinValue;
        private DateTime _lastAgentRefreshRequestUtc = DateTime.MinValue;
        private readonly SemaphoreSlim _loadPcDataSemaphore = new(1, 1);
        private bool _isInitialized;
        private bool _isActive = true;
        private bool _isLoading;
        private bool _isTimelineLoading;

        public MonitorViewModel(
            INavigationService navigationService,
            IPCDataCacheService cache,
            IServiceScopeFactory scopeFactory,
            IPowerCommandQueueService powerCommandQueueService,
            IConfiguration configuration)
        {
            _navigationService = navigationService;
            _cache = cache;
            _scopeFactory = scopeFactory;
            _powerCommandQueueService = powerCommandQueueService;
            _screenStreamPort = int.TryParse(configuration["AgentSettings:ScreenStreamPort"], out var port) ? port : 5057;
            _screenStreamToken = configuration["AgentSettings:ScreenStreamToken"];
            ViewScreenCommand = new RelayCommand(async () => await ViewScreenAsync(), () => SelectedPC != null);
            ViewScreenForPCCommand = new RelayCommand<PCDisplayModel>(pc => ViewScreenForPC(pc));
            ToggleCardDetailsCommand = new RelayCommand<PCDisplayModel>(pc => ToggleCardDetails(pc));
            OpenPcAlertsForPCCommand = new RelayCommand<PCDisplayModel>(pc => OpenAlertsForPC(pc));
            ClosePcAlertsPanelCommand = new RelayCommand(() => IsPcAlertsPanelOpen = false, () => true);
            ShowTimelineForPCCommand = new RelayCommand<PCDisplayModel>(pc => OpenTimelineForPC(pc));
            CloseTimelinePanelCommand = new RelayCommand(() => IsTimelinePanelOpen = false, () => true);
            FreezeCommand = new RelayCommand(async () => await ToggleFreezeAsync(), () => SelectedPC != null);
            RemoteDesktopCommand = new RelayCommand(() => RemoteDesktopConnect(), () => SelectedPC != null);
            RemoteDesktopForPCCommand = new RelayCommand<PCDisplayModel>(pc => RemoteDesktopForPC(pc));
            RefreshCommand = new RelayCommand(async () => { IsLoading = true; await LoadPCDataAsync(); }, () => true);
            RefreshSelectedPcTimelineCommand = new RelayCommand(async () => await RefreshSelectedPcTimelineAsync(), () => SelectedPC != null);
            ApplyFiltersCommand = new RelayCommand(async () => await ApplyFiltersAsync(), () => true);
            ResetFiltersCommand = new RelayCommand(async () => await ResetFiltersAsync(), () => true);

            SelectedPcTimeline.CollectionChanged += OnSelectedPcTimelineCollectionChanged;

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _refreshTimer.Tick += async (s, e) => await LoadPCDataAsync();

            _ = InitializeAsync();
        }

        public ObservableCollection<PCDisplayModel> PCs { get; } = new();
        public ObservableCollection<PCDisplayModel> FilteredPCs { get; } = new();
        public ObservableCollection<RoomDto> Rooms { get; } = new();
        public ObservableCollection<LiveAlertDisplayModel> ActiveAlerts { get; } = new();
        public ObservableCollection<LiveAlertDisplayModel> SelectedPcAlerts { get; } = new();
        public ObservableCollection<PCHealthTimelineDisplayModel> SelectedPcTimeline { get; } = new();

        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
            }
        }

        public string SelectedPcStatus
        {
            get => _selectedPcStatus;
            set
            {
                _selectedPcStatus = value;
                OnPropertyChanged();
            }
        }

        public RoomDto? SelectedRoom
        {
            get => _selectedRoom;
            set
            {
                _selectedRoom = value;
                OnPropertyChanged();
            }
        }

        public int TotalPCCount
        {
            get => _totalPCCount;
            set { _totalPCCount = value; OnPropertyChanged(); }
        }

        public int OnlinePCCount
        {
            get => _onlinePCCount;
            set { _onlinePCCount = value; OnPropertyChanged(); }
        }

        public int OfflinePCCount
        {
            get => _offlinePCCount;
            set { _offlinePCCount = value; OnPropertyChanged(); }
        }

        public bool HasNoPCs
        {
            get => _hasNoPCs;
            set { _hasNoPCs = value; OnPropertyChanged(); }
        }

        public int CriticalAlertCount
        {
            get => _criticalAlertCount;
            set { _criticalAlertCount = value; OnPropertyChanged(); }
        }

        public int HighAlertCount
        {
            get => _highAlertCount;
            set { _highAlertCount = value; OnPropertyChanged(); }
        }

        public string TopAlertMessage
        {
            get => _topAlertMessage;
            set { _topAlertMessage = value; OnPropertyChanged(); }
        }

        public string AlertHeaderText
        {
            get => _alertHeaderText;
            set { _alertHeaderText = value; OnPropertyChanged(); }
        }

        public bool IsPcAlertsPanelOpen
        {
            get => _isPcAlertsPanelOpen;
            set { _isPcAlertsPanelOpen = value; OnPropertyChanged(); }
        }

        public bool IsTimelinePanelOpen
        {
            get => _isTimelinePanelOpen;
            set { _isTimelinePanelOpen = value; OnPropertyChanged(); }
        }

        public string SelectedPcAlertsTitle
        {
            get => _selectedPcAlertsTitle;
            set { _selectedPcAlertsTitle = value; OnPropertyChanged(); }
        }

        public string SelectedPcTimelineTitle
        {
            get => _selectedPcTimelineTitle;
            set { _selectedPcTimelineTitle = value; OnPropertyChanged(); }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        public bool IsTimelineLoading
        {
            get => _isTimelineLoading;
            set { _isTimelineLoading = value; OnPropertyChanged(); }
        }

        public bool HasSelectedPC => SelectedPC != null;
        public bool HasTimelineEvents => SelectedPcTimeline.Count > 0;
        public string TimelineEmptyMessage => HasSelectedPC
            ? "No timeline events for the selected period"
            : "Select a PC to view health timeline";

        private PCDisplayModel? _selectedPC;
        public PCDisplayModel? SelectedPC
        {
            get => _selectedPC;
            set
            {
                // Deselect previous
                if (_selectedPC != null) _selectedPC.IsSelected = false;
                _selectedPC = value;
                if (_selectedPC != null) _selectedPC.IsSelected = true;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedPC));
                OnPropertyChanged(nameof(HasTimelineEvents));
                OnPropertyChanged(nameof(TimelineEmptyMessage));
                (ViewScreenCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (FreezeCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (RemoteDesktopCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (RefreshSelectedPcTimelineCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public ICommand ViewScreenCommand { get; }
        public ICommand ViewScreenForPCCommand { get; }
        public ICommand ToggleCardDetailsCommand { get; }
        public ICommand OpenPcAlertsForPCCommand { get; }
        public ICommand ClosePcAlertsPanelCommand { get; }
        public ICommand ShowTimelineForPCCommand { get; }
        public ICommand CloseTimelinePanelCommand { get; }
        public ICommand FreezeCommand { get; }
        public ICommand RemoteDesktopCommand { get; }
        public ICommand RemoteDesktopForPCCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand RefreshSelectedPcTimelineCommand { get; }
        public ICommand ApplyFiltersCommand { get; }
        public ICommand ResetFiltersCommand { get; }

        private async Task ApplyFiltersAsync()
        {
            _appliedRoom = SelectedRoom;
            _appliedSearchText = SearchText?.Trim() ?? string.Empty;
            _appliedPcStatus = SelectedPcStatus;

            // Apply room filter to cache and reload data
            var selectedRoomId = _appliedRoom != null && _appliedRoom.Id > 0 ? _appliedRoom.Id : (int?)null;
            _cache.CurrentRoomFilter = selectedRoomId;
            
            await LoadPCDataAsync();
        }

        private async Task ResetFiltersAsync()
        {
            SearchText = string.Empty;
            SelectedPcStatus = "All Statuses";
            SelectedRoom = Rooms.FirstOrDefault();
            await ApplyFiltersAsync();
        }

        private async Task InitializeAsync()
        {
            if (_isInitialized) return;
            await LoadRoomsAsync();
            _isInitialized = true;

            _appliedRoom = SelectedRoom;
            _appliedSearchText = SearchText?.Trim() ?? string.Empty;
            _appliedPcStatus = SelectedPcStatus;

            IsLoading = true;
            await LoadPCDataAsync();
            _refreshTimer.Start();
        }

        private async Task LoadPCDataAsync()
        {
            if (!_isActive)
            {
                return;
            }

            if (!await _loadPcDataSemaphore.WaitAsync(0))
            {
                return;
            }

            try
            {
                if (!_isActive)
                {
                    return;
                }

                var previousSelectionId = SelectedPC?.Id;
                var previousFlipState = SelectedPC?.IsFlipped ?? false;

                // Force refresh from DB - always get latest data
                _cache.CurrentRoomFilter = null;
                await _cache.RefreshPCDataAsync();

                var pcs = _cache.CachedPCs;
                var counts = _cache.CachedStatusCounts;

                await RequestImmediateAgentRefreshAsync(pcs);

                ApplyCachedPCData();

                // Always apply filter first so PCs are visible even if alerts/snapshots fail
                ApplyFilter();

                // Force alert refresh every time
                try
                {
                    await LoadLiveAlertsAsync();
                }
                catch { /* Alert loading failure must not block PC display */ }

                // Screenshots only load on Monitor page (not from cache) — reduces overhead
                try
                {
                    await LoadSnapshotsAsync();
                }
                catch { /* Snapshot loading failure must not block PC display */ }

                if (previousSelectionId.HasValue)
                {
                    var matchedSelection = PCs.FirstOrDefault(p => p.Id == previousSelectionId.Value);
                    if (matchedSelection != null)
                    {
                        matchedSelection.IsFlipped = previousFlipState;
                    }

                    SelectedPC = matchedSelection;
                }
                else
                {
                    SelectedPC = null;
                }
            }
            catch
            {
                // Fallback to empty if error
            }
            finally
            {
                IsLoading = false;
                _loadPcDataSemaphore.Release();
            }
        }

        public void OnNavigatedTo()
        {
            _isActive = true;
            _ = LoadPCDataAsync();
            _refreshTimer.Start();
        }

        public void OnNavigatedFrom()
        {
            _isActive = false;
            _refreshTimer.Stop();
        }

        /// <summary>
        /// Updates PCs collection in-place from the shared cache.
        /// Existing items are updated without removing them (no visual flash).
        /// New PCs are added; stale PCs are removed.
        /// </summary>
        private void ApplyCachedPCData()
        {
            var pcs = _cache.CachedPCs;
            var counts = _cache.CachedStatusCounts;

            var existingById = PCs.ToDictionary(p => p.Id);
            var incomingIds = new HashSet<int>();

            foreach (var pc in pcs)
            {
                incomingIds.Add(pc.Id);

                if (existingById.TryGetValue(pc.Id, out var existing))
                {
                    // Update in-place — each setter fires PropertyChanged, UI updates smoothly
                    UpdateDisplayModel(existing, pc);

                    var cachedFreezeState = _cache.GetFreezeState(pc.Id);
                    if (cachedFreezeState.HasValue)
                    {
                        existing.IsFreezeActive = cachedFreezeState.Value;
                    }
                }
                else
                {
                    // Brand-new PC — add it
                    var display = CreateDisplayModel(pc);

                    var cachedFreezeState = _cache.GetFreezeState(pc.Id);
                    if (cachedFreezeState.HasValue)
                    {
                        display.IsFreezeActive = cachedFreezeState.Value;
                    }

                    PCs.Add(display);
                }
            }

            // Remove PCs that are no longer in the cache
            for (int i = PCs.Count - 1; i >= 0; i--)
            {
                if (!incomingIds.Contains(PCs[i].Id))
                {
                    PCs.RemoveAt(i);
                }
            }

            OnlinePCCount = counts.OnlineCount;
            OfflinePCCount = counts.OfflineCount;
            TotalPCCount = OnlinePCCount + OfflinePCCount;
        }

        private static PCDisplayModel CreateDisplayModel(PCMonitorInfo pc)
        {
            return new PCDisplayModel
            {
                Id = pc.Id,
                PCName = pc.Name,
                IPAddress = pc.IpAddress,
                MacAddress = pc.MacAddress,
                RoomName = pc.RoomName,
                Status = pc.Status,
                OS = pc.OperatingSystem,
                CPU = $"{pc.CpuUsage:F0}%",
                CPUTemperature = pc.CpuTemperature.HasValue ? $"{pc.CpuTemperature.Value:F1} °C" : "N/A",
                GPU = pc.GpuUsage.HasValue ? $"{pc.GpuUsage.Value:F0}%" : "N/A",
                GPUTemperature = pc.GpuTemperature.HasValue ? $"{pc.GpuTemperature.Value:F1} °C" : "N/A",
                Network = $"{pc.NetworkUsage:F1} Mbps",
                NetworkUpload = $"{pc.NetworkUploadMbps:F1} Mbps",
                NetworkDownload = $"{pc.NetworkDownloadMbps:F1} Mbps",
                NetworkLatency = pc.NetworkLatencyMs.HasValue ? $"{pc.NetworkLatencyMs.Value:F0} ms" : "N/A",
                PacketLoss = pc.PacketLossPercent.HasValue ? $"{pc.PacketLossPercent.Value:F1}%" : "N/A",
                RAM = $"{pc.RamUsage:F0}%",
                CpuUsagePercent = pc.CpuUsage,
                CpuTemperatureValue = pc.CpuTemperature,
                GpuUsagePercent = pc.GpuUsage,
                GpuTemperatureValue = pc.GpuTemperature,
                RamUsagePercent = pc.RamUsage,
                DiskUsagePercent = pc.DiskUsage,
                NetworkUploadMbps = pc.NetworkUploadMbps,
                NetworkDownloadMbps = pc.NetworkDownloadMbps,
                NetworkLatencyMs = pc.NetworkLatencyMs,
                PacketLossPercent = pc.PacketLossPercent,
                SnapshotImageBase64 = null,
                TopAlertSeverity = "None",
                TopAlertMessage = "No active alerts",
                LastMetricTimestamp = pc.LastMetricTimestamp
            };
        }

        private static void UpdateDisplayModel(PCDisplayModel display, PCMonitorInfo pc)
        {
            display.PCName = pc.Name;
            display.IPAddress = pc.IpAddress;
            display.MacAddress = pc.MacAddress;
            display.RoomName = pc.RoomName;
            display.Status = pc.Status;
            display.OS = pc.OperatingSystem;
            display.CPU = $"{pc.CpuUsage:F0}%";
            display.CPUTemperature = pc.CpuTemperature.HasValue ? $"{pc.CpuTemperature.Value:F1} °C" : "N/A";
            display.GPU = pc.GpuUsage.HasValue ? $"{pc.GpuUsage.Value:F0}%" : "N/A";
            display.GPUTemperature = pc.GpuTemperature.HasValue ? $"{pc.GpuTemperature.Value:F1} °C" : "N/A";
            display.Network = $"{pc.NetworkUsage:F1} Mbps";
            display.NetworkUpload = $"{pc.NetworkUploadMbps:F1} Mbps";
            display.NetworkDownload = $"{pc.NetworkDownloadMbps:F1} Mbps";
            display.NetworkLatency = pc.NetworkLatencyMs.HasValue ? $"{pc.NetworkLatencyMs.Value:F0} ms" : "N/A";
            display.PacketLoss = pc.PacketLossPercent.HasValue ? $"{pc.PacketLossPercent.Value:F1}%" : "N/A";
            display.RAM = $"{pc.RamUsage:F0}%";
            display.CpuUsagePercent = pc.CpuUsage;
            display.CpuTemperatureValue = pc.CpuTemperature;
            display.GpuUsagePercent = pc.GpuUsage;
            display.GpuTemperatureValue = pc.GpuTemperature;
            display.RamUsagePercent = pc.RamUsage;
            display.DiskUsagePercent = pc.DiskUsage;
            display.NetworkUploadMbps = pc.NetworkUploadMbps;
            display.NetworkDownloadMbps = pc.NetworkDownloadMbps;
            display.NetworkLatencyMs = pc.NetworkLatencyMs;
            display.PacketLossPercent = pc.PacketLossPercent;
            display.LastMetricTimestamp = pc.LastMetricTimestamp;
            // SnapshotImageBase64 and alert fields are NOT overwritten — they're loaded separately
        }

        private async Task LoadRoomsAsync()
        {
            try
            {
                await _cache.RefreshRoomsAsync();
                var rooms = _cache.CachedRooms;
                Rooms.Clear();
                Rooms.Add(new RoomDto(-1, "All Laboratories", "", 0, true, DateTime.UtcNow));
                foreach (var room in rooms)
                {
                    Rooms.Add(room);
                }

                // Default to "All Rooms" so the monitor shows all PCs immediately
                if (SelectedRoom == null && Rooms.Count > 0)
                {
                    SelectedRoom = Rooms[0];
                }
            }
            catch
            {
                // ignore for now
            }
        }

        private void ApplyFilter()
        {
            var desired = new List<PCDisplayModel>();
            var appliedRoomName = _appliedRoom != null && _appliedRoom.Id > 0
                ? _appliedRoom.RoomNumber
                : null;

            foreach (var pc in PCs)
            {
                bool matchesRoom = string.IsNullOrWhiteSpace(appliedRoomName) ||
                    string.Equals(pc.RoomName, appliedRoomName, StringComparison.OrdinalIgnoreCase);

                bool matchesSearch = string.IsNullOrEmpty(_appliedSearchText) ||
                    pc.PCName.Contains(_appliedSearchText, StringComparison.OrdinalIgnoreCase) ||
                    pc.IPAddress.Contains(_appliedSearchText, StringComparison.OrdinalIgnoreCase);

                bool matchesStatus = _appliedPcStatus.StartsWith("All", StringComparison.OrdinalIgnoreCase)
                    || (_appliedPcStatus.StartsWith("Online", StringComparison.OrdinalIgnoreCase) && string.Equals(pc.Status, "Online", StringComparison.OrdinalIgnoreCase))
                    || (_appliedPcStatus.StartsWith("Offline", StringComparison.OrdinalIgnoreCase) && string.Equals(pc.Status, "Offline", StringComparison.OrdinalIgnoreCase));

                if (matchesRoom && matchesSearch && matchesStatus)
                {
                    desired.Add(pc);
                }
            }

            desired = desired
                .OrderBy(pc => pc.PCName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(pc => pc.Id)
                .ToList();

            // Build a set of desired IDs for quick removal lookup
            var desiredIds = new HashSet<int>(desired.Select(p => p.Id));

            // Remove items that should no longer be visible
            for (int i = FilteredPCs.Count - 1; i >= 0; i--)
            {
                if (!desiredIds.Contains(FilteredPCs[i].Id))
                {
                    FilteredPCs.RemoveAt(i);
                }
            }

            // Add items that are missing
            var existingIds = new HashSet<int>(FilteredPCs.Select(p => p.Id));
            foreach (var pc in desired)
            {
                if (!existingIds.Contains(pc.Id))
                {
                    FilteredPCs.Add(pc);
                }
            }

            HasNoPCs = FilteredPCs.Count == 0;
        }

        private async Task LoadLiveAlertsAsync()
        {
            await _cache.RefreshLiveAlertsAsync();
            var alerts = _cache.CachedLiveAlerts;
            ActiveAlerts.Clear();

            foreach (var alert in alerts)
            {
                ActiveAlerts.Add(new LiveAlertDisplayModel
                {
                    PCId = alert.PCId,
                    PCName = alert.PCName,
                    RoomName = alert.RoomName,
                    Severity = alert.Severity,
                    Type = alert.Type,
                    Message = alert.Message,
                    Timestamp = DateTimeDisplayHelper.ToManilaFromUtc(alert.Timestamp)
                });
            }

            CriticalAlertCount = alerts.Count(a => a.Severity == "Critical");
            HighAlertCount = alerts.Count(a => a.Severity == "High");
            TopAlertMessage = alerts.FirstOrDefault()?.Message ?? "No active alerts";
            AlertHeaderText = CriticalAlertCount == 0 && HighAlertCount == 0
                ? "All clear"
                : $"Critical {CriticalAlertCount} • High {HighAlertCount}";

            var topByPc = alerts
                .GroupBy(a => a.PCId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(a => a.SeverityRank).ThenByDescending(a => a.Timestamp).First());

            var countByPc = alerts
                .GroupBy(a => a.PCId)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var pc in PCs)
            {
                if (topByPc.TryGetValue(pc.Id, out var topAlert))
                {
                    pc.TopAlertSeverity = topAlert.Severity;
                    pc.TopAlertMessage = topAlert.Message;
                    pc.AlertCount = countByPc.TryGetValue(pc.Id, out var alertCount) ? alertCount : 0;
                }
                else
                {
                    pc.TopAlertSeverity = "None";
                    pc.TopAlertMessage = "No active alerts";
                    pc.AlertCount = 0;
                }
            }

            if (IsPcAlertsPanelOpen && SelectedPC != null)
            {
                PopulateSelectedPcAlerts(SelectedPC.Id);
            }

            _lastAlertRefreshUtc = DateTime.UtcNow;
        }

        private async Task LoadSnapshotsAsync()
        {
            var reachablePcs = PCs
                .Where(p => !string.IsNullOrWhiteSpace(p.IPAddress) && p.IPAddress != "N/A")
                .ToList();

            var semaphore = new SemaphoreSlim(4);
            var tasks = reachablePcs.Select(async pc =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var base64 = await GetSnapshotBase64Async(pc.IPAddress);
                    if (!string.IsNullOrWhiteSpace(base64))
                    {
                        pc.SnapshotImageBase64 = base64;
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }

        private async Task RequestImmediateAgentRefreshAsync(IEnumerable<PCMonitorInfo> pcs)
        {
            var nowUtc = DateTime.UtcNow;
            if ((nowUtc - _lastAgentRefreshRequestUtc).TotalSeconds < 12)
            {
                return;
            }

            _lastAgentRefreshRequestUtc = nowUtc;

            var uniqueMacs = pcs
                .Select(pc => pc.MacAddress?.Trim())
                .Where(mac => !string.IsNullOrWhiteSpace(mac))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var mac in uniqueMacs)
            {
                await _powerCommandQueueService.QueueCommandAsync(mac!, "RefreshMetrics");
            }
        }

        private async Task<string?> GetSnapshotBase64Async(string ipAddress)
        {
            try
            {
                var url = $"http://{ipAddress}:{_screenStreamPort}/snapshot";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (!string.IsNullOrWhiteSpace(_screenStreamToken))
                {
                    request.Headers.TryAddWithoutValidation("X-IRIS-Snapshot-Token", _screenStreamToken);
                }

                using var response = await SnapshotHttpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var bytes = await response.Content.ReadAsByteArrayAsync();
                return bytes.Length > 0 ? Convert.ToBase64String(bytes) : null;
            }
            catch
            {
                return null;
            }
        }

        private async Task ViewScreenAsync()
        {
            if (SelectedPC != null)
            {
                await Task.CompletedTask;
                _navigationService.NavigateTo("ViewScreen", SelectedPC);
            }
        }

        private void ViewScreenForPC(PCDisplayModel? pc)
        {
            if (pc == null)
            {
                return;
            }

            SelectedPC = pc;
            _navigationService.NavigateTo("ViewScreen", pc);
        }

        private void ToggleCardDetails(PCDisplayModel? pc)
        {
            if (pc == null)
            {
                return;
            }

            SelectedPC = pc;
            pc.IsFlipped = !pc.IsFlipped;
        }

        private void OpenAlertsForPC(PCDisplayModel? pc)
        {
            if (pc == null)
            {
                return;
            }

            SelectedPC = pc;
            PopulateSelectedPcAlerts(pc.Id);
            SelectedPcAlertsTitle = $"Alerts • {pc.PCName}";
            IsPcAlertsPanelOpen = true;
        }

        private void OpenTimelineForPC(PCDisplayModel? pc)
        {
            if (pc == null) return;
            SelectedPC = pc;
            IsTimelinePanelOpen = true;
            _ = LoadSelectedPcTimelineAsync(pc.Id);
        }

        private void PopulateSelectedPcAlerts(int pcId)
        {
            SelectedPcAlerts.Clear();
            foreach (var alert in ActiveAlerts.Where(a => a.PCId == pcId).OrderByDescending(a => a.Timestamp))
            {
                SelectedPcAlerts.Add(alert);
            }
        }

        private async Task RefreshSelectedPcTimelineAsync()
        {
            if (SelectedPC == null)
            {
                return;
            }

            await LoadSelectedPcTimelineAsync(SelectedPC.Id);
        }

        private async Task LoadSelectedPcTimelineAsync(int pcId)
        {
            if (pcId <= 0)
            {
                SelectedPcTimeline.Clear();
                return;
            }

            try
            {
                IsTimelineLoading = true;

                using var scope = _scopeFactory.CreateScope();
                var monitoringService = scope.ServiceProvider.GetRequiredService<IMonitoringService>();
                var events = await monitoringService.GetPcHealthTimelineAsync(pcId, 24, 100);

                SelectedPcTimeline.Clear();
                foreach (var timelineEvent in events.OrderByDescending(e => e.Timestamp))
                {
                    SelectedPcTimeline.Add(new PCHealthTimelineDisplayModel
                    {
                        Timestamp = DateTimeDisplayHelper.ToManilaFromUtc(timelineEvent.Timestamp),
                        RelativeTime = ToRelativeTime(timelineEvent.Timestamp),
                        Severity = string.IsNullOrWhiteSpace(timelineEvent.Severity) ? "Info" : timelineEvent.Severity,
                        Category = string.IsNullOrWhiteSpace(timelineEvent.Category) ? "System" : timelineEvent.Category,
                        Title = timelineEvent.Title,
                        Message = FormatTimelineMessage(timelineEvent)
                    });
                }

                SelectedPcTimelineTitle = SelectedPC != null
                    ? $"Device Timeline • {SelectedPC.PCName}"
                    : "Device Timeline";

                OnPropertyChanged(nameof(HasTimelineEvents));
                OnPropertyChanged(nameof(TimelineEmptyMessage));
            }
            catch
            {
                SelectedPcTimeline.Clear();
                OnPropertyChanged(nameof(HasTimelineEvents));
                OnPropertyChanged(nameof(TimelineEmptyMessage));
            }
            finally
            {
                IsTimelineLoading = false;
            }
        }

        private static string ToRelativeTime(DateTime timestamp)
        {
            var delta = DateTime.UtcNow - timestamp;
            if (delta.TotalSeconds < 60)
            {
                return $"{Math.Max(0, (int)delta.TotalSeconds)}s ago";
            }

            if (delta.TotalMinutes < 60)
            {
                return $"{(int)delta.TotalMinutes}m ago";
            }

            if (delta.TotalHours < 24)
            {
                return $"{(int)delta.TotalHours}h ago";
            }

            return $"{(int)delta.TotalDays}d ago";
        }

        private void OnSelectedPcTimelineCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HasTimelineEvents));
            OnPropertyChanged(nameof(TimelineEmptyMessage));
        }

        private static string FormatTimelineMessage(PcHealthTimelineEvent timelineEvent)
        {
            if (!string.IsNullOrWhiteSpace(timelineEvent.Message)
                && timelineEvent.Message.Contains("Last agent heartbeat", StringComparison.OrdinalIgnoreCase))
            {
                var manilaTimestamp = DateTimeDisplayHelper.ToManilaFromUtc(timelineEvent.Timestamp);
                return $"Last agent heartbeat at {manilaTimestamp:MMM dd, yyyy HH:mm:ss} Asia/Manila.";
            }

            return timelineEvent.Message;
        }

        private async Task ToggleFreezeAsync()
        {
            await Task.CompletedTask;

            if (SelectedPC == null)
            {
                return;
            }

            if (!string.Equals(SelectedPC.Status, "Online", StringComparison.OrdinalIgnoreCase))
            {
                ShowOfflineActionDialog("change freeze state");
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedPC.MacAddress))
            {
                var missingMacDialog = new ConfirmationDialog(
                    "Command Error",
                    "Cannot send freeze command: missing PC MAC address.",
                    "Warning24",
                    "OK",
                    "Cancel",
                    false);
                missingMacDialog.Owner = Application.Current.MainWindow;
                missingMacDialog.ShowDialog();
                return;
            }

            string commandType;
            if (SelectedPC.IsFreezeActive)
            {
                var confirmationDialog = new ConfirmationDialog(
                    "Confirm Unfreeze",
                    $"Unfreeze {SelectedPC.PCName}?",
                    "LockClosed24");
                confirmationDialog.Owner = Application.Current.MainWindow;

                if (confirmationDialog.ShowDialog() != true)
                {
                    return;
                }

                commandType = "FreezeOff";
            }
            else
            {
                var freezeDialog = new FreezeMessageDialog(
                    "Freeze PC",
                    $"Enter the message to show on {SelectedPC.PCName} while frozen:",
                    FreezeMessageDialog.DefaultFreezeMessage);
                freezeDialog.Owner = Application.Current.MainWindow;

                if (freezeDialog.ShowDialog() != true)
                {
                    return;
                }

                var encodedMessage = Convert.ToBase64String(Encoding.UTF8.GetBytes(freezeDialog.FreezeMessage));
                commandType = $"FreezeOn::{encodedMessage}";
            }

            var queued = await _powerCommandQueueService.QueueCommandAsync(SelectedPC.MacAddress, commandType);
            if (!queued)
            {
                var commandErrorDialog = new ConfirmationDialog(
                    "Command Error",
                    "Failed to queue freeze command.",
                    "Warning24",
                    "OK",
                    "Cancel",
                    false);
                commandErrorDialog.Owner = Application.Current.MainWindow;
                commandErrorDialog.ShowDialog();
                return;
            }

            SelectedPC.IsFreezeActive = !SelectedPC.IsFreezeActive;
            _cache.SetFreezeState(SelectedPC.Id, SelectedPC.IsFreezeActive);
        }

        private void RemoteDesktopConnect()
        {
            if (SelectedPC == null)
            {
                return;
            }

            if (!string.Equals(SelectedPC.Status, "Online", StringComparison.OrdinalIgnoreCase))
            {
                ShowOfflineActionDialog("open Remote Desktop");
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedPC.IPAddress) || SelectedPC.IPAddress == "N/A")
            {
                var missingIpDialog = new ConfirmationDialog(
                    "Remote Desktop",
                    "Cannot open Remote Desktop: missing target IP address.",
                    "Warning24",
                    "OK",
                    "Cancel",
                    false);
                missingIpDialog.Owner = Application.Current.MainWindow;
                missingIpDialog.ShowDialog();
                return;
            }

            var confirmationDialog = new ConfirmationDialog(
                "Open Remote Desktop",
                $"Open Remote Desktop connection to {SelectedPC.PCName}?",
                "Desktop24");
            confirmationDialog.Owner = Application.Current.MainWindow;

            if (confirmationDialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "mstsc",
                    Arguments = $"/v:{SelectedPC.IPAddress}",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                var errorDialog = new ConfirmationDialog(
                    "Remote Desktop Error",
                    $"Failed to open Remote Desktop. {ex.Message}",
                    "Warning24",
                    "OK",
                    "Cancel",
                    false);
                errorDialog.Owner = Application.Current.MainWindow;
                errorDialog.ShowDialog();
            }
        }

        private static void ShowOfflineActionDialog(string actionName)
        {
            var offlineDialog = new ConfirmationDialog(
                "PC Offline",
                $"Cannot {actionName} because this PC is offline.",
                "Desktop24",
                "OK",
                "Cancel",
                false);
            offlineDialog.Owner = Application.Current.MainWindow;
            offlineDialog.ShowDialog();
        }

        private void RemoteDesktopForPC(PCDisplayModel? pc)
        {
            if (pc == null) return;
            SelectedPC = pc;
            RemoteDesktopConnect();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class PCDisplayModel : INotifyPropertyChanged
    {
        private int _id;
        private string _pcName = string.Empty;
        private string _ipAddress = string.Empty;
        private string _status = string.Empty;
        private string _os = string.Empty;
        private string _cpu = string.Empty;
        private string _cpuTemperature = "N/A";
        private string _gpu = "N/A";
        private string _gpuTemperature = "N/A";
        private string _network = string.Empty;
        private string _networkUpload = "0 Mbps";
        private string _networkDownload = "0 Mbps";
        private string _networkLatency = "N/A";
        private string _packetLoss = "N/A";
        private string _ram = string.Empty;
        private string _macAddress = string.Empty;
        private string _roomName = string.Empty;
        private double _cpuUsagePercent;
        private double? _cpuTemperatureValue;
        private double? _gpuUsagePercent;
        private double? _gpuTemperatureValue;
        private double _ramUsagePercent;
        private double _diskUsagePercent;
        private double _networkUploadMbps;
        private double _networkDownloadMbps;
        private double? _networkLatencyMs;
        private double? _packetLossPercent;
        private DateTime? _lastMetricTimestamp;
        private string? _snapshotImageBase64;
        private string _topAlertSeverity = "None";
        private string _topAlertMessage = "No active alerts";
        private int _alertCount;
        private bool _isSelected;
        private bool _isFlipped;
        private bool _isFreezeActive;

        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public string PCName
        {
            get => _pcName;
            set { _pcName = value; OnPropertyChanged(); }
        }

        public string IPAddress
        {
            get => _ipAddress;
            set { _ipAddress = value; OnPropertyChanged(); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public string OS
        {
            get => _os;
            set { _os = value; OnPropertyChanged(); }
        }

        public string CPU
        {
            get => _cpu;
            set { _cpu = value; OnPropertyChanged(); }
        }

        public string CPUTemperature
        {
            get => _cpuTemperature;
            set { _cpuTemperature = value; OnPropertyChanged(); }
        }

        public string GPU
        {
            get => _gpu;
            set { _gpu = value; OnPropertyChanged(); }
        }

        public string GPUTemperature
        {
            get => _gpuTemperature;
            set { _gpuTemperature = value; OnPropertyChanged(); }
        }

        public string Network
        {
            get => _network;
            set { _network = value; OnPropertyChanged(); }
        }

        public string NetworkUpload
        {
            get => _networkUpload;
            set { _networkUpload = value; OnPropertyChanged(); }
        }

        public string NetworkDownload
        {
            get => _networkDownload;
            set { _networkDownload = value; OnPropertyChanged(); }
        }

        public string NetworkLatency
        {
            get => _networkLatency;
            set { _networkLatency = value; OnPropertyChanged(); }
        }

        public string PacketLoss
        {
            get => _packetLoss;
            set { _packetLoss = value; OnPropertyChanged(); }
        }

        public string RAM
        {
            get => _ram;
            set { _ram = value; OnPropertyChanged(); }
        }

        public string MacAddress
        {
            get => _macAddress;
            set { _macAddress = value; OnPropertyChanged(); }
        }

        public string RoomName
        {
            get => _roomName;
            set { _roomName = value; OnPropertyChanged(); }
        }

        public double CpuUsagePercent
        {
            get => _cpuUsagePercent;
            set { _cpuUsagePercent = value; OnPropertyChanged(); }
        }

        public double? CpuTemperatureValue
        {
            get => _cpuTemperatureValue;
            set
            {
                _cpuTemperatureValue = value;
                OnPropertyChanged();
            }
        }

        public double? GpuUsagePercent
        {
            get => _gpuUsagePercent;
            set { _gpuUsagePercent = value; OnPropertyChanged(); }
        }

        public double? GpuTemperatureValue
        {
            get => _gpuTemperatureValue;
            set
            {
                _gpuTemperatureValue = value;
                OnPropertyChanged();
            }
        }

        public double RamUsagePercent
        {
            get => _ramUsagePercent;
            set { _ramUsagePercent = value; OnPropertyChanged(); }
        }

        public double DiskUsagePercent
        {
            get => _diskUsagePercent;
            set { _diskUsagePercent = value; OnPropertyChanged(); }
        }

        public double NetworkUploadMbps
        {
            get => _networkUploadMbps;
            set { _networkUploadMbps = value; OnPropertyChanged(); }
        }

        public double NetworkDownloadMbps
        {
            get => _networkDownloadMbps;
            set { _networkDownloadMbps = value; OnPropertyChanged(); }
        }

        public double? NetworkLatencyMs
        {
            get => _networkLatencyMs;
            set { _networkLatencyMs = value; OnPropertyChanged(); }
        }

        public double? PacketLossPercent
        {
            get => _packetLossPercent;
            set { _packetLossPercent = value; OnPropertyChanged(); }
        }

        public DateTime? LastMetricTimestamp
        {
            get => _lastMetricTimestamp;
            set { _lastMetricTimestamp = value; OnPropertyChanged(); }
        }

        public string? SnapshotImageBase64
        {
            get => _snapshotImageBase64;
            set { _snapshotImageBase64 = value; OnPropertyChanged(); }
        }

        public string TopAlertSeverity
        {
            get => _topAlertSeverity;
            set { _topAlertSeverity = value; OnPropertyChanged(); }
        }

        public string TopAlertMessage
        {
            get => _topAlertMessage;
            set { _topAlertMessage = value; OnPropertyChanged(); }
        }

        public int AlertCount
        {
            get => _alertCount;
            set { _alertCount = value; OnPropertyChanged(); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public bool IsFlipped
        {
            get => _isFlipped;
            set { _isFlipped = value; OnPropertyChanged(); }
        }

        public bool IsFreezeActive
        {
            get => _isFreezeActive;
            set { _isFreezeActive = value; OnPropertyChanged(); }
        }

        // Legacy property aliases for backward compatibility
        public string Name { get => PCName; set => PCName = value; }
        public string IP { get => IPAddress; set => IPAddress = value; }
        public SolidColorBrush StatusColor
        {
            get => Status == "Online" ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) :
                   new SolidColorBrush(Color.FromRgb(239, 68, 68));
            set { } // ignore sets
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class LiveAlertDisplayModel
    {
        public int PCId { get; set; }
        public string PCName { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public class PCHealthTimelineDisplayModel
    {
        public DateTime Timestamp { get; set; }
        public string RelativeTime { get; set; } = string.Empty;
        public string Severity { get; set; } = "Info";
        public string Category { get; set; } = "System";
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
