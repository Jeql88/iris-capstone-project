using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using IRIS.UI.Helpers;
using IRIS.UI.Services;
using IRIS.Core.Services.Contracts;

namespace IRIS.UI.ViewModels
{
    public class ViewScreenViewModel : INotifyPropertyChanged
    {
        private readonly INavigationService _navigationService;
        private readonly IMonitoringService _monitoringService;
        private int _pcId;
        private bool _isDetailsExpanded = true;
        private string _pcName = string.Empty;
        private string _pcNumber = string.Empty;
        private string _roomName = string.Empty;
        private string _ip = string.Empty;
        private string _macAddress = string.Empty;
        private string _subnet = "255.255.255.0";
        private string _network = string.Empty;
        private string _os = string.Empty;
        private string _cpu = string.Empty;
        private string _cpuTemp = "N/A";
        private string _gpuTemp = "N/A";
        private string _ram = string.Empty;
        private double _cpuUsage;
        private double _ramUsage;
        private double _diskUsage;
        private string _cpuModel = string.Empty;
        private string _gpuModel = string.Empty;
        private string _motherboard = string.Empty;
        private string _ramModel = string.Empty;
        private string _storagePrimary = string.Empty;
        private string _storageSecondary = string.Empty;
        private string _connectionStatus = "Connected";
        private bool _isConnected = true;
        private bool _isLoading;
        private bool _isDisconnected;

        public ViewScreenViewModel(INavigationService navigationService, IMonitoringService monitoringService)
        {
            _navigationService = navigationService;
            _monitoringService = monitoringService;
            ToggleDetailsCommand = new RelayCommand(async () => await ToggleDetailsAsync(), () => true);
            LockScreenCommand = new RelayCommand(async () => await LockScreenAsync(), () => true);
            ShutDownCommand = new RelayCommand(async () => await ShutDownAsync(), () => true);
            ShutdownPCCommand = new RelayCommand(async () => await ShutDownAsync(), () => true);
            RestartPCCommand = new RelayCommand(async () => await Task.CompletedTask, () => true);
            RemoteDesktopCommand = new RelayCommand(async () => await RemoteDesktopAsync(), () => true);
            FullscreenCommand = new RelayCommand(async () => await Task.CompletedTask, () => true);
            RefreshScreenCommand = new RelayCommand(async () => await Task.CompletedTask, () => true);
            RetryConnectionCommand = new RelayCommand(async () => await Task.CompletedTask, () => true);
            BackCommand = new RelayCommand(async () => await BackAsync(), () => _navigationService.CanGoBack);
        }

        public bool IsDetailsExpanded
        {
            get => _isDetailsExpanded;
            set
            {
                _isDetailsExpanded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DetailsVisibility));
                OnPropertyChanged(nameof(ExpandIcon));
            }
        }

        public Visibility DetailsVisibility => IsDetailsExpanded ? Visibility.Visible : Visibility.Collapsed;
        public string ExpandIcon => IsDetailsExpanded ? "▲" : "▼";

        public string PCName { get => _pcName; set { _pcName = value; OnPropertyChanged(); } }
        public string PCNumber { get => _pcNumber; set { _pcNumber = value; OnPropertyChanged(); } }
        public string RoomName { get => _roomName; set { _roomName = value; OnPropertyChanged(); } }
        public string IP { get => _ip; set { _ip = value; OnPropertyChanged(); } }
        public string MacAddress { get => _macAddress; set { _macAddress = value; OnPropertyChanged(); } }
        public string Subnet { get => _subnet; set { _subnet = value; OnPropertyChanged(); } }
        public string Network { get => _network; set { _network = value; OnPropertyChanged(); } }
        public string OS { get => _os; set { _os = value; OnPropertyChanged(); } }
        public string CPU { get => _cpu; set { _cpu = value; OnPropertyChanged(); } }
        public string CPUTemp { get => _cpuTemp; set { _cpuTemp = value; OnPropertyChanged(); } }
        public string GPUTemp { get => _gpuTemp; set { _gpuTemp = value; OnPropertyChanged(); } }
        public string RAM { get => _ram; set { _ram = value; OnPropertyChanged(); } }
        public double CpuUsage { get => _cpuUsage; set { _cpuUsage = value; OnPropertyChanged(); } }
        public double RamUsage { get => _ramUsage; set { _ramUsage = value; OnPropertyChanged(); } }
        public double DiskUsage { get => _diskUsage; set { _diskUsage = value; OnPropertyChanged(); } }
        public string CPUModel { get => _cpuModel; set { _cpuModel = value; OnPropertyChanged(); } }
        public string GPUModel { get => _gpuModel; set { _gpuModel = value; OnPropertyChanged(); } }
        public string Motherboard { get => _motherboard; set { _motherboard = value; OnPropertyChanged(); } }
        public string RAMModel { get => _ramModel; set { _ramModel = value; OnPropertyChanged(); } }
        public string StoragePrimary { get => _storagePrimary; set { _storagePrimary = value; OnPropertyChanged(); } }
        public string StorageSecondary { get => _storageSecondary; set { _storageSecondary = value; OnPropertyChanged(); } }
        public string ConnectionStatus { get => _connectionStatus; set { _connectionStatus = value; OnPropertyChanged(); } }
        public bool IsConnected { get => _isConnected; set { _isConnected = value; OnPropertyChanged(); } }
        public bool IsLoading { get => _isLoading; set { _isLoading = value; OnPropertyChanged(); } }
        public bool IsDisconnected { get => _isDisconnected; set { _isDisconnected = value; OnPropertyChanged(); } }

        public ICommand ToggleDetailsCommand { get; }
        public ICommand LockScreenCommand { get; }
        public ICommand ShutDownCommand { get; }
        public ICommand ShutdownPCCommand { get; }
        public ICommand RestartPCCommand { get; }
        public ICommand RemoteDesktopCommand { get; }
        public ICommand FullscreenCommand { get; }
        public ICommand RefreshScreenCommand { get; }
        public ICommand RetryConnectionCommand { get; }
        public ICommand BackCommand { get; }

        public async void LoadPCData(PCDisplayModel pc)
        {
            _pcId = pc.Id;
            PCName = pc.PCName;
            PCNumber = pc.PCName;
            RoomName = pc.RoomName;
            IP = pc.IPAddress;
            MacAddress = pc.MacAddress;
            Network = pc.Network;
            OS = pc.OS;
            CPU = pc.CPU;
            RAM = pc.RAM;
            CpuUsage = pc.CpuUsagePercent;
            RamUsage = pc.RamUsagePercent;
            DiskUsage = pc.DiskUsagePercent;

            ConnectionStatus = pc.Status == "Online" ? "Connected" : "Disconnected";
            IsConnected = pc.Status == "Online";
            IsDisconnected = pc.Status == "Offline";
            
            await LoadHardwareConfigAsync();
        }

        private async Task LoadHardwareConfigAsync()
        {
            try
            {
                var config = await _monitoringService.GetPCHardwareConfigAsync(_pcId);
                if (config != null)
                {
                    CPUModel = $"CPU: {config.Processor ?? "Unknown"}";
                    GPUModel = $"GPU: {config.GraphicsCard ?? "Unknown"}";
                    Motherboard = $"Motherboard: {config.Motherboard ?? "Unknown"}";
                    RAMModel = $"RAM: {FormatBytes(config.RamCapacity ?? 0)}";
                    StoragePrimary = $"Storage: {FormatBytes(config.StorageCapacity ?? 0)} ({config.StorageType ?? "Unknown"})";
                    StorageSecondary = "";
                }
            }
            catch { }
        }

        private string FormatBytes(long bytes)
        {
            if (bytes >= 1099511627776) return $"{bytes / 1099511627776.0:F1} TB";
            if (bytes >= 1073741824) return $"{bytes / 1073741824.0:F1} GB";
            if (bytes >= 1048576) return $"{bytes / 1048576.0:F1} MB";
            return $"{bytes} bytes";
        }

        private async Task ToggleDetailsAsync()
        {
            await Task.CompletedTask;
            IsDetailsExpanded = !IsDetailsExpanded;
        }

        private async Task LockScreenAsync()
        {
            await Task.CompletedTask;
            MessageBox.Show("Lock Screen functionality will be implemented.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task ShutDownAsync()
        {
            await Task.CompletedTask;
            MessageBox.Show("Shut Down functionality will be implemented.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task RemoteDesktopAsync()
        {
            await Task.CompletedTask;
            MessageBox.Show("Remote Desktop functionality will be implemented.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task BackAsync()
        {
            await Task.CompletedTask;
            _navigationService.GoBack();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
