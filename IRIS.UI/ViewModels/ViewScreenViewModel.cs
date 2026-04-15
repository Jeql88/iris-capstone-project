using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Text;
using IRIS.UI.Helpers;
using IRIS.UI.Services;
using IRIS.UI.Services.Contracts;
using IRIS.UI.Views.Dialogs;
using IRIS.UI.Views.Faculty;
using IRIS.Core.Models;
using IRIS.Core.Services.Contracts;
using Microsoft.Extensions.Configuration;

namespace IRIS.UI.ViewModels
{
    public class ViewScreenViewModel : INotifyPropertyChanged, INavigationAware
    {
        private readonly INavigationService _navigationService;
        private readonly IPCDataCacheService _cache;
        private readonly IMonitoringService _monitoringService;
        private readonly IPowerCommandQueueService _powerCommandQueueService;
        private readonly IAuthenticationService _authenticationService;
        private readonly ILocalMachineIdentityService _localMachineIdentity;
        private readonly int _screenStreamPort;
        private readonly int _remoteDesktopPort;
        private readonly string? _screenStreamToken;
        private readonly DispatcherTimer _screenRefreshTimer;
        private readonly DispatcherTimer _systemInfoRefreshTimer;
        // Snapshot fetching uses RawHttpClient (raw TCP) instead of HttpClient
        // to bypass Sophos Web Protection interception of .NET's HTTP library layer.
        private int _pcId;
        private bool _isActive;
        private bool _isDetailsExpanded = false;
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
        private bool _isFreezeActive;
        private DateTime _lastFrameUpdatedUtc = DateTime.MinValue;
        private ImageSource? _screenImage;
        private ExpandedScreenWindow? _expandedWindow;

        public ViewScreenViewModel(
            INavigationService navigationService,
            IPCDataCacheService cache,
            IMonitoringService monitoringService,
            IPowerCommandQueueService powerCommandQueueService,
            IConfiguration configuration,
            IAuthenticationService authenticationService,
            ILocalMachineIdentityService localMachineIdentity)
        {
            _navigationService = navigationService;
            _cache = cache;
            _monitoringService = monitoringService;
            _powerCommandQueueService = powerCommandQueueService;
            _authenticationService = authenticationService;
            _localMachineIdentity = localMachineIdentity;
            _screenStreamPort = int.TryParse(configuration["AgentSettings:ScreenStreamPort"], out var port) ? port : 5057;
            _remoteDesktopPort = int.TryParse(configuration["AgentSettings:RemoteDesktopPort"], out var rdpPort) ? rdpPort : 3389;
            _screenStreamToken = configuration["AgentSettings:ScreenStreamToken"];
            ToggleDetailsCommand = new RelayCommand(async () => await ToggleDetailsAsync(), () => true);
            LockScreenCommand = new RelayCommand(async () => await LockScreenAsync(), () => true);
            ShutDownCommand = new RelayCommand(async () => await ShutDownAsync(), () => true);
            ShutdownPCCommand = new RelayCommand(async () => await ShutDownAsync(), () => true);
            RestartPCCommand = new RelayCommand(async () => await RestartPCAsync(), () => true);
            ToggleFreezeCommand = new RelayCommand(async () => await ToggleFreezeAsync(), () => true);
            SendMessageCommand = new RelayCommand(async () => await SendMessageAsync(), () => true);
            RemoteDesktopCommand = new RelayCommand(async () => await RemoteDesktopAsync(), () => true);
            RefreshScreenCommand = new RelayCommand(async () => { await RefreshScreenAsync(); RefreshSystemInfoFromCache(); }, () => true);
            RetryConnectionCommand = new RelayCommand(async () => await RefreshScreenAsync(), () => true);
            ExpandScreenCommand = new RelayCommand(() => ExpandScreen(), () => true);
            BackCommand = new RelayCommand(async () => await BackAsync(), () => _navigationService.CanGoBack);

            _screenRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _screenRefreshTimer.Tick += async (_, _) => await RefreshScreenAsync();

            _systemInfoRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _systemInfoRefreshTimer.Tick += (_, _) => RefreshSystemInfoFromCache();
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

        public bool IsFaculty => _authenticationService.GetCurrentUser()?.Role == UserRole.Faculty;
        public bool ShowRemoteDesktop => !IsFaculty;
        public bool ShowPowerControls => !IsFaculty;

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
        public bool IsFreezeActive { get => _isFreezeActive; set { _isFreezeActive = value; OnPropertyChanged(); OnPropertyChanged(nameof(FreezeButtonText)); } }
        public string LastFrameUpdatedText => _lastFrameUpdatedUtc == DateTime.MinValue
            ? "No frame yet"
            : $"Last frame {DateTimeDisplayHelper.ToManilaFromUtc(_lastFrameUpdatedUtc):HH:mm:ss}";
        public string FreezeButtonText => IsFreezeActive ? "Unfreeze" : "Freeze";
        public ImageSource? ScreenImage { get => _screenImage; set { _screenImage = value; OnPropertyChanged(); } }

        public ICommand ToggleDetailsCommand { get; }
        public ICommand LockScreenCommand { get; }
        public ICommand ShutDownCommand { get; }
        public ICommand ShutdownPCCommand { get; }
        public ICommand RestartPCCommand { get; }
        public ICommand ToggleFreezeCommand { get; }
        public ICommand SendMessageCommand { get; }
        public ICommand RemoteDesktopCommand { get; }
        public ICommand RefreshScreenCommand { get; }
        public ICommand RetryConnectionCommand { get; }
        public ICommand ExpandScreenCommand { get; }
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
            var cachedFreezeState = _cache.GetFreezeState(pc.Id);
            IsFreezeActive = cachedFreezeState ?? pc.IsFreezeActive;

            _isActive = true;
            await LoadHardwareConfigAsync();
            await RefreshScreenAsync();
            _screenRefreshTimer.Start();
            _systemInfoRefreshTimer.Start();
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
                var headers = string.IsNullOrWhiteSpace(_screenStreamToken)
                    ? null
                    : new[] { ("X-IRIS-Snapshot-Token", _screenStreamToken) };

                var bytes = await RawHttpClient.GetBytesAsync(
                    ipAddress, _screenStreamPort, "/snapshot", headers, TimeSpan.FromMilliseconds(1200));

                return bytes is { Length: > 0 } ? Convert.ToBase64String(bytes) : null;
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

        private void RefreshSystemInfoFromCache()
        {
            if (_pcId <= 0) return;

            var pc = _cache.CachedPCs.FirstOrDefault(p => p.Id == _pcId);
            if (pc == null) return;

            CpuUsage = pc.CpuUsage;
            RamUsage = pc.RamUsage;
            DiskUsage = pc.DiskUsage;
            CPU = $"{pc.CpuUsage:F0}%";
            RAM = $"{pc.RamUsage:F0}%";
            CPUTemp = pc.CpuTemperature.HasValue ? $"{pc.CpuTemperature.Value:F1} °C" : "N/A";
            GPUTemp = pc.GpuTemperature.HasValue ? $"{pc.GpuTemperature.Value:F1} °C" : "N/A";
            Network = $"{pc.NetworkDownloadMbps:F1} Mbps / {pc.NetworkUploadMbps:F1} Mbps";
            IP = pc.IpAddress;

            var isOnline = pc.Status == "Online";
            ConnectionStatus = isOnline ? "Connected" : "Disconnected";
            IsConnected = isOnline;
            IsDisconnected = !isOnline;
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
            if (!IsConnected || IsDisconnected)
            {
                ShowOfflineActionDialog("shutdown");
                return;
            }

            if (string.IsNullOrWhiteSpace(MacAddress))
            {
                MessageBox.Show("Cannot send shutdown command: missing PC MAC address.", "Command Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // if (_localMachineIdentity.IsLocalMachine(MacAddress))
            // {
            //     var selfWarning = new ConfirmationDialog(
            //         "Shutdown Blocked",
            //         $"The target PC \"{PCName}\" is this machine (the dashboard host). " +
            //         "Shutting down will terminate the IRIS dashboard.\n\n" +
            //         "This action has been blocked for safety.",
            //         "Warning24",
            //         "OK",
            //         "Cancel",
            //         false);
            //     selfWarning.Owner = Application.Current.MainWindow;
            //     selfWarning.ShowDialog();
            //     return;
            // }

            var confirmationDialog = new ConfirmationDialog(
                "Confirm Shutdown",
                $"Are you sure you want to shutdown {PCName}?",
                "Power24");
            confirmationDialog.Owner = Application.Current.MainWindow;

            if (confirmationDialog.ShowDialog() != true)
            {
                return;
            }

            var queued = await _powerCommandQueueService.QueueCommandAsync(MacAddress, "Shutdown");
            if (queued)
            {
                return;
            }

            MessageBox.Show("Failed to queue shutdown command.", "Command Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private async Task RestartPCAsync()
        {
            if (!IsConnected || IsDisconnected)
            {
                ShowOfflineActionDialog("restart");
                return;
            }

            if (string.IsNullOrWhiteSpace(MacAddress))
            {
                MessageBox.Show("Cannot send restart command: missing PC MAC address.", "Command Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // if (_localMachineIdentity.IsLocalMachine(MacAddress))
            // {
            //     var selfWarning = new ConfirmationDialog(
            //         "Restart Blocked",
            //         $"The target PC \"{PCName}\" is this machine (the dashboard host). " +
            //         "Restarting will terminate the IRIS dashboard.\n\n" +
            //         "This action has been blocked for safety.",
            //         "Warning24",
            //         "OK",
            //         "Cancel",
            //         false);
            //     selfWarning.Owner = Application.Current.MainWindow;
            //     selfWarning.ShowDialog();
            //     return;
            // }

            var confirmationDialog = new ConfirmationDialog(
                "Confirm Restart",
                $"Are you sure you want to restart {PCName}?",
                "ArrowClockwise24");
            confirmationDialog.Owner = Application.Current.MainWindow;

            if (confirmationDialog.ShowDialog() != true)
            {
                return;
            }

            var queued = await _powerCommandQueueService.QueueCommandAsync(MacAddress, "Restart");
            if (queued)
            {
                return;
            }

            MessageBox.Show("Failed to queue restart command.", "Command Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private async Task ToggleFreezeAsync()
        {
            if (!IsConnected || IsDisconnected)
            {
                ShowOfflineActionDialog("change freeze state");
                return;
            }

            if (string.IsNullOrWhiteSpace(MacAddress))
            {
                ShowActionErrorDialog("Command Error", "Cannot send freeze command: missing PC MAC address.");
                return;
            }

            string commandType;
            if (IsFreezeActive)
            {
                var confirmationDialog = new ConfirmationDialog(
                    "Confirm Unfreeze",
                    $"Unfreeze {PCName}?",
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
                    $"Enter the message to show on {PCName} while frozen:",
                    FreezeMessageDialog.DefaultFreezeMessage);
                freezeDialog.Owner = Application.Current.MainWindow;

                if (freezeDialog.ShowDialog() != true)
                {
                    return;
                }

                var encodedMessage = Convert.ToBase64String(Encoding.UTF8.GetBytes(freezeDialog.FreezeMessage));
                commandType = $"FreezeOn::{encodedMessage}";
            }

            var queued = await _powerCommandQueueService.QueueCommandAsync(MacAddress, commandType);
            if (!queued)
            {
                ShowActionErrorDialog("Command Error", "Failed to queue freeze command.");
                return;
            }

            IsFreezeActive = !IsFreezeActive;
            _cache.SetFreezeState(_pcId, IsFreezeActive);
        }

        private async Task SendMessageAsync()
        {
            if (!IsConnected || IsDisconnected)
            {
                ShowOfflineActionDialog("send a message");
                return;
            }

            if (string.IsNullOrWhiteSpace(MacAddress))
            {
                ShowActionErrorDialog("Command Error", "Cannot send message: missing PC MAC address.");
                return;
            }

            var messageDialog = new FreezeMessageDialog(
                "Send Message",
                $"Enter the message to show on {PCName}:",
                "Please check the latest instruction from your instructor.");
            messageDialog.Owner = Application.Current.MainWindow;

            if (messageDialog.ShowDialog() != true)
            {
                return;
            }

            var currentUser = _authenticationService.GetCurrentUser();
            var senderName = !string.IsNullOrWhiteSpace(currentUser?.FullName)
                ? currentUser!.FullName!
                : currentUser?.Username ?? "IRIS User";

            var outboundMessage = $"Message from {senderName}\n\n{messageDialog.FreezeMessage}";
            var encodedMessage = Convert.ToBase64String(Encoding.UTF8.GetBytes(outboundMessage));
            var queued = await _powerCommandQueueService.QueueCommandAsync(MacAddress, $"Message::{encodedMessage}");

            if (!queued)
            {
                ShowActionErrorDialog("Command Error", "Failed to queue message command.");
            }
        }

        private async Task RemoteDesktopAsync()
        {
            await Task.CompletedTask;

            if (!IsConnected || IsDisconnected)
            {
                ShowOfflineActionDialog("open Remote Desktop");
                return;
            }

            if (string.IsNullOrWhiteSpace(IP) || IP.Equals("N/A", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Cannot open Remote Desktop: missing target IP address.", "Remote Desktop", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirmationDialog = new ConfirmationDialog(
                "Open Remote Desktop",
                $"Open Remote Desktop connection to {PCName}?",
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
                    Arguments = $"/v:{IP}:{_remoteDesktopPort}",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open Remote Desktop. {ex.Message}", "Remote Desktop Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task BackAsync()
        {
            await Task.CompletedTask;
            OnDeactivated();
            _navigationService.GoBack();
        }

        private void ShowOfflineActionDialog(string actionName)
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

        private void ShowActionSuccessDialog(string title, string message)
        {
            var successDialog = new ConfirmationDialog(
                title,
                message,
                "Checkmark24",
                "OK",
                "Cancel",
                false);
            successDialog.Owner = Application.Current.MainWindow;
            successDialog.ShowDialog();
        }

        private static void ShowActionErrorDialog(string title, string message)
        {
            var errorDialog = new ConfirmationDialog(
                title,
                message,
                "Warning24",
                "OK",
                "Cancel",
                false);
            errorDialog.Owner = Application.Current.MainWindow;
            errorDialog.ShowDialog();
        }

        private void ExpandScreen()
        {
            _expandedWindow = new ExpandedScreenWindow { DataContext = this };
            _expandedWindow.Show();
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
            _systemInfoRefreshTimer.Stop();
        }

        public void OnNavigatedTo()
        {
            _ = OnActivatedAsync();
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
