using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using IRIS.UI.Helpers;
using IRIS.UI.Services;
using IRIS.Core.Services.Contracts;
using IRIS.Core.Models;
using IRIS.Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;

namespace IRIS.UI.ViewModels
{
    public class PolicyEnforcementViewModel : INotifyPropertyChanged, INavigationAware
    {
        private readonly IMonitoringService _monitoringService;
        private readonly IPolicyService _policyService;
        private readonly IRISDbContext _dbContext;
        private readonly DispatcherTimer _refreshTimer;
        private readonly DispatcherTimer _messageTimer;
        private bool _wallpaperResetEnabled;
        private bool _autoShutdownEnabled;
        private int _autoShutdownMinutes = 30;
        private double _cpuWarningThreshold = 85;
        private double _cpuCriticalThreshold = 95;
        private double _ramWarningThreshold = 85;
        private double _ramCriticalThreshold = 95;
        private double _diskWarningThreshold = 90;
        private double _diskCriticalThreshold = 98;
        private double _cpuTempWarningThreshold = 80;
        private double _cpuTempCriticalThreshold = 90;
        private double _gpuTempWarningThreshold = 80;
        private double _gpuTempCriticalThreshold = 90;
        private double _latencyWarningThreshold = 150;
        private double _latencyCriticalThreshold = 300;
        private double _packetLossWarningThreshold = 3;
        private double _packetLossCriticalThreshold = 10;
        private string _selectedWallpaperPath = "No wallpaper selected";
        private string _selectionStatusText = "No rooms selected";
        private string _lastAppliedText = "Never applied";
        private string _statusMessage = string.Empty;
        private string _statusMessageColor = "#10B981";
        private readonly SemaphoreSlim _loadRoomDataSemaphore = new(1, 1);
        private bool _isActive = true;
        
        // Original values for change tracking
        private bool _originalWallpaperResetEnabled;
        private bool _originalAutoShutdownEnabled;
        private int _originalAutoShutdownMinutes;
        private string _originalWallpaperPath = string.Empty;
        private double _originalCpuWarningThreshold = 85;
        private double _originalCpuCriticalThreshold = 95;
        private double _originalRamWarningThreshold = 85;
        private double _originalRamCriticalThreshold = 95;
        private double _originalDiskWarningThreshold = 90;
        private double _originalDiskCriticalThreshold = 98;
        private double _originalCpuTempWarningThreshold = 80;
        private double _originalCpuTempCriticalThreshold = 90;
        private double _originalGpuTempWarningThreshold = 80;
        private double _originalGpuTempCriticalThreshold = 90;
        private double _originalLatencyWarningThreshold = 150;
        private double _originalLatencyCriticalThreshold = 300;
        private double _originalPacketLossWarningThreshold = 3;
        private double _originalPacketLossCriticalThreshold = 10;

        public ObservableCollection<RoomItem> Rooms { get; set; }
        public ObservableCollection<RoomPolicyDisplay> SelectedRoomPolicies { get; set; }

        public bool WallpaperResetEnabled
        {
            get => _wallpaperResetEnabled;
            set
            {
                _wallpaperResetEnabled = value;
                OnPropertyChanged();
                ((RelayCommand)ApplyPoliciesCommand).RaiseCanExecuteChanged();
            }
        }

        public bool AutoShutdownEnabled
        {
            get => _autoShutdownEnabled;
            set
            {
                _autoShutdownEnabled = value;
                OnPropertyChanged();
                ((RelayCommand)ApplyPoliciesCommand).RaiseCanExecuteChanged();
            }
        }

        public int AutoShutdownMinutes
        {
            get => _autoShutdownMinutes;
            set
            {
                _autoShutdownMinutes = value;
                OnPropertyChanged();
                ((RelayCommand)ApplyPoliciesCommand).RaiseCanExecuteChanged();
            }
        }

        public double CpuWarningThreshold
        {
            get => _cpuWarningThreshold;
            set { _cpuWarningThreshold = value; OnPropertyChanged(); ((RelayCommand)ApplyPoliciesCommand).RaiseCanExecuteChanged(); }
        }

        public double CpuCriticalThreshold
        {
            get => _cpuCriticalThreshold;
            set { _cpuCriticalThreshold = value; OnPropertyChanged(); ((RelayCommand)ApplyPoliciesCommand).RaiseCanExecuteChanged(); }
        }

        public double RamWarningThreshold
        {
            get => _ramWarningThreshold;
            set { _ramWarningThreshold = value; OnPropertyChanged(); ((RelayCommand)ApplyPoliciesCommand).RaiseCanExecuteChanged(); }
        }

        public double RamCriticalThreshold
        {
            get => _ramCriticalThreshold;
            set { _ramCriticalThreshold = value; OnPropertyChanged(); ((RelayCommand)ApplyPoliciesCommand).RaiseCanExecuteChanged(); }
        }

        public double DiskWarningThreshold
        {
            get => _diskWarningThreshold;
            set { _diskWarningThreshold = value; OnPropertyChanged(); ((RelayCommand)ApplyPoliciesCommand).RaiseCanExecuteChanged(); }
        }

        public double DiskCriticalThreshold
        {
            get => _diskCriticalThreshold;
            set { _diskCriticalThreshold = value; OnPropertyChanged(); ((RelayCommand)ApplyPoliciesCommand).RaiseCanExecuteChanged(); }
        }

        public double CpuTempWarningThreshold
        {
            get => _cpuTempWarningThreshold;
            set { _cpuTempWarningThreshold = value; OnPropertyChanged(); ((RelayCommand)ApplyPoliciesCommand).RaiseCanExecuteChanged(); }
        }

        public double CpuTempCriticalThreshold
        {
            get => _cpuTempCriticalThreshold;
            set { _cpuTempCriticalThreshold = value; OnPropertyChanged(); ((RelayCommand)ApplyPoliciesCommand).RaiseCanExecuteChanged(); }
        }

        public double GpuTempWarningThreshold
        {
            get => _gpuTempWarningThreshold;
            set { _gpuTempWarningThreshold = value; OnPropertyChanged(); ((RelayCommand)ApplyPoliciesCommand).RaiseCanExecuteChanged(); }
        }

        public double GpuTempCriticalThreshold
        {
            get => _gpuTempCriticalThreshold;
            set { _gpuTempCriticalThreshold = value; OnPropertyChanged(); ((RelayCommand)ApplyPoliciesCommand).RaiseCanExecuteChanged(); }
        }

        public double LatencyWarningThreshold
        {
            get => _latencyWarningThreshold;
            set { _latencyWarningThreshold = value; OnPropertyChanged(); ((RelayCommand)ApplyPoliciesCommand).RaiseCanExecuteChanged(); }
        }

        public double LatencyCriticalThreshold
        {
            get => _latencyCriticalThreshold;
            set { _latencyCriticalThreshold = value; OnPropertyChanged(); ((RelayCommand)ApplyPoliciesCommand).RaiseCanExecuteChanged(); }
        }

        public double PacketLossWarningThreshold
        {
            get => _packetLossWarningThreshold;
            set { _packetLossWarningThreshold = value; OnPropertyChanged(); ((RelayCommand)ApplyPoliciesCommand).RaiseCanExecuteChanged(); }
        }

        public double PacketLossCriticalThreshold
        {
            get => _packetLossCriticalThreshold;
            set { _packetLossCriticalThreshold = value; OnPropertyChanged(); ((RelayCommand)ApplyPoliciesCommand).RaiseCanExecuteChanged(); }
        }

        public string SelectedWallpaperPath
        {
            get => _selectedWallpaperPath;
            set
            {
                _selectedWallpaperPath = value;
                OnPropertyChanged();
                ((RelayCommand)ApplyPoliciesCommand).RaiseCanExecuteChanged();
            }
        }

        public string SelectionStatusText
        {
            get => _selectionStatusText;
            set
            {
                _selectionStatusText = value;
                OnPropertyChanged();
            }
        }

        public string LastAppliedText
        {
            get => _lastAppliedText;
            set
            {
                _lastAppliedText = value;
                OnPropertyChanged();
            }
        }

        public bool HasSelectedRoom => Rooms?.Any(r => r.IsSelected) == true;
        public string SelectedRoomText
        {
            get
            {
                var selectedRoom = Rooms?.FirstOrDefault(r => r.IsSelected);
                return selectedRoom != null ? $"Configuring policies for {selectedRoom.RoomNumber}" : "No laboratory selected";
            }
        }
        public string CurrentWallpaperStatus { get; private set; } = "OFF";
        public string CurrentWallpaperStatusColor { get; private set; } = "#EF4444";
        public string CurrentShutdownStatus { get; private set; } = "OFF";
        public string CurrentShutdownStatusColor { get; private set; } = "#EF4444";
        
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasStatusMessage));
            }
        }

        public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);
        
        public string StatusMessageColor
        {
            get => _statusMessageColor;
            set
            {
                _statusMessageColor = value;
                OnPropertyChanged();
            }
        }

        public ICommand ApplyPoliciesCommand { get; }
        public ICommand ClearSelectionCommand { get; }
        public ICommand BrowseWallpaperCommand { get; }
        public ICommand LoadCurrentSettingsCommand { get; }

        public PolicyEnforcementViewModel(IMonitoringService monitoringService, IPolicyService policyService, IRISDbContext dbContext)
        {
            _monitoringService = monitoringService;
            _policyService = policyService;
            _dbContext = dbContext;
            Rooms = new ObservableCollection<RoomItem>();
            SelectedRoomPolicies = new ObservableCollection<RoomPolicyDisplay>();

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _refreshTimer.Tick += async (s, e) => await LoadRoomDataAsync();
            _refreshTimer.Start();
            _ = LoadRoomDataAsync();

            _messageTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _messageTimer.Tick += (s, e) => ClearStatusMessage();

            ApplyPoliciesCommand = new RelayCommand(async () => await ApplyPoliciesAsync(), CanApplyPolicies);
            ClearSelectionCommand = new RelayCommand(async () => { ClearRoomSelection(); await Task.CompletedTask; }, () => true);
            BrowseWallpaperCommand = new RelayCommand(async () => { BrowseWallpaper(); await Task.CompletedTask; }, () => true);
            LoadCurrentSettingsCommand = new RelayCommand(async () => await LoadCurrentSettingsAsync(), () => Rooms?.Any(r => r.IsSelected) == true);
        }

        private async Task LoadRoomDataAsync()
        {
            if (!_isActive)
            {
                return;
            }

            if (!await _loadRoomDataSemaphore.WaitAsync(0))
            {
                return;
            }

            try
            {
                if (!_isActive)
                {
                    return;
                }

                var rooms = await _dbContext.Rooms
                    .Include(r => r.PCs)
                    .Include(r => r.Policies)
                    .Where(r => r.IsActive)
                    .ToListAsync();

                var roomsWithCounts = await _monitoringService.GetActiveLabPCsAsync();

                // Preserve current selection
                var selectedRoomId = Rooms.FirstOrDefault(r => r.IsSelected)?.Id;

                Rooms.Clear();
                foreach (var room in rooms)
                {
                    var onlineCount = roomsWithCounts.ContainsKey(room.RoomNumber) ? roomsWithCounts[room.RoomNumber] : 0;
                    var totalCount = room.PCs.Count();
                    
                    var activePolicies = new List<string>();
                    
                    foreach (var policy in room.Policies.Where(p => p.IsActive))
                    {
                        if (policy.ResetWallpaperOnStartup)
                            activePolicies.Add("Wallpaper Reset");
                        if (policy.AutoShutdownIdleMinutes.HasValue)
                            activePolicies.Add($"Auto-Shutdown ({policy.AutoShutdownIdleMinutes}min)");

                        activePolicies.Add("Threshold Profile");
                    }

                    Rooms.Add(new RoomItem
                    {
                        Id = room.Id,
                        RoomNumber = room.RoomNumber,
                        Description = room.Description ?? "Computer Laboratory",
                        OnlineCount = onlineCount,
                        TotalCount = totalCount,
                        IsSelected = selectedRoomId.HasValue && room.Id == selectedRoomId.Value,
                        ActivePolicies = activePolicies
                    });
                }

                UpdateSelectionStatus();
                UpdateSelectedRoomPolicies();
            }
            catch (Exception ex)
            {
                LoadMockRoomData();
                SelectionStatusText = "Error loading rooms: " + ex.Message;
            }
            finally
            {
                _loadRoomDataSemaphore.Release();
            }
        }

        private void LoadMockRoomData()
        {
            Rooms.Clear();
            Rooms.Add(new RoomItem { Id = 1, RoomNumber = "Lab 1", Description = "Computer Laboratory 1", OnlineCount = 0, TotalCount = 0, IsSelected = false, ActivePolicies = new List<string> { "Wallpaper Reset" } });
            Rooms.Add(new RoomItem { Id = 2, RoomNumber = "Lab 2", Description = "Computer Laboratory 2", OnlineCount = 0, TotalCount = 0, IsSelected = false, ActivePolicies = new List<string>() });
            Rooms.Add(new RoomItem { Id = 3, RoomNumber = "Lab 3", Description = "Computer Laboratory 3", OnlineCount = 0, TotalCount = 0, IsSelected = false, ActivePolicies = new List<string> { "Auto-Shutdown (30min)" } });
            Rooms.Add(new RoomItem { Id = 4, RoomNumber = "Lab 4", Description = "Computer Laboratory 4", OnlineCount = 0, TotalCount = 0, IsSelected = false, ActivePolicies = new List<string>() });
            UpdateSelectionStatus();
        }

        private async Task ApplyPoliciesAsync()
        {
            try
            {
                var selectedRoom = Rooms.FirstOrDefault(r => r.IsSelected);
                if (selectedRoom == null) return;
                
                // Validate settings before applying
                if (!ValidatePolicySettings())
                {
                    return;
                }
                
                await _policyService.CreateOrUpdatePolicyAsync(
                    selectedRoom.Id, 
                    WallpaperResetEnabled, 
                    AutoShutdownEnabled ? AutoShutdownMinutes : null,
                    WallpaperResetEnabled && !string.IsNullOrEmpty(SelectedWallpaperPath) && SelectedWallpaperPath != "No wallpaper selected" ? SelectedWallpaperPath : null,
                    CpuWarningThreshold,
                    CpuCriticalThreshold,
                    RamWarningThreshold,
                    RamCriticalThreshold,
                    DiskWarningThreshold,
                    DiskCriticalThreshold,
                    CpuTempWarningThreshold,
                    CpuTempCriticalThreshold,
                    GpuTempWarningThreshold,
                    GpuTempCriticalThreshold,
                    LatencyWarningThreshold,
                    LatencyCriticalThreshold,
                    PacketLossWarningThreshold,
                    PacketLossCriticalThreshold
                );

                StatusMessage = $"Policies successfully deployed to {selectedRoom.RoomNumber}";
                StatusMessageColor = "#10B981";
                StartMessageTimer();
                LastAppliedText = $"Applied to {selectedRoom.RoomNumber} at {DateTime.Now:HH:mm:ss}";
                
                await LoadRoomDataAsync();
                LoadCurrentPolicySettings();
                
                // Update original values after successful deployment
                _originalWallpaperResetEnabled = WallpaperResetEnabled;
                _originalAutoShutdownEnabled = AutoShutdownEnabled;
                _originalAutoShutdownMinutes = AutoShutdownMinutes;
                _originalWallpaperPath = SelectedWallpaperPath;
                _originalCpuWarningThreshold = CpuWarningThreshold;
                _originalCpuCriticalThreshold = CpuCriticalThreshold;
                _originalRamWarningThreshold = RamWarningThreshold;
                _originalRamCriticalThreshold = RamCriticalThreshold;
                _originalDiskWarningThreshold = DiskWarningThreshold;
                _originalDiskCriticalThreshold = DiskCriticalThreshold;
                _originalCpuTempWarningThreshold = CpuTempWarningThreshold;
                _originalCpuTempCriticalThreshold = CpuTempCriticalThreshold;
                _originalGpuTempWarningThreshold = GpuTempWarningThreshold;
                _originalGpuTempCriticalThreshold = GpuTempCriticalThreshold;
                _originalLatencyWarningThreshold = LatencyWarningThreshold;
                _originalLatencyCriticalThreshold = LatencyCriticalThreshold;
                _originalPacketLossWarningThreshold = PacketLossWarningThreshold;
                _originalPacketLossCriticalThreshold = PacketLossCriticalThreshold;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to deploy policies: {ex.Message}";
                StatusMessageColor = "#EF4444";
                StartMessageTimer();
                LastAppliedText = "Failed to apply";
            }
        }

        private void ClearRoomSelection()
        {
            foreach (var room in Rooms)
            {
                room.IsSelected = false;
            }
            UpdateSelectionStatus();
            UpdateSelectedRoomPolicies();
            OnPropertyChanged(nameof(HasSelectedRoom));
            ((RelayCommand)ApplyPoliciesCommand).RaiseCanExecuteChanged();
            ((RelayCommand)LoadCurrentSettingsCommand).RaiseCanExecuteChanged();
        }

        public void ToggleRoom(RoomItem? room)
        {
            if (room != null)
            {
                // Clear all other selections (single selection only)
                foreach (var r in Rooms)
                {
                    r.IsSelected = false;
                }
                
                // Select the clicked room
                room.IsSelected = true;
                
                // Clear status messages when switching rooms
                ClearStatusMessage();
                
                UpdateSelectionStatus();
                LoadCurrentPolicySettings();
                OnPropertyChanged(nameof(HasSelectedRoom));
                OnPropertyChanged(nameof(SelectedRoomText));
                ((RelayCommand)ApplyPoliciesCommand).RaiseCanExecuteChanged();
            }
        }

        private bool CanApplyPolicies()
        {
            if (!Rooms.Any(r => r.IsSelected)) return false;
            
            // Check if any changes were made
            return WallpaperResetEnabled != _originalWallpaperResetEnabled ||
                   AutoShutdownEnabled != _originalAutoShutdownEnabled ||
                   AutoShutdownMinutes != _originalAutoShutdownMinutes ||
                   SelectedWallpaperPath != _originalWallpaperPath ||
                   CpuWarningThreshold != _originalCpuWarningThreshold ||
                   CpuCriticalThreshold != _originalCpuCriticalThreshold ||
                   RamWarningThreshold != _originalRamWarningThreshold ||
                   RamCriticalThreshold != _originalRamCriticalThreshold ||
                   DiskWarningThreshold != _originalDiskWarningThreshold ||
                   DiskCriticalThreshold != _originalDiskCriticalThreshold ||
                   CpuTempWarningThreshold != _originalCpuTempWarningThreshold ||
                   CpuTempCriticalThreshold != _originalCpuTempCriticalThreshold ||
                   GpuTempWarningThreshold != _originalGpuTempWarningThreshold ||
                   GpuTempCriticalThreshold != _originalGpuTempCriticalThreshold ||
                   LatencyWarningThreshold != _originalLatencyWarningThreshold ||
                   LatencyCriticalThreshold != _originalLatencyCriticalThreshold ||
                   PacketLossWarningThreshold != _originalPacketLossWarningThreshold ||
                   PacketLossCriticalThreshold != _originalPacketLossCriticalThreshold;
        }
        
        private bool ValidatePolicySettings()
        {
            if (WallpaperResetEnabled && (string.IsNullOrEmpty(SelectedWallpaperPath) || SelectedWallpaperPath == "No wallpaper selected"))
            {
                StatusMessage = "Please select a wallpaper image when wallpaper reset is enabled.";
                StatusMessageColor = "#EF4444";
                StartMessageTimer();
                return false;
            }

            if (!ValidateThresholdPair(CpuWarningThreshold, CpuCriticalThreshold, "CPU usage") ||
                !ValidateThresholdPair(RamWarningThreshold, RamCriticalThreshold, "RAM usage") ||
                !ValidateThresholdPair(DiskWarningThreshold, DiskCriticalThreshold, "Disk usage") ||
                !ValidateThresholdPair(CpuTempWarningThreshold, CpuTempCriticalThreshold, "CPU temperature") ||
                !ValidateThresholdPair(GpuTempWarningThreshold, GpuTempCriticalThreshold, "GPU temperature") ||
                !ValidateThresholdPair(LatencyWarningThreshold, LatencyCriticalThreshold, "Latency") ||
                !ValidateThresholdPair(PacketLossWarningThreshold, PacketLossCriticalThreshold, "Packet loss"))
            {
                return false;
            }
            
            StatusMessage = string.Empty;
            return true;
        }

        private bool ValidateThresholdPair(double warning, double critical, string metricName)
        {
            if (warning < 0 || critical < 0)
            {
                StatusMessage = $"{metricName} thresholds must be non-negative.";
                StatusMessageColor = "#EF4444";
                StartMessageTimer();
                return false;
            }

            if (warning >= critical)
            {
                StatusMessage = $"{metricName} warning threshold must be lower than critical.";
                StatusMessageColor = "#EF4444";
                StartMessageTimer();
                return false;
            }

            return true;
        }

        private void LoadCurrentPolicySettings()
        {
            var selectedRoom = Rooms.FirstOrDefault(r => r.IsSelected);
            if (selectedRoom == null) return;

            var roomData = _dbContext?.Rooms
                .Include(r => r.Policies)
                .FirstOrDefault(r => r.Id == selectedRoom.Id);
                
            var activePolicy = roomData?.Policies?.FirstOrDefault(p => p.IsActive);
            
            if (activePolicy != null)
            {
                // Update current status display
                CurrentWallpaperStatus = activePolicy.ResetWallpaperOnStartup ? "ON" : "OFF";
                CurrentWallpaperStatusColor = activePolicy.ResetWallpaperOnStartup ? "#10B981" : "#EF4444";
                
                CurrentShutdownStatus = activePolicy.AutoShutdownIdleMinutes.HasValue ? $"{activePolicy.AutoShutdownIdleMinutes}min" : "OFF";
                CurrentShutdownStatusColor = activePolicy.AutoShutdownIdleMinutes.HasValue ? "#F59E0B" : "#EF4444";
                
                // Load settings into form controls
                WallpaperResetEnabled = activePolicy.ResetWallpaperOnStartup;
                AutoShutdownEnabled = activePolicy.AutoShutdownIdleMinutes.HasValue;
                AutoShutdownMinutes = activePolicy.AutoShutdownIdleMinutes ?? 30;
                
                if (!string.IsNullOrEmpty(activePolicy.WallpaperPath))
                {
                    SelectedWallpaperPath = activePolicy.WallpaperPath;
                }
                else
                {
                    SelectedWallpaperPath = "No wallpaper selected";
                }

                CpuWarningThreshold = activePolicy.CpuUsageWarningThreshold;
                CpuCriticalThreshold = activePolicy.CpuUsageCriticalThreshold;
                RamWarningThreshold = activePolicy.RamUsageWarningThreshold;
                RamCriticalThreshold = activePolicy.RamUsageCriticalThreshold;
                DiskWarningThreshold = activePolicy.DiskUsageWarningThreshold;
                DiskCriticalThreshold = activePolicy.DiskUsageCriticalThreshold;
                CpuTempWarningThreshold = activePolicy.CpuTemperatureWarningThreshold;
                CpuTempCriticalThreshold = activePolicy.CpuTemperatureCriticalThreshold;
                GpuTempWarningThreshold = activePolicy.GpuTemperatureWarningThreshold;
                GpuTempCriticalThreshold = activePolicy.GpuTemperatureCriticalThreshold;
                LatencyWarningThreshold = activePolicy.LatencyWarningThreshold;
                LatencyCriticalThreshold = activePolicy.LatencyCriticalThreshold;
                PacketLossWarningThreshold = activePolicy.PacketLossWarningThreshold;
                PacketLossCriticalThreshold = activePolicy.PacketLossCriticalThreshold;
                
                // Store original values for change tracking
                _originalWallpaperResetEnabled = WallpaperResetEnabled;
                _originalAutoShutdownEnabled = AutoShutdownEnabled;
                _originalAutoShutdownMinutes = AutoShutdownMinutes;
                _originalWallpaperPath = SelectedWallpaperPath;
                _originalCpuWarningThreshold = CpuWarningThreshold;
                _originalCpuCriticalThreshold = CpuCriticalThreshold;
                _originalRamWarningThreshold = RamWarningThreshold;
                _originalRamCriticalThreshold = RamCriticalThreshold;
                _originalDiskWarningThreshold = DiskWarningThreshold;
                _originalDiskCriticalThreshold = DiskCriticalThreshold;
                _originalCpuTempWarningThreshold = CpuTempWarningThreshold;
                _originalCpuTempCriticalThreshold = CpuTempCriticalThreshold;
                _originalGpuTempWarningThreshold = GpuTempWarningThreshold;
                _originalGpuTempCriticalThreshold = GpuTempCriticalThreshold;
                _originalLatencyWarningThreshold = LatencyWarningThreshold;
                _originalLatencyCriticalThreshold = LatencyCriticalThreshold;
                _originalPacketLossWarningThreshold = PacketLossWarningThreshold;
                _originalPacketLossCriticalThreshold = PacketLossCriticalThreshold;
            }
            else
            {
                // No active policy
                CurrentWallpaperStatus = "OFF";
                CurrentWallpaperStatusColor = "#EF4444";
                CurrentShutdownStatus = "OFF";
                CurrentShutdownStatusColor = "#EF4444";
                
                WallpaperResetEnabled = false;
                AutoShutdownEnabled = false;
                AutoShutdownMinutes = 30;
                SelectedWallpaperPath = "No wallpaper selected";
                CpuWarningThreshold = 85;
                CpuCriticalThreshold = 95;
                RamWarningThreshold = 85;
                RamCriticalThreshold = 95;
                DiskWarningThreshold = 90;
                DiskCriticalThreshold = 98;
                CpuTempWarningThreshold = 80;
                CpuTempCriticalThreshold = 90;
                GpuTempWarningThreshold = 80;
                GpuTempCriticalThreshold = 90;
                LatencyWarningThreshold = 150;
                LatencyCriticalThreshold = 300;
                PacketLossWarningThreshold = 3;
                PacketLossCriticalThreshold = 10;
                
                // Store original values for change tracking
                _originalWallpaperResetEnabled = WallpaperResetEnabled;
                _originalAutoShutdownEnabled = AutoShutdownEnabled;
                _originalAutoShutdownMinutes = AutoShutdownMinutes;
                _originalWallpaperPath = SelectedWallpaperPath;
                _originalCpuWarningThreshold = CpuWarningThreshold;
                _originalCpuCriticalThreshold = CpuCriticalThreshold;
                _originalRamWarningThreshold = RamWarningThreshold;
                _originalRamCriticalThreshold = RamCriticalThreshold;
                _originalDiskWarningThreshold = DiskWarningThreshold;
                _originalDiskCriticalThreshold = DiskCriticalThreshold;
                _originalCpuTempWarningThreshold = CpuTempWarningThreshold;
                _originalCpuTempCriticalThreshold = CpuTempCriticalThreshold;
                _originalGpuTempWarningThreshold = GpuTempWarningThreshold;
                _originalGpuTempCriticalThreshold = GpuTempCriticalThreshold;
                _originalLatencyWarningThreshold = LatencyWarningThreshold;
                _originalLatencyCriticalThreshold = LatencyCriticalThreshold;
                _originalPacketLossWarningThreshold = PacketLossWarningThreshold;
                _originalPacketLossCriticalThreshold = PacketLossCriticalThreshold;
            }
            
            OnPropertyChanged(nameof(CurrentWallpaperStatus));
            OnPropertyChanged(nameof(CurrentWallpaperStatusColor));
            OnPropertyChanged(nameof(CurrentShutdownStatus));
            OnPropertyChanged(nameof(CurrentShutdownStatusColor));
        }

        private void UpdateSelectionStatus()
        {
            var selectedCount = Rooms.Count(r => r.IsSelected);
            SelectionStatusText = selectedCount == 0 ? "No rooms selected" : 
                                selectedCount == 1 ? "1 room selected" : 
                                $"{selectedCount} rooms selected";
        }

        private void UpdateSelectedRoomPolicies()
        {
            SelectedRoomPolicies.Clear();
            
            var selectedRooms = Rooms.Where(r => r.IsSelected).ToList();
            foreach (var room in selectedRooms)
            {
                var roomData = _dbContext?.Rooms
                    .Include(r => r.Policies)
                    .FirstOrDefault(r => r.Id == room.Id);
                    
                var activePolicy = roomData?.Policies?.FirstOrDefault(p => p.IsActive);
                
                var policyDisplay = new RoomPolicyDisplay
                {
                    RoomNumber = room.RoomNumber,
                    HasActivePolicy = activePolicy != null,
                    WallpaperEnabled = activePolicy?.ResetWallpaperOnStartup ?? false,
                    AutoShutdownEnabled = activePolicy?.AutoShutdownIdleMinutes.HasValue ?? false,
                    AutoShutdownMinutes = activePolicy?.AutoShutdownIdleMinutes,
                    LastUpdated = activePolicy?.UpdatedAt?.ToString("MMM dd, HH:mm") ?? 
                                 activePolicy?.CreatedAt.ToString("MMM dd, HH:mm") ?? "Never"
                };
                
                SelectedRoomPolicies.Add(policyDisplay);
            }
        }

        private void BrowseWallpaper()
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select Wallpaper Image",
                Filter = "Image files (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|All files (*.*)|*.*",
                FilterIndex = 1
            };

            if (openFileDialog.ShowDialog() == true)
            {
                SelectedWallpaperPath = openFileDialog.FileName;
            }
        }

        private void StartMessageTimer()
        {
            _messageTimer.Stop();
            _messageTimer.Start();
        }

        private void ClearStatusMessage()
        {
            _messageTimer.Stop();
            StatusMessage = string.Empty;
        }

        private async Task LoadCurrentSettingsAsync()
        {
            try
            {
                var selectedRoom = Rooms.FirstOrDefault(r => r.IsSelected);
                if (selectedRoom == null) return;

                var roomData = await _dbContext.Rooms
                    .Include(r => r.Policies)
                    .FirstOrDefaultAsync(r => r.Id == selectedRoom.Id);

                var activePolicy = roomData?.Policies?.FirstOrDefault(p => p.IsActive);
                
                if (activePolicy != null)
                {
                    WallpaperResetEnabled = activePolicy.ResetWallpaperOnStartup;
                    AutoShutdownEnabled = activePolicy.AutoShutdownIdleMinutes.HasValue;
                    AutoShutdownMinutes = activePolicy.AutoShutdownIdleMinutes ?? 30;
                    CpuWarningThreshold = activePolicy.CpuUsageWarningThreshold;
                    CpuCriticalThreshold = activePolicy.CpuUsageCriticalThreshold;
                    RamWarningThreshold = activePolicy.RamUsageWarningThreshold;
                    RamCriticalThreshold = activePolicy.RamUsageCriticalThreshold;
                    DiskWarningThreshold = activePolicy.DiskUsageWarningThreshold;
                    DiskCriticalThreshold = activePolicy.DiskUsageCriticalThreshold;
                    CpuTempWarningThreshold = activePolicy.CpuTemperatureWarningThreshold;
                    CpuTempCriticalThreshold = activePolicy.CpuTemperatureCriticalThreshold;
                    GpuTempWarningThreshold = activePolicy.GpuTemperatureWarningThreshold;
                    GpuTempCriticalThreshold = activePolicy.GpuTemperatureCriticalThreshold;
                    LatencyWarningThreshold = activePolicy.LatencyWarningThreshold;
                    LatencyCriticalThreshold = activePolicy.LatencyCriticalThreshold;
                    PacketLossWarningThreshold = activePolicy.PacketLossWarningThreshold;
                    PacketLossCriticalThreshold = activePolicy.PacketLossCriticalThreshold;
                    
                    if (!string.IsNullOrEmpty(activePolicy.WallpaperPath))
                    {
                        SelectedWallpaperPath = activePolicy.WallpaperPath;
                    }
                }
                else
                {
                    WallpaperResetEnabled = false;
                    AutoShutdownEnabled = false;
                    AutoShutdownMinutes = 30;
                    SelectedWallpaperPath = "No wallpaper selected";
                    CpuWarningThreshold = 85;
                    CpuCriticalThreshold = 95;
                    RamWarningThreshold = 85;
                    RamCriticalThreshold = 95;
                    DiskWarningThreshold = 90;
                    DiskCriticalThreshold = 98;
                    CpuTempWarningThreshold = 80;
                    CpuTempCriticalThreshold = 90;
                    GpuTempWarningThreshold = 80;
                    GpuTempCriticalThreshold = 90;
                    LatencyWarningThreshold = 150;
                    LatencyCriticalThreshold = 300;
                    PacketLossWarningThreshold = 3;
                    PacketLossCriticalThreshold = 10;
                }
            }
            catch (Exception)
            {
                // Silently handle errors when loading current settings
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void OnNavigatedFrom()
        {
            _isActive = false;
            _refreshTimer.Stop();
            _messageTimer.Stop();
        }
    }

    public class RoomItem : INotifyPropertyChanged
    {
        private int _id;
        private string _roomNumber = string.Empty;
        private string _description = string.Empty;
        private int _onlineCount;
        private int _totalCount;
        private bool _isSelected;
        private List<string> _activePolicies = new();

        public int Id
        {
            get => _id;
            set
            {
                _id = value;
                OnPropertyChanged();
            }
        }

        public string RoomNumber
        {
            get => _roomNumber;
            set
            {
                _roomNumber = value;
                OnPropertyChanged();
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged();
            }
        }

        public int OnlineCount
        {
            get => _onlineCount;
            set
            {
                _onlineCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
            }
        }

        public int TotalCount
        {
            get => _totalCount;
            set
            {
                _totalCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
            }
        }

        public string StatusText => $"{OnlineCount}/{TotalCount} Online";

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public List<string> ActivePolicies
        {
            get => _activePolicies;
            set
            {
                _activePolicies = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasActivePolicies));
            }
        }

        public bool HasActivePolicies => ActivePolicies.Any();
        public bool WallpaperPolicyEnabled => ActivePolicies.Any(p => p.Contains("Wallpaper"));
        public bool AutoShutdownPolicyEnabled => ActivePolicies.Any(p => p.Contains("Auto-Shutdown"));
        public string AutoShutdownMinutesText
        {
            get
            {
                var policy = ActivePolicies.FirstOrDefault(p => p.Contains("Auto-Shutdown"));
                if (policy != null)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(policy, @"\((\d+)min\)");
                    if (match.Success) return match.Groups[1].Value + "min";
                }
                return "ON";
            }
        }
        public string PolicyStatusText => HasActivePolicies ? "Active" : "No Policies";
        public string PolicyStatusColor => HasActivePolicies ? "#10B981" : "#6B7280";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class RoomPolicyDisplay : INotifyPropertyChanged
    {
        public string RoomNumber { get; set; } = string.Empty;
        public bool HasActivePolicy { get; set; }
        public bool WallpaperEnabled { get; set; }
        public bool AutoShutdownEnabled { get; set; }
        public int? AutoShutdownMinutes { get; set; }
        public string LastUpdated { get; set; } = string.Empty;

        public string StatusText => HasActivePolicy ? "Active" : "No Policies";
        public string StatusColor => HasActivePolicy ? "#10B981" : "#6B7280";
        
        public string WallpaperStatus => WallpaperEnabled ? "ON" : "OFF";
        public string WallpaperStatusColor => WallpaperEnabled ? "#10B981" : "#EF4444";
        
        public string ShutdownStatus => AutoShutdownEnabled ? $"{AutoShutdownMinutes}min" : "OFF";
        public string ShutdownStatusColor => AutoShutdownEnabled ? "#F59E0B" : "#EF4444";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }
}