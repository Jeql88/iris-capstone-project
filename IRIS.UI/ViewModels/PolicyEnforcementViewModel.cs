using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using IRIS.UI.Helpers;
using IRIS.Core.Services.Contracts;
using IRIS.Core.Models;
using IRIS.Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;

namespace IRIS.UI.ViewModels
{
    public class PolicyEnforcementViewModel : INotifyPropertyChanged
    {
        private readonly IMonitoringService _monitoringService;
        private readonly IPolicyService _policyService;
        private readonly IRISDbContext _dbContext;
        private readonly DispatcherTimer _refreshTimer;
        private readonly DispatcherTimer _messageTimer;
        private bool _wallpaperResetEnabled;
        private bool _autoShutdownEnabled;
        private int _autoShutdownMinutes = 30;
        private string _selectedWallpaperPath = "No wallpaper selected";
        private string _selectionStatusText = "No rooms selected";
        private string _lastAppliedText = "Never applied";
        private string _statusMessage = string.Empty;
        private string _statusMessageColor = "#10B981";
        
        // Original values for change tracking
        private bool _originalWallpaperResetEnabled;
        private bool _originalAutoShutdownEnabled;
        private int _originalAutoShutdownMinutes;
        private string _originalWallpaperPath = string.Empty;

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
            try
            {
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
                    WallpaperResetEnabled && !string.IsNullOrEmpty(SelectedWallpaperPath) && SelectedWallpaperPath != "No wallpaper selected" ? SelectedWallpaperPath : null
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
                   SelectedWallpaperPath != _originalWallpaperPath;
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
            
            StatusMessage = string.Empty;
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
                
                // Store original values for change tracking
                _originalWallpaperResetEnabled = WallpaperResetEnabled;
                _originalAutoShutdownEnabled = AutoShutdownEnabled;
                _originalAutoShutdownMinutes = AutoShutdownMinutes;
                _originalWallpaperPath = SelectedWallpaperPath;
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
                
                // Store original values for change tracking
                _originalWallpaperResetEnabled = WallpaperResetEnabled;
                _originalAutoShutdownEnabled = AutoShutdownEnabled;
                _originalAutoShutdownMinutes = AutoShutdownMinutes;
                _originalWallpaperPath = SelectedWallpaperPath;
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