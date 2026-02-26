using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Threading;
using IRIS.UI.Helpers;
using IRIS.UI.Services;
using IRIS.Core.Services.Contracts;
using IRIS.Core.Services.ServiceModels;
using IRIS.Core.DTOs;
using Microsoft.Extensions.Configuration;

namespace IRIS.UI.ViewModels
{
    public class MonitorViewModel : INotifyPropertyChanged, INavigationAware
    {
        private readonly INavigationService _navigationService;
        private readonly IMonitoringService _monitoringService;
        private readonly int _screenStreamPort;
        private readonly string? _screenStreamToken;
        private readonly DispatcherTimer _refreshTimer;
        private static readonly HttpClient SnapshotHttpClient = new() { Timeout = TimeSpan.FromMilliseconds(2200) };
        private string _searchText = string.Empty;
        private RoomDto? _selectedRoom;
        private int? _selectedRoomId = null;
        private int _totalPCCount;
        private int _onlinePCCount;
        private int _offlinePCCount;
        private int _idlePCCount;
        private bool _hasNoPCs = true;
        private int _criticalAlertCount;
        private int _highAlertCount;
        private string _topAlertMessage = "No active alerts";
        private string _alertHeaderText = "All clear";
        private bool _isPcAlertsPanelOpen;
        private string _selectedPcAlertsTitle = "Device Alerts";
        private DateTime _lastAlertRefreshUtc = DateTime.MinValue;
        private readonly SemaphoreSlim _loadPcDataSemaphore = new(1, 1);
        private bool _isInitialized;
        private bool _isActive = true;

        public MonitorViewModel(INavigationService navigationService, IMonitoringService monitoringService, IConfiguration configuration)
        {
            _navigationService = navigationService;
            _monitoringService = monitoringService;
            _screenStreamPort = int.TryParse(configuration["AgentSettings:ScreenStreamPort"], out var port) ? port : 5057;
            _screenStreamToken = configuration["AgentSettings:ScreenStreamToken"];
            ViewScreenCommand = new RelayCommand(async () => await ViewScreenAsync(), () => SelectedPC != null);
            ViewScreenForPCCommand = new RelayCommand<PCDisplayModel>(pc => ViewScreenForPC(pc));
            ToggleCardDetailsCommand = new RelayCommand<PCDisplayModel>(pc => ToggleCardDetails(pc));
            OpenPcAlertsForPCCommand = new RelayCommand<PCDisplayModel>(pc => OpenAlertsForPC(pc));
            ClosePcAlertsPanelCommand = new RelayCommand(() => IsPcAlertsPanelOpen = false, () => true);
            LockScreenCommand = new RelayCommand(async () => await LockScreenAsync(), () => SelectedPC != null);
            RefreshCommand = new RelayCommand(async () => await LoadPCDataAsync(), () => true);
            RestartPCCommand = new RelayCommand(async () => await Task.CompletedTask, () => SelectedPC != null);
            ShutdownPCCommand = new RelayCommand(async () => await Task.CompletedTask, () => SelectedPC != null);

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _refreshTimer.Tick += async (s, e) => await LoadPCDataAsync();

            _ = InitializeAsync();
        }

        public ObservableCollection<PCDisplayModel> PCs { get; } = new();
        public ObservableCollection<PCDisplayModel> FilteredPCs { get; } = new();
        public ObservableCollection<RoomDto> Rooms { get; } = new();
        public ObservableCollection<LiveAlertDisplayModel> ActiveAlerts { get; } = new();
        public ObservableCollection<LiveAlertDisplayModel> SelectedPcAlerts { get; } = new();

        public string SearchText
        {
            get => _searchText;
            set 
            { 
                _searchText = value; 
                OnPropertyChanged(); 
                ApplyFilter();
            }
        }

        public RoomDto? SelectedRoom
        {
            get => _selectedRoom;
            set
            {
                _selectedRoom = value;
                _selectedRoomId = value != null && value.Id > 0 ? value.Id : null;
                OnPropertyChanged();
                if (_isInitialized)
                {
                    _ = LoadPCDataAsync();
                }
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

        public int IdlePCCount
        {
            get => _idlePCCount;
            set { _idlePCCount = value; OnPropertyChanged(); }
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

        public string SelectedPcAlertsTitle
        {
            get => _selectedPcAlertsTitle;
            set { _selectedPcAlertsTitle = value; OnPropertyChanged(); }
        }

        public bool HasSelectedPC => SelectedPC != null;

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
                (ViewScreenCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (LockScreenCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (RestartPCCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (ShutdownPCCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public ICommand ViewScreenCommand { get; }
        public ICommand ViewScreenForPCCommand { get; }
        public ICommand ToggleCardDetailsCommand { get; }
        public ICommand OpenPcAlertsForPCCommand { get; }
        public ICommand ClosePcAlertsPanelCommand { get; }
        public ICommand LockScreenCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand RestartPCCommand { get; }
        public ICommand ShutdownPCCommand { get; }

        private async Task InitializeAsync()
        {
            await LoadRoomsAsync();
            _isInitialized = true;
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

                var pcs = await _monitoringService.GetPCsForMonitorAsync(_selectedRoomId);
                var counts = await _monitoringService.GetPCStatusCountsAsync(_selectedRoomId);
                
                PCs.Clear();
                
                foreach (var pc in pcs)
                {
                    PCs.Add(new PCDisplayModel
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
                        CpuTempSource = string.IsNullOrWhiteSpace(pc.CpuTemperatureSource) ? "Unavailable" : pc.CpuTemperatureSource,
                        GPU = pc.GpuUsage.HasValue ? $"{pc.GpuUsage.Value:F0}%" : "N/A",
                        GPUTemperature = pc.GpuTemperature.HasValue ? $"{pc.GpuTemperature.Value:F1} °C" : "N/A",
                        GpuTempSource = string.IsNullOrWhiteSpace(pc.GpuTemperatureSource) ? "Unavailable" : pc.GpuTemperatureSource,
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
                        User = pc.User,
                        SnapshotImageBase64 = null,
                        TopAlertSeverity = "None",
                        TopAlertMessage = "No active alerts",
                        LastMetricTimestamp = pc.LastMetricTimestamp
                    });
                }
                
                OnlinePCCount = counts.OnlineCount;
                OfflinePCCount = counts.OfflineCount;
                IdlePCCount = counts.WarningCount;
                TotalPCCount = OnlinePCCount + OfflinePCCount + IdlePCCount;

                if ((DateTime.UtcNow - _lastAlertRefreshUtc).TotalSeconds >= 10)
                {
                    await LoadLiveAlertsAsync();
                }

                await LoadSnapshotsAsync();
                
                ApplyFilter();
            }
            catch
            {
                // Fallback to empty if error
            }
            finally
            {
                _loadPcDataSemaphore.Release();
            }
        }

        public void OnNavigatedFrom()
        {
            _isActive = false;
            _refreshTimer.Stop();
        }

        private async Task LoadRoomsAsync()
        {
            try
            {
                var rooms = await _monitoringService.GetRoomsAsync();
                Rooms.Clear();
                Rooms.Add(new RoomDto(-1, "All Rooms", "", 0, true, DateTime.UtcNow));
                foreach (var room in rooms)
                {
                    Rooms.Add(room);
                }

                if (SelectedRoom == null && Rooms.Any())
                {
                    SelectedRoom = Rooms.First();
                }
            }
            catch
            {
                // ignore for now
            }
        }

        private void ApplyFilter()
        {
            FilteredPCs.Clear();
            
            foreach (var pc in PCs)
            {
                bool matchesSearch = string.IsNullOrEmpty(SearchText) ||
                    pc.PCName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    pc.IPAddress.Contains(SearchText, StringComparison.OrdinalIgnoreCase);

                if (matchesSearch)
                {
                    FilteredPCs.Add(pc);
                }
            }
            
            HasNoPCs = FilteredPCs.Count == 0;
        }

        private async Task LoadLiveAlertsAsync()
        {
            var alerts = await _monitoringService.GetLiveAlertsAsync(_selectedRoomId, 30);
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
                    Timestamp = alert.Timestamp
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
            var connectedPcs = PCs
                .Where(p => !p.Status.Equals("Offline", StringComparison.OrdinalIgnoreCase))
                .Where(p => !string.IsNullOrWhiteSpace(p.IPAddress) && p.IPAddress != "N/A")
                .ToList();

            var semaphore = new SemaphoreSlim(4);
            var tasks = connectedPcs.Select(async pc =>
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

        private void PopulateSelectedPcAlerts(int pcId)
        {
            SelectedPcAlerts.Clear();
            foreach (var alert in ActiveAlerts.Where(a => a.PCId == pcId).OrderByDescending(a => a.Timestamp))
            {
                SelectedPcAlerts.Add(alert);
            }
        }

        private async Task LockScreenAsync()
        {
            await Task.CompletedTask;
            // TODO: Implement lock screen functionality
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
        private string _cpuTempSource = "Unavailable";
        private string _gpuTempSource = "Unavailable";
        private string _network = string.Empty;
        private string _networkUpload = "0 Mbps";
        private string _networkDownload = "0 Mbps";
        private string _networkLatency = "N/A";
        private string _packetLoss = "N/A";
        private string _ram = string.Empty;
        private string _user = string.Empty;
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

        public string CpuTempSource
        {
            get => _cpuTempSource;
            set
            {
                _cpuTempSource = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SensorDiagnostics));
                OnPropertyChanged(nameof(SensorHealth));
            }
        }

        public string GpuTempSource
        {
            get => _gpuTempSource;
            set
            {
                _gpuTempSource = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SensorDiagnostics));
                OnPropertyChanged(nameof(SensorHealth));
            }
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

        public string User
        {
            get => _user;
            set { _user = value; OnPropertyChanged(); }
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
                OnPropertyChanged(nameof(SensorHealth));
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
                OnPropertyChanged(nameof(SensorHealth));
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

        public string SensorDiagnostics => $"CPU: {CpuTempSource} • GPU: {GpuTempSource}";

        public string SensorHealth
        {
            get
            {
                if (CpuTemperatureValue.HasValue || GpuTemperatureValue.HasValue)
                {
                    return "Available";
                }

                return "Unavailable";
            }
        }

        // Legacy property aliases for backward compatibility
        public string Name { get => PCName; set => PCName = value; }
        public string IP { get => IPAddress; set => IPAddress = value; }
        public SolidColorBrush StatusColor
        {
            get => Status == "Online" ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) :
                   Status == "Offline" ? new SolidColorBrush(Color.FromRgb(239, 68, 68)) :
                   new SolidColorBrush(Color.FromRgb(245, 158, 11));
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
}
