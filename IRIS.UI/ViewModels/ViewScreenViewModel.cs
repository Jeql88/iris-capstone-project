using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using IRIS.UI.Helpers;
using IRIS.UI.Services;
using IRIS.Core.Services.Contracts;
using Microsoft.Extensions.Configuration;

namespace IRIS.UI.ViewModels
{
    public class ViewScreenViewModel : INotifyPropertyChanged, INavigationAware
    {
        private readonly INavigationService _navigationService;
        private readonly IMonitoringService _monitoringService;
        private readonly IPowerCommandQueueService _powerCommandQueueService;
        private readonly int _screenStreamPort;
        private readonly string? _screenStreamToken;
        private readonly DispatcherTimer _screenRefreshTimer;
        private static readonly HttpClient SnapshotHttpClient = new() { Timeout = TimeSpan.FromMilliseconds(1200) };
        private int _pcId;
        private bool _isActive;
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
        private DateTime _lastFrameUpdatedUtc = DateTime.MinValue;
        private ImageSource? _screenImage;

        public ViewScreenViewModel(
            INavigationService navigationService,
            IMonitoringService monitoringService,
            IPowerCommandQueueService powerCommandQueueService,
            IConfiguration configuration)
        {
            _navigationService = navigationService;
            _monitoringService = monitoringService;
            _powerCommandQueueService = powerCommandQueueService;
            _screenStreamPort = int.TryParse(configuration["AgentSettings:ScreenStreamPort"], out var port) ? port : 5057;
            _screenStreamToken = configuration["AgentSettings:ScreenStreamToken"];
            ToggleDetailsCommand = new RelayCommand(async () => await ToggleDetailsAsync(), () => true);
            LockScreenCommand = new RelayCommand(async () => await LockScreenAsync(), () => true);
            ShutDownCommand = new RelayCommand(async () => await ShutDownAsync(), () => true);
            ShutdownPCCommand = new RelayCommand(async () => await ShutDownAsync(), () => true);
            RestartPCCommand = new RelayCommand(async () => await RestartPCAsync(), () => true);
            RemoteDesktopCommand = new RelayCommand(async () => await RemoteDesktopAsync(), () => true);
            RefreshScreenCommand = new RelayCommand(async () => await RefreshScreenAsync(), () => true);
            RetryConnectionCommand = new RelayCommand(async () => await RefreshScreenAsync(), () => true);
            BackCommand = new RelayCommand(async () => await BackAsync(), () => _navigationService.CanGoBack);

            _screenRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _screenRefreshTimer.Tick += async (_, _) => await RefreshScreenAsync();
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
        public string LastFrameUpdatedText => _lastFrameUpdatedUtc == DateTime.MinValue
            ? "No frame yet"
            : $"Last frame {TimeZoneInfo.ConvertTimeFromUtc(_lastFrameUpdatedUtc, TimeZoneInfo.Local):HH:mm:ss}";
        public ImageSource? ScreenImage { get => _screenImage; set { _screenImage = value; OnPropertyChanged(); } }

        public ICommand ToggleDetailsCommand { get; }
        public ICommand LockScreenCommand { get; }
        public ICommand ShutDownCommand { get; }
        public ICommand ShutdownPCCommand { get; }
        public ICommand RestartPCCommand { get; }
        public ICommand RemoteDesktopCommand { get; }
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
            Network = $"{pc.NetworkDownload} / {pc.NetworkUpload}";
            OS = pc.OS;
            CPU = pc.CPU;
            CPUTemp = pc.CPUTemperature;
            GPUTemp = pc.GPUTemperature;
            RAM = pc.RAM;
            CpuUsage = pc.CpuUsagePercent;
            RamUsage = pc.RamUsagePercent;
            DiskUsage = pc.DiskUsagePercent;

            ConnectionStatus = pc.Status == "Online" ? "Connected" : "Disconnected";
            IsConnected = pc.Status == "Online";
            IsDisconnected = pc.Status == "Offline";

            _isActive = true;
            await LoadHardwareConfigAsync();
            await RefreshScreenAsync();
            _screenRefreshTimer.Start();
        }

        private async Task RefreshScreenAsync()
        {
            if (_pcId <= 0 || !_isActive)
            {
                return;
            }

            try
            {
                IsLoading = true;
                var imageBase64 = await GetSnapshotBase64Async(IP);
                if (!string.IsNullOrWhiteSpace(imageBase64))
                {
                    ScreenImage = CreateImage(imageBase64);
                    IsConnected = true;
                    IsDisconnected = false;
                    ConnectionStatus = "Connected";
                    _lastFrameUpdatedUtc = DateTime.UtcNow;
                    OnPropertyChanged(nameof(LastFrameUpdatedText));
                }
                else
                {
                    IsConnected = false;
                    IsDisconnected = true;
                    ConnectionStatus = "Disconnected";
                }
            }
            catch
            {
                IsConnected = false;
                IsDisconnected = true;
                ConnectionStatus = "Disconnected";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task<string?> GetSnapshotBase64Async(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress) || ipAddress == "N/A")
            {
                return null;
            }

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

        private async Task LoadHardwareConfigAsync()
        {
            try
            {
                var config = await _monitoringService.GetPCHardwareConfigAsync(_pcId);
                if (config != null)
                {
                    CPUModel = config.Processor ?? "Unknown";
                    GPUModel = config.GraphicsCard ?? "Unknown";
                    Motherboard = config.Motherboard ?? "Unknown";
                    RAMModel = FormatBytes(config.RamCapacity ?? 0);
                    StoragePrimary = $"{FormatBytes(config.StorageCapacity ?? 0)} ({config.StorageType ?? "Unknown"})";
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
            if (string.IsNullOrWhiteSpace(MacAddress))
            {
                MessageBox.Show("Cannot send shutdown command: missing PC MAC address.", "Command Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirmation = MessageBox.Show(
                $"Are you sure you want to shutdown {PCName}?",
                "Confirm Shutdown",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.Yes)
            {
                return;
            }

            var queued = await _powerCommandQueueService.QueueCommandAsync(MacAddress, "Shutdown");
            if (queued)
            {
                MessageBox.Show($"Shutdown command queued for {PCName}.", "Command Queued", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            MessageBox.Show("Failed to queue shutdown command.", "Command Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private async Task RestartPCAsync()
        {
            if (string.IsNullOrWhiteSpace(MacAddress))
            {
                MessageBox.Show("Cannot send restart command: missing PC MAC address.", "Command Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirmation = MessageBox.Show(
                $"Are you sure you want to restart {PCName}?",
                "Confirm Restart",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.Yes)
            {
                return;
            }

            var queued = await _powerCommandQueueService.QueueCommandAsync(MacAddress, "Restart");
            if (queued)
            {
                MessageBox.Show($"Restart command queued for {PCName}.", "Command Queued", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            MessageBox.Show("Failed to queue restart command.", "Command Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private async Task RemoteDesktopAsync()
        {
            await Task.CompletedTask;
            MessageBox.Show("Remote Desktop functionality will be implemented.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task BackAsync()
        {
            await Task.CompletedTask;
            OnDeactivated();
            _navigationService.GoBack();
        }

        public async Task OnActivatedAsync()
        {
            if (_pcId <= 0)
            {
                return;
            }

            _isActive = true;
            await RefreshScreenAsync();
            _screenRefreshTimer.Start();
        }

        public void OnDeactivated()
        {
            _isActive = false;
            _screenRefreshTimer.Stop();
        }

        public void OnNavigatedFrom()
        {
            OnDeactivated();
        }

        private static BitmapImage? CreateImage(string base64)
        {
            try
            {
                var bytes = Convert.FromBase64String(base64);
                using var ms = new MemoryStream(bytes);
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = ms;
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
