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
        private bool _isDetailsExpanded;
        private string _pcName = "LB448";
        private string _pcNumber = "PC12";
        private string _ip = "192.168.1.102";
        private string _subnet = "255.255.255.0";
        private string _network = "32 Mbps";
        private string _os = "Windows 10 Pro";
        private string _cpu = "74%";
        private string _cpuTemp = "63°C";
        private string _gpuTemp = "44°C";
        private string _ram = "15%";
        private string _cpuModel = "CPU: AMD Ryzen 9 7950X";
        private string _gpuModel = "GPU: NVIDIA RTX 4070 Super 12GB";
        private string _motherboard = "Motherboard: ASUS ProArt X670E-Creator WiFi";
        private string _ramModel = "RAM: Corsair Vengeance 64GB DDR5 5600MHz";
        private string _storagePrimary = "Storage (Primary): Samsung 990 PRO 2TB NVMe SSD";
        private string _storageSecondary = "Storage (Secondary): Seagate Barracuda 4TB HDD (7200 RPM)";

        public ViewScreenViewModel(INavigationService navigationService, IMonitoringService monitoringService)
        {
            _navigationService = navigationService;
            _monitoringService = monitoringService;
            ToggleDetailsCommand = new RelayCommand(async () => await ToggleDetailsAsync(), () => true);
            LockScreenCommand = new RelayCommand(async () => await LockScreenAsync(), () => true);
            ShutDownCommand = new RelayCommand(async () => await ShutDownAsync(), () => true);
            RemoteDesktopCommand = new RelayCommand(async () => await RemoteDesktopAsync(), () => true);
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
        public string IP { get => _ip; set { _ip = value; OnPropertyChanged(); } }
        public string Subnet { get => _subnet; set { _subnet = value; OnPropertyChanged(); } }
        public string Network { get => _network; set { _network = value; OnPropertyChanged(); } }
        public string OS { get => _os; set { _os = value; OnPropertyChanged(); } }
        public string CPU { get => _cpu; set { _cpu = value; OnPropertyChanged(); } }
        public string CPUTemp { get => _cpuTemp; set { _cpuTemp = value; OnPropertyChanged(); } }
        public string GPUTemp { get => _gpuTemp; set { _gpuTemp = value; OnPropertyChanged(); } }
        public string RAM { get => _ram; set { _ram = value; OnPropertyChanged(); } }
        public string CPUModel { get => _cpuModel; set { _cpuModel = value; OnPropertyChanged(); } }
        public string GPUModel { get => _gpuModel; set { _gpuModel = value; OnPropertyChanged(); } }
        public string Motherboard { get => _motherboard; set { _motherboard = value; OnPropertyChanged(); } }
        public string RAMModel { get => _ramModel; set { _ramModel = value; OnPropertyChanged(); } }
        public string StoragePrimary { get => _storagePrimary; set { _storagePrimary = value; OnPropertyChanged(); } }
        public string StorageSecondary { get => _storageSecondary; set { _storageSecondary = value; OnPropertyChanged(); } }

        public ICommand ToggleDetailsCommand { get; }
        public ICommand LockScreenCommand { get; }
        public ICommand ShutDownCommand { get; }
        public ICommand RemoteDesktopCommand { get; }
        public ICommand BackCommand { get; }

        public async void LoadPCData(PCDisplayModel pc)
        {
            _pcId = pc.Id;
            PCName = "LB448";
            PCNumber = pc.Name;
            IP = pc.IP.Replace("IP: ", "");
            Network = pc.Network.Replace("Network: ", "");
            OS = pc.OS.Replace("OS: ", "");
            CPU = pc.CPU.Replace("CPU: ", "");
            RAM = pc.RAM.Replace("RAM: ", "");
            
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
