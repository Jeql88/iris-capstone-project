using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using IRIS.UI.Helpers;
using IRIS.Core.Services;
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
        private bool _wallpaperResetEnabled;
        private bool _autoShutdownEnabled;
        private int _autoShutdownMinutes = 30;
        private string _selectedWallpaperPath = "No wallpaper selected";
        private string _selectionStatusText = "No rooms selected";
        private string _lastAppliedText = "Never applied";

        public ObservableCollection<RoomItem> Rooms { get; set; }

        public bool WallpaperResetEnabled
        {
            get => _wallpaperResetEnabled;
            set
            {
                _wallpaperResetEnabled = value;
                OnPropertyChanged();
            }
        }

        public bool AutoShutdownEnabled
        {
            get => _autoShutdownEnabled;
            set
            {
                _autoShutdownEnabled = value;
                OnPropertyChanged();
            }
        }

        public int AutoShutdownMinutes
        {
            get => _autoShutdownMinutes;
            set
            {
                _autoShutdownMinutes = value;
                OnPropertyChanged();
            }
        }

        public string SelectedWallpaperPath
        {
            get => _selectedWallpaperPath;
            set
            {
                _selectedWallpaperPath = value;
                OnPropertyChanged();
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

        public ICommand ApplyPoliciesCommand { get; }
        public ICommand ClearSelectionCommand { get; }
        public ICommand BrowseWallpaperCommand { get; }

        public PolicyEnforcementViewModel() : this(null!, null!, null!) { }

        public PolicyEnforcementViewModel(IMonitoringService monitoringService, IPolicyService policyService, IRISDbContext dbContext)
        {
            _monitoringService = monitoringService;
            _policyService = policyService;
            _dbContext = dbContext;
            Rooms = new ObservableCollection<RoomItem>();

            if (_monitoringService != null && _dbContext != null)
            {
                _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
                _refreshTimer.Tick += async (s, e) => await LoadRoomDataAsync();
                _refreshTimer.Start();
                _ = LoadRoomDataAsync();
            }
            else
            {
                LoadMockRoomData();
            }

            ApplyPoliciesCommand = new RelayCommand(async () => await ApplyPoliciesAsync(), () => Rooms.Any(r => r.IsSelected));
            ClearSelectionCommand = new RelayCommand(async () => { ClearRoomSelection(); await Task.CompletedTask; }, () => true);
            BrowseWallpaperCommand = new RelayCommand(async () => { BrowseWallpaper(); await Task.CompletedTask; }, () => true);
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

                // Get the same data as Monitor page uses
                var roomsWithCounts = await _monitoringService.GetActiveLabPCsAsync();

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
                        IsSelected = false,
                        ActivePolicies = activePolicies
                    });
                }

                UpdateSelectionStatus();
            }
            catch (Exception ex)
            {
                // Surface the error so we can diagnose why DB/monitoring queries failed
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
                var selectedRooms = Rooms.Where(r => r.IsSelected).ToList();
                
                foreach (var room in selectedRooms)
                {
                    // Remove existing policies for this room
                    await _policyService.DeletePoliciesByRoomIdAsync(room.Id);

                    // Create new policy if any settings are enabled
                    if (WallpaperResetEnabled || AutoShutdownEnabled)
                    {
                        var policy = new Policy
                        {
                            Name = $"Policy for {room.RoomNumber}",
                            Description = "Auto-generated policy from Policy Enforcement UI",
                            RoomId = room.Id,
                            ResetWallpaperOnStartup = WallpaperResetEnabled,
                            WallpaperPath = WallpaperResetEnabled ? SelectedWallpaperPath : null,
                            AutoShutdownIdleMinutes = AutoShutdownEnabled ? AutoShutdownMinutes : null,
                            IsActive = true
                        };

                        await _policyService.CreatePolicyAsync(policy);
                    }
                }

                LastAppliedText = $"Applied {DateTime.Now:HH:mm:ss}";
                
                // Refresh room data to show updated policies
                await LoadRoomDataAsync();
            }
            catch (Exception ex)
            {
                // Handle error - could show message box or log
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
        }

        public void ToggleRoom(RoomItem? room)
        {
            if (room != null)
            {
                room.IsSelected = !room.IsSelected;
                UpdateSelectionStatus();
            }
        }

        private void UpdateSelectionStatus()
        {
            var selectedCount = Rooms.Count(r => r.IsSelected);
            SelectionStatusText = selectedCount == 0 ? "No rooms selected" : 
                                selectedCount == 1 ? "1 room selected" : 
                                $"{selectedCount} rooms selected";
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

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
