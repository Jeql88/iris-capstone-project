using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using IRIS.Core.DTOs;
using IRIS.Core.Services.Contracts;
using IRIS.UI.Helpers;
using IRIS.UI.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;

namespace IRIS.UI.ViewModels
{
    public class FileManagementViewModel : INotifyPropertyChanged
    {
        private readonly IDeploymentDataService _deploymentDataService;
        private readonly IRoomService _roomService;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(60) };

        private string _currentRemotePath = ".";
        private string _remoteSearchText = string.Empty;
        private string? _remoteParentPath;
        private string _statusMessage = "Ready";
        private RoomDto? _selectedRoom;
        private PCModel? _selectedFileManagementPC;
        private RemoteFileItemModel? _selectedRemoteFile;

        private readonly int _agentFileApiPort;
        private readonly string _agentApiToken;

        public FileManagementViewModel(
            IDeploymentDataService deploymentDataService,
            IRoomService roomService,
            IConfiguration configuration)
        {
            _deploymentDataService = deploymentDataService;
            _roomService = roomService;
            _configuration = configuration;

            _agentFileApiPort = int.TryParse(_configuration["AgentSettings:FileApiPort"], out var port) ? port : 5065;
            _agentApiToken = _configuration["AgentSettings:FileApiToken"] ?? string.Empty;

            RefreshPCsCommand = new RelayCommand(async () => await LoadPCsAsync(), () => true);

            BrowseUploadFilesCommand = new RelayCommand(BrowseUploadFiles, () => true);
            UploadFilesCommand = new RelayCommand(async () => await UploadFilesAsync(), CanUploadFiles);
            LoadRemoteFilesCommand = new RelayCommand(async () => await LoadRemoteFilesAsync(), () => SelectedFileManagementPC != null);
            NavigateUpRemotePathCommand = new RelayCommand(async () => await NavigateUpRemotePathAsync(), () => SelectedFileManagementPC != null && CanNavigateUp);
            SearchRemoteFilesCommand = new RelayCommand(async () => await LoadRemoteFilesAsync(), () => SelectedFileManagementPC != null);
            ClearRemoteSearchCommand = new RelayCommand(async () => await ClearRemoteSearchAsync(), () => SelectedFileManagementPC != null);
            OpenRemoteFolderCommand = new RelayCommand<RemoteFileItemModel>(item => _ = OpenRemoteItemAsync(item));
            DownloadRemoteFileCommand = new RelayCommand<RemoteFileItemModel>(item => _ = DownloadRemoteItemAsync(item));
            DeleteRemoteFileCommand = new RelayCommand<RemoteFileItemModel>(item => _ = DeleteRemoteFileAsync(item));
            RemovePendingUploadCommand = new RelayCommand<string>(RemovePendingUploadFile);

            _ = InitializeAsync();
        }

        public ObservableCollection<PCModel> PCs { get; } = new();
        public ObservableCollection<RoomDto> Rooms { get; } = new();
        public ObservableCollection<RemoteFileItemModel> RemoteFiles { get; } = new();
        public ObservableCollection<string> PendingUploadFiles { get; } = new();

        public RoomDto? SelectedRoom
        {
            get => _selectedRoom;
            set
            {
                _selectedRoom = value;
                OnPropertyChanged();
                _ = LoadPCsAsync();
            }
        }

        public PCModel? SelectedFileManagementPC
        {
            get => _selectedFileManagementPC;
            set
            {
                _selectedFileManagementPC = value;
                OnPropertyChanged();
                (LoadRemoteFilesCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (NavigateUpRemotePathCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (SearchRemoteFilesCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (ClearRemoteSearchCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (UploadFilesCommand as RelayCommand)?.RaiseCanExecuteChanged();
                _ = LoadRemoteFilesAsync();
            }
        }

        public RemoteFileItemModel? SelectedRemoteFile
        {
            get => _selectedRemoteFile;
            set
            {
                _selectedRemoteFile = value;
                OnPropertyChanged();
            }
        }

        public string CurrentRemotePath
        {
            get => _currentRemotePath;
            set
            {
                _currentRemotePath = value;
                OnPropertyChanged();
            }
        }

        public string RemoteSearchText
        {
            get => _remoteSearchText;
            set
            {
                _remoteSearchText = value;
                OnPropertyChanged();
            }
        }

        public bool CanNavigateUp => !string.IsNullOrWhiteSpace(_remoteParentPath);

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public ICommand RefreshPCsCommand { get; }
        public ICommand BrowseUploadFilesCommand { get; }
        public ICommand UploadFilesCommand { get; }
        public ICommand LoadRemoteFilesCommand { get; }
        public ICommand NavigateUpRemotePathCommand { get; }
        public ICommand SearchRemoteFilesCommand { get; }
        public ICommand ClearRemoteSearchCommand { get; }
        public ICommand OpenRemoteFolderCommand { get; }
        public ICommand DownloadRemoteFileCommand { get; }
        public ICommand DeleteRemoteFileCommand { get; }
        public ICommand RemovePendingUploadCommand { get; }

        private async Task InitializeAsync()
        {
            try
            {
                var rooms = await _roomService.GetRoomsAsync();
                Rooms.Clear();
                Rooms.Add(new RoomDto(-1, "All Rooms", string.Empty, 0, true, DateTime.UtcNow));
                foreach (var room in rooms.OrderBy(r => r.RoomNumber))
                {
                    Rooms.Add(room);
                }

                SelectedRoom = Rooms.FirstOrDefault();

                await LoadPCsAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Initialization failed: {ex.Message}";
            }
        }

        public async Task LoadPCsAsync()
        {
            try
            {
                int? roomId = SelectedRoom != null && SelectedRoom.Id > 0 ? SelectedRoom.Id : null;
                var pcs = await _deploymentDataService.GetRegisteredPCsAsync(roomId);

                PCs.Clear();
                foreach (var pc in pcs)
                {
                    var item = new PCModel
                    {
                        Id = pc.Id,
                        Hostname = pc.Hostname ?? "Unknown",
                        IPAddress = pc.IpAddress ?? "N/A",
                        Status = pc.Status,
                        RoomNumber = pc.RoomNumber
                    };
                    PCs.Add(item);
                }

                if (SelectedFileManagementPC == null)
                {
                    SelectedFileManagementPC = PCs.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.IPAddress) && p.IPAddress != "N/A");
                }

                StatusMessage = $"Loaded {PCs.Count} registered PCs.";
                (UploadFilesCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to load PCs: {ex.Message}";
            }
        }

        public void AddPendingUploadFiles(IEnumerable<string> files)
        {
            foreach (var file in files)
            {
                if (!PendingUploadFiles.Contains(file, StringComparer.OrdinalIgnoreCase) && File.Exists(file))
                {
                    PendingUploadFiles.Add(file);
                }
            }

            (UploadFilesCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        public async Task UploadDroppedFilesAsync(IEnumerable<string> files)
        {
            AddPendingUploadFiles(files);

            if (CanUploadFiles())
            {
                await UploadFilesAsync();
                return;
            }

            if (!PCs.Any(p => p.IsSelected))
            {
                StatusMessage = "Select at least one PC before dropping files to upload.";
            }
        }

        public async Task OpenRemoteItemAsync(RemoteFileItemModel? item)
        {
            if (item == null || !item.IsDirectory)
            {
                return;
            }

            CurrentRemotePath = item.FullPath;
            await LoadRemoteFilesAsync();
        }

        private async Task NavigateUpRemotePathAsync()
        {
            if (string.IsNullOrWhiteSpace(_remoteParentPath))
            {
                return;
            }

            CurrentRemotePath = _remoteParentPath;
            await LoadRemoteFilesAsync();
        }

        private async Task ClearRemoteSearchAsync()
        {
            RemoteSearchText = string.Empty;
            await LoadRemoteFilesAsync();
        }

        private void BrowseUploadFiles()
        {
            var dialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                AddPendingUploadFiles(dialog.FileNames);
            }
        }

        private bool CanUploadFiles()
        {
            if (PendingUploadFiles.Count == 0)
            {
                return false;
            }

            return SelectedFileManagementPC != null &&
                   !string.IsNullOrWhiteSpace(SelectedFileManagementPC.IPAddress) &&
                   SelectedFileManagementPC.IPAddress != "N/A";
        }

        private async Task UploadFilesAsync()
        {
            var selectedPcs = new List<PCModel>();
            if (SelectedFileManagementPC != null)
            {
                selectedPcs.Add(SelectedFileManagementPC);
            }

            if (!selectedPcs.Any() || PendingUploadFiles.Count == 0)
            {
                return;
            }

            StatusMessage = "Uploading files...";

            foreach (var pc in selectedPcs)
            {
                foreach (var localPath in PendingUploadFiles.ToList())
                {
                    await UploadFileToPcAsync(pc, localPath);
                }
            }

            StatusMessage = "File upload completed.";
            PendingUploadFiles.Clear();
            (UploadFilesCommand as RelayCommand)?.RaiseCanExecuteChanged();

            await LoadRemoteFilesAsync();
        }

        private async Task UploadFileToPcAsync(PCModel pc, string localPath)
        {
            var fileName = Path.GetFileName(localPath);
            var relativePath = $"/files/upload?path={Uri.EscapeDataString(CurrentRemotePath)}&fileName={Uri.EscapeDataString(fileName)}";
            var (response, target) = await SendAgentRequestWithFallbackAsync(
                pc,
                target =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, BuildAgentUri(target, relativePath))
                    {
                        Content = new StreamContent(File.OpenRead(localPath))
                    };
                    request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                    return request;
                });

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Upload failed for {pc.Hostname} via {target} ({response.StatusCode})");
            }
        }

        private async Task LoadRemoteFilesAsync()
        {
            try
            {
                RemoteFiles.Clear();

                if (SelectedFileManagementPC == null || string.IsNullOrWhiteSpace(SelectedFileManagementPC.IPAddress) || SelectedFileManagementPC.IPAddress == "N/A")
                {
                    _remoteParentPath = null;
                    OnPropertyChanged(nameof(CanNavigateUp));
                    (NavigateUpRemotePathCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    return;
                }

                var relativePath = $"/files/browse?path={Uri.EscapeDataString(CurrentRemotePath)}&search={Uri.EscapeDataString(RemoteSearchText ?? string.Empty)}";
                var responsePair = await SendAgentRequestWithFallbackAsync(
                    SelectedFileManagementPC,
                    target => new HttpRequestMessage(HttpMethod.Get, BuildAgentUri(target, relativePath)));
                using var response = responsePair.Response;
                if (!response.IsSuccessStatusCode)
                {
                    StatusMessage = $"Could not read remote files: {response.StatusCode}";
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();
                var browse = JsonSerializer.Deserialize<AgentBrowseResponse>(json, JsonOptions());
                var files = browse?.Entries ?? [];
                CurrentRemotePath = string.IsNullOrWhiteSpace(browse?.CurrentPath) ? "." : browse.CurrentPath;
                _remoteParentPath = browse?.ParentPath;
                OnPropertyChanged(nameof(CanNavigateUp));
                (NavigateUpRemotePathCommand as RelayCommand)?.RaiseCanExecuteChanged();

                foreach (var file in files.OrderBy(f => !f.IsDirectory).ThenBy(f => f.Name))
                {
                    RemoteFiles.Add(new RemoteFileItemModel
                    {
                        Name = file.Name ?? string.Empty,
                        FullPath = file.FullPath ?? string.Empty,
                        IsDirectory = file.IsDirectory,
                        Length = file.Length,
                        LastWriteTimeUtc = file.LastWriteTimeUtc
                    });
                }

                StatusMessage = $"Showing {RemoteFiles.Count} item(s) at '{CurrentRemotePath}' from {responsePair.Target}.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Remote file load failed: {ex.Message}";
            }
        }

        private async Task DeleteRemoteFileAsync(RemoteFileItemModel? item)
        {
            if (item == null || SelectedFileManagementPC == null)
            {
                return;
            }

            try
            {
                var confirm = MessageBox.Show($"Delete '{item.Name}' on {SelectedFileManagementPC.Hostname}?", "Delete Remote Item", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.Yes)
                {
                    return;
                }

                var relativePath = $"/files?path={Uri.EscapeDataString(item.FullPath)}";
                var responsePair = await SendAgentRequestWithFallbackAsync(
                    SelectedFileManagementPC,
                    target => new HttpRequestMessage(HttpMethod.Delete, BuildAgentUri(target, relativePath)));
                using var response = responsePair.Response;
                if (!response.IsSuccessStatusCode)
                {
                    StatusMessage = $"Delete failed via {responsePair.Target}: {response.StatusCode}";
                    return;
                }

                await LoadRemoteFilesAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Delete failed: {ex.Message}";
            }
        }

        private async Task DownloadRemoteItemAsync(RemoteFileItemModel? item)
        {
            if (item == null || SelectedFileManagementPC == null)
            {
                return;
            }

            try
            {
                var defaultFileName = item.IsDirectory ? $"{item.Name}.zip" : item.Name;
                var dialog = new SaveFileDialog
                {
                    FileName = defaultFileName,
                    Filter = item.IsDirectory ? "Zip files (*.zip)|*.zip|All files (*.*)|*.*" : "All files (*.*)|*.*",
                    AddExtension = true,
                    OverwritePrompt = true
                };

                if (dialog.ShowDialog() != true)
                {
                    return;
                }

                var relativePath = $"/files/download?path={Uri.EscapeDataString(item.FullPath)}";
                var responsePair = await SendAgentRequestWithFallbackAsync(
                    SelectedFileManagementPC,
                    target => new HttpRequestMessage(HttpMethod.Get, BuildAgentUri(target, relativePath)),
                    HttpCompletionOption.ResponseHeadersRead);
                using var response = responsePair.Response;
                if (!response.IsSuccessStatusCode)
                {
                    StatusMessage = $"Download failed via {responsePair.Target}: {response.StatusCode}";
                    return;
                }

                await using var remoteStream = await response.Content.ReadAsStreamAsync();
                await using var localFile = File.Create(dialog.FileName);
                await remoteStream.CopyToAsync(localFile);

                StatusMessage = $"Downloaded '{item.Name}' to '{dialog.FileName}' via {responsePair.Target}.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Download failed: {ex.Message}";
            }
        }

        private void RemovePendingUploadFile(string? localPath)
        {
            if (string.IsNullOrWhiteSpace(localPath)) return;
            PendingUploadFiles.Remove(localPath);
            (UploadFilesCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private Uri BuildAgentUri(string ipAddress, string relativePath)
        {
            var path = relativePath.StartsWith('/') ? relativePath : "/" + relativePath;
            return new Uri($"http://{ipAddress}:{_agentFileApiPort}/api{path}");
        }

        private async Task<(HttpResponseMessage Response, string Target)> SendAgentRequestWithFallbackAsync(
            PCModel pc,
            Func<string, HttpRequestMessage> requestFactory,
            HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead)
        {
            Exception? lastException = null;

            foreach (var target in await GetAgentTargetsAsync(pc))
            {
                using var request = requestFactory(target);
                AttachAuthHeader(request);

                try
                {
                    var response = await _httpClient.SendAsync(request, completionOption);
                    return (response, target);
                }
                catch (HttpRequestException ex)
                {
                    lastException = ex;
                }
                catch (TaskCanceledException ex)
                {
                    lastException = ex;
                }
            }

            throw lastException ?? new HttpRequestException($"Could not reach agent for PC '{pc.Hostname}'.");
        }

        private async Task<List<string>> GetAgentTargetsAsync(PCModel pc)
        {
            var targets = new List<string>();

            void Add(string? value)
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                var trimmed = value.Trim();
                if (trimmed.Equals("N/A", StringComparison.OrdinalIgnoreCase)) return;
                if (!targets.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
                {
                    targets.Add(trimmed);
                }
            }

            Add(pc.IPAddress);
            Add(pc.Hostname);

            if (!string.IsNullOrWhiteSpace(pc.Hostname))
            {
                try
                {
                    var resolved = await Dns.GetHostAddressesAsync(pc.Hostname);
                    foreach (var ip in resolved
                        .Where(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        .Where(x => !IPAddress.IsLoopback(x)))
                    {
                        Add(ip.ToString());
                    }
                }
                catch
                {
                    // best-effort fallback only
                }
            }

            return targets;
        }

        private void AttachAuthHeader(HttpRequestMessage request)
        {
            if (!string.IsNullOrWhiteSpace(_agentApiToken))
            {
                request.Headers.Remove("X-IRIS-Token");
                request.Headers.Add("X-IRIS-Token", _agentApiToken);
            }
        }

        private static JsonSerializerOptions JsonOptions()
        {
            return new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private sealed class AgentBrowseResponse
        {
            public string? CurrentPath { get; set; }
            public string? ParentPath { get; set; }
            public List<AgentFileEntry> Entries { get; set; } = [];
        }

        private sealed class AgentFileEntry
        {
            public string? Name { get; set; }
            public string? FullPath { get; set; }
            public bool IsDirectory { get; set; }
            public long Length { get; set; }
            public DateTime LastWriteTimeUtc { get; set; }
        }
    }
}
