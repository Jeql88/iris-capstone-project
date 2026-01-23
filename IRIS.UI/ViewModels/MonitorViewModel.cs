using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using IRIS.UI.Helpers;
using IRIS.UI.Services;
using IRIS.Core.Services;
using IRIS.Core.Services.ServiceModels;

namespace IRIS.UI.ViewModels
{
    public class MonitorViewModel : INotifyPropertyChanged
    {
        private readonly INavigationService _navigationService;
        private readonly IMonitoringService _monitoringService;
        private readonly DispatcherTimer _refreshTimer;
        private string _searchText = string.Empty;
        private string _selectedLab = "Archi Lab 1";
        private int _onlineCount;
        private int _offlineCount;
        private int _warningCount;

        public MonitorViewModel(INavigationService navigationService, IMonitoringService monitoringService)
        {
            _navigationService = navigationService;
            _monitoringService = monitoringService;
            ViewScreenCommand = new RelayCommand(async () => await ViewScreenAsync(), () => SelectedPC != null);
            LockScreenCommand = new RelayCommand(async () => await LockScreenAsync(), () => SelectedPC != null);
            
            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _refreshTimer.Tick += async (s, e) => await LoadPCDataAsync();
            _refreshTimer.Start();
            
            _ = LoadPCDataAsync();
        }

        public ObservableCollection<PCDisplayModel> PCs { get; } = new();

        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); }
        }

        public string SelectedLab
        {
            get => _selectedLab;
            set { _selectedLab = value; OnPropertyChanged(); _ = LoadPCDataAsync(); }
        }

        public int OnlineCount
        {
            get => _onlineCount;
            set { _onlineCount = value; OnPropertyChanged(); }
        }

        public int OfflineCount
        {
            get => _offlineCount;
            set { _offlineCount = value; OnPropertyChanged(); }
        }

        public int WarningCount
        {
            get => _warningCount;
            set { _warningCount = value; OnPropertyChanged(); }
        }

        private PCDisplayModel? _selectedPC;
        public PCDisplayModel? SelectedPC
        {
            get => _selectedPC;
            set
            {
                _selectedPC = value;
                OnPropertyChanged();
                (ViewScreenCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (LockScreenCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public ICommand ViewScreenCommand { get; }
        public ICommand LockScreenCommand { get; }

        private async Task LoadPCDataAsync()
        {
            try
            {
                var pcs = await _monitoringService.GetPCsForMonitorAsync();
                var counts = await _monitoringService.GetPCStatusCountsAsync();
                
                PCs.Clear();
                
                foreach (var pc in pcs)
                {
                    var statusColor = pc.Status == "Online" ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) :
                                     pc.Status == "Offline" ? new SolidColorBrush(Color.FromRgb(239, 68, 68)) :
                                     new SolidColorBrush(Color.FromRgb(245, 158, 11));

                    PCs.Add(new PCDisplayModel
                    {
                        Id = pc.Id,
                        Name = pc.Name,
                        IP = $"IP: {pc.IpAddress}",
                        OS = $"OS: {pc.OperatingSystem}",
                        CPU = $"CPU: {pc.CpuUsage:F0}%",
                        Network = $"Network: {pc.NetworkUsage:F1} Mbps",
                        RAM = $"RAM: {pc.RamUsage:F0}%",
                        User = string.IsNullOrEmpty(pc.User) ? "" : $"User: {pc.User}",
                        StatusColor = statusColor
                    });
                }
                
                OnlineCount = counts.OnlineCount;
                OfflineCount = counts.OfflineCount;
                WarningCount = counts.WarningCount;
            }
            catch
            {
                // Fallback to empty if error
            }
        }

        private void LoadPCData_OLD()
        {
            PCs.Clear();
            var pcData = new[]
            {
                new PCDisplayModel { Name = "LAB1-PC01", IP = "IP: 192.168.1.101", OS = "OS: Windows 11", CPU = "CPU: 0%", Network = "Network: 0.0 Mbps", RAM = "RAM: 0%", User = "", StatusColor = new SolidColorBrush(Color.FromRgb(239, 68, 68)) },
                new PCDisplayModel { Name = "LAB1-PC02", IP = "IP: 192.168.1.102", OS = "OS: Windows 11", CPU = "CPU: 0%", Network = "Network: 0.0 Mbps", RAM = "RAM: 0%", User = "", StatusColor = new SolidColorBrush(Color.FromRgb(239, 68, 68)) },
                new PCDisplayModel { Name = "LAB1-PC03", IP = "IP: 192.168.1.103", OS = "OS: Windows 11", CPU = "CPU: 0%", Network = "Network: 0.0 Mbps", RAM = "RAM: 0%", User = "", StatusColor = new SolidColorBrush(Color.FromRgb(239, 68, 68)) },
                new PCDisplayModel { Name = "LAB1-PC04", IP = "IP: 192.168.1.104", OS = "OS: Windows 11", CPU = "CPU: 0%", Network = "Network: 0.0 Mbps", RAM = "RAM: 0%", User = "", StatusColor = new SolidColorBrush(Color.FromRgb(239, 68, 68)) },
                new PCDisplayModel { Name = "LAB1-PC05", IP = "IP: 192.168.1.105", OS = "OS: Windows 11", CPU = "CPU: 65%", Network = "Network: 6.0 Mbps", RAM = "RAM: 43%", User = "User: student5", StatusColor = new SolidColorBrush(Color.FromRgb(245, 158, 11)) },
                new PCDisplayModel { Name = "LAB1-PC06", IP = "IP: 192.168.1.106", OS = "OS: Windows 11", CPU = "CPU: 87%", Network = "Network: 9.1 Mbps", RAM = "RAM: 73%", User = "User: student6", StatusColor = new SolidColorBrush(Color.FromRgb(245, 158, 11)) },
                new PCDisplayModel { Name = "LAB1-PC07", IP = "IP: 192.168.1.107", OS = "OS: Windows 11", CPU = "CPU: 27%", Network = "Network: 3.0 Mbps", RAM = "RAM: 0%", User = "User: student7", StatusColor = new SolidColorBrush(Color.FromRgb(245, 158, 11)) },
                new PCDisplayModel { Name = "LAB1-PC08", IP = "IP: 192.168.1.108", OS = "OS: Windows 11", CPU = "CPU: 0%", Network = "Network: 0.0 Mbps", RAM = "RAM: 57%", User = "", StatusColor = new SolidColorBrush(Color.FromRgb(16, 185, 129)) },
                new PCDisplayModel { Name = "LAB1-PC09", IP = "IP: 192.168.1.109", OS = "OS: Windows 11", CPU = "CPU: 69%", Network = "Network: 3.8 Mbps", RAM = "RAM: 34%", User = "User: student9", StatusColor = new SolidColorBrush(Color.FromRgb(16, 185, 129)) },
                new PCDisplayModel { Name = "LAB1-PC10", IP = "IP: 192.168.1.110", OS = "OS: Windows 11", CPU = "CPU: 9%", Network = "Network: 3.6 Mbps", RAM = "RAM: 26%", User = "User: student10", StatusColor = new SolidColorBrush(Color.FromRgb(16, 185, 129)) },
                new PCDisplayModel { Name = "LAB1-PC11", IP = "IP: 192.168.1.111", OS = "OS: Windows 11", CPU = "CPU: 35%", Network = "Network: 5.5 Mbps", RAM = "RAM: 1%", User = "User: student11", StatusColor = new SolidColorBrush(Color.FromRgb(16, 185, 129)) },
                new PCDisplayModel { Name = "LAB1-PC12", IP = "IP: 192.168.1.112", OS = "OS: Windows 11", CPU = "CPU: 11%", Network = "Network: 5.4 Mbps", RAM = "RAM: 93%", User = "User: student12", StatusColor = new SolidColorBrush(Color.FromRgb(16, 185, 129)) },
                new PCDisplayModel { Name = "LAB1-PC13", IP = "IP: 192.168.1.113", OS = "OS: Windows 11", CPU = "CPU: 40%", Network = "Network: 6.9 Mbps", RAM = "RAM: 64%", User = "User: student13", StatusColor = new SolidColorBrush(Color.FromRgb(16, 185, 129)) },
                new PCDisplayModel { Name = "LAB1-PC14", IP = "IP: 192.168.1.114", OS = "OS: Windows 11", CPU = "CPU: 79%", Network = "Network: 8.9 Mbps", RAM = "RAM: 68%", User = "User: student14", StatusColor = new SolidColorBrush(Color.FromRgb(16, 185, 129)) },
                new PCDisplayModel { Name = "LAB1-PC15", IP = "IP: 192.168.1.115", OS = "OS: Windows 11", CPU = "CPU: 95%", Network = "Network: 8.1 Mbps", RAM = "RAM: 67%", User = "User: student15", StatusColor = new SolidColorBrush(Color.FromRgb(16, 185, 129)) },
                new PCDisplayModel { Name = "LAB1-PC16", IP = "IP: 192.168.1.116", OS = "OS: Windows 11", CPU = "CPU: 68%", Network = "Network: 7.0 Mbps", RAM = "RAM: 43%", User = "User: student16", StatusColor = new SolidColorBrush(Color.FromRgb(16, 185, 129)) },
                new PCDisplayModel { Name = "LAB1-PC17", IP = "IP: 192.168.1.117", OS = "OS: Windows 11", CPU = "CPU: 50%", Network = "Network: 0.2 Mbps", RAM = "RAM: 31%", User = "User: student17", StatusColor = new SolidColorBrush(Color.FromRgb(16, 185, 129)) },
                new PCDisplayModel { Name = "LAB1-PC18", IP = "IP: 192.168.1.118", OS = "OS: Windows 11", CPU = "CPU: 99%", Network = "Network: 8.0 Mbps", RAM = "RAM: 1%", User = "User: student18", StatusColor = new SolidColorBrush(Color.FromRgb(16, 185, 129)) },
                new PCDisplayModel { Name = "LAB1-PC19", IP = "IP: 192.168.1.119", OS = "OS: Windows 11", CPU = "CPU: 11%", Network = "Network: 7.2 Mbps", RAM = "RAM: 36%", User = "User: student19", StatusColor = new SolidColorBrush(Color.FromRgb(16, 185, 129)) },
                new PCDisplayModel { Name = "LAB1-PC20", IP = "IP: 192.168.1.120", OS = "OS: Windows 11", CPU = "CPU: 71%", Network = "Network: 1.4 Mbps", RAM = "RAM: 7%", User = "User: student20", StatusColor = new SolidColorBrush(Color.FromRgb(16, 185, 129)) }
            };

            foreach (var pc in pcData)
                PCs.Add(pc);
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
        private string _name = string.Empty;
        private string _ip = string.Empty;
        private string _os = string.Empty;
        private string _cpu = string.Empty;
        private string _network = string.Empty;
        private string _ram = string.Empty;
        private string _user = string.Empty;
        private SolidColorBrush _statusColor = new(Colors.Gray);

        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string IP
        {
            get => _ip;
            set { _ip = value; OnPropertyChanged(); }
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

        public SolidColorBrush StatusColor
        {
            get => _statusColor;
            set { _statusColor = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
