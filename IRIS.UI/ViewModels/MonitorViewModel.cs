using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using IRIS.UI.Helpers;
using IRIS.UI.Services;
using IRIS.Core.Services.Contracts;
using IRIS.Core.Services.ServiceModels;

namespace IRIS.UI.ViewModels
{
    public class MonitorViewModel : INotifyPropertyChanged
    {
        private readonly INavigationService _navigationService;
        private readonly IMonitoringService _monitoringService;
        private readonly DispatcherTimer _refreshTimer;
        private string _searchText = string.Empty;
        private string? _selectedRoom;
        private int _totalPCCount;
        private int _onlinePCCount;
        private int _offlinePCCount;
        private int _idlePCCount;
        private bool _hasNoPCs = true;

        public MonitorViewModel(INavigationService navigationService, IMonitoringService monitoringService)
        {
            _navigationService = navigationService;
            _monitoringService = monitoringService;
            ViewScreenCommand = new RelayCommand(async () => await ViewScreenAsync(), () => SelectedPC != null);
            LockScreenCommand = new RelayCommand(async () => await LockScreenAsync(), () => SelectedPC != null);
            RefreshCommand = new RelayCommand(async () => await LoadPCDataAsync(), () => true);
            RestartPCCommand = new RelayCommand(async () => await Task.CompletedTask, () => SelectedPC != null);
            ShutdownPCCommand = new RelayCommand(async () => await Task.CompletedTask, () => SelectedPC != null);
            
            // Initialize rooms
            Rooms.Add("All Rooms");
            Rooms.Add("Archi Lab 1");
            Rooms.Add("Archi Lab 2");
            Rooms.Add("Archi Lab 3");
            Rooms.Add("Archi Lab 4");
            _selectedRoom = Rooms.FirstOrDefault();

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _refreshTimer.Tick += async (s, e) => await LoadPCDataAsync();
            _refreshTimer.Start();
            
            _ = LoadPCDataAsync();
        }

        public ObservableCollection<PCDisplayModel> PCs { get; } = new();
        public ObservableCollection<PCDisplayModel> FilteredPCs { get; } = new();
        public ObservableCollection<string> Rooms { get; } = new();

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

        public string? SelectedRoom
        {
            get => _selectedRoom;
            set { _selectedRoom = value; OnPropertyChanged(); _ = LoadPCDataAsync(); }
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
        public ICommand LockScreenCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand RestartPCCommand { get; }
        public ICommand ShutdownPCCommand { get; }

        private async Task LoadPCDataAsync()
        {
            try
            {
                var pcs = await _monitoringService.GetPCsForMonitorAsync();
                var counts = await _monitoringService.GetPCStatusCountsAsync();
                
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
                        Network = $"{pc.NetworkUsage:F1} Mbps",
                        RAM = $"{pc.RamUsage:F0}%",
                        CpuUsagePercent = pc.CpuUsage,
                        RamUsagePercent = pc.RamUsage,
                        DiskUsagePercent = pc.DiskUsage,
                        User = pc.User
                    });
                }
                
                OnlinePCCount = counts.OnlineCount;
                OfflinePCCount = counts.OfflineCount;
                IdlePCCount = counts.WarningCount;
                TotalPCCount = OnlinePCCount + OfflinePCCount + IdlePCCount;
                
                ApplyFilter();
            }
            catch
            {
                // Fallback to empty if error
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

        private async Task ViewScreenAsync()
        {
            if (SelectedPC != null)
            {
                await Task.CompletedTask;
                _navigationService.NavigateTo("ViewScreen", SelectedPC);
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
        private string _network = string.Empty;
        private string _ram = string.Empty;
        private string _user = string.Empty;
        private string _macAddress = string.Empty;
        private string _roomName = string.Empty;
        private double _cpuUsagePercent;
        private double _ramUsagePercent;
        private double _diskUsagePercent;
        private bool _isSelected;

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

        public string Network
        {
            get => _network;
            set { _network = value; OnPropertyChanged(); }
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

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
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
}
