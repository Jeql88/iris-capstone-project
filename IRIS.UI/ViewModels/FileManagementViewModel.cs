using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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

namespace IRIS.UI.ViewModels
{
    public class FileManagementViewModel : INotifyPropertyChanged
    {
        private readonly IDeploymentDataService _deploymentDataService;
        private readonly IRoomService _roomService;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(120) };

        private readonly int _agentFileApiPort;
        private readonly string _agentApiToken;

        // PC selection
        private RoomDto? _selectedRoom;
        private PCModel? _selectedPC;

        // Local browser state
        private string _currentLocalPath = string.Empty;
        private FileItemModel? _selectedLocalFile;
        private bool _isAtLocalDriveRoot = true;

        // Remote browser state
        private string _currentRemotePath = string.Empty;
        private FileItemModel? _selectedRemoteFile;
        private bool _isAtRemoteDriveRoot = true;
        private string? _remoteParentPath;

        private string _statusMessage = "Ready";
        private bool _isBusy;

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

            // PC commands
            RefreshPCsCommand = new RelayCommand(async () => await LoadPCsAsync(), () => true);

            // Local navigation commands
            NavigateUpLocalCommand = new RelayCommand(async () => await NavigateUpLocalAsync(), () => !_isAtLocalDriveRoot);
            NavigateToLocalPathCommand = new RelayCommand(async () => await BrowseLocalPathAsync(CurrentLocalPath), () => true);
            GoToLocalDrivesCommand = new RelayCommand(async () => { CurrentLocalPath = string.Empty; await LoadLocalDrivesAsync(); }, () => true);

            // Remote navigation commands
            NavigateUpRemoteCommand = new RelayCommand(async () => await NavigateUpRemoteAsync(), () => !_isAtRemoteDriveRoot && _selectedPC != null);
            NavigateToRemotePathCommand = new RelayCommand(async () => await BrowseRemotePathAsync(CurrentRemotePath), () => _selectedPC != null);
            GoToRemoteDrivesCommand = new RelayCommand(async () => { CurrentRemotePath = string.Empty; await LoadRemoteDrivesAsync(); }, () => _selectedPC != null);

            // Context-menu / action commands
            DeleteLocalFileCommand = new RelayCommand<FileItemModel>(item => _ = DeleteLocalItemAsync(item));
            DeleteRemoteFileCommand = new RelayCommand<FileItemModel>(item => _ = DeleteRemoteItemAsync(item));
            RenameLocalFileCommand = new RelayCommand<FileItemModel>(item => _ = RenameLocalItemAsync(item));
            RenameRemoteFileCommand = new RelayCommand<FileItemModel>(item => _ = RenameRemoteItemAsync(item));
            DownloadRemoteFileCommand = new RelayCommand<FileItemModel>(item => _ = DownloadRemoteItemAsync(item));
            UploadLocalFileCommand = new RelayCommand<FileItemModel>(item => _ = UploadLocalItemAsync(item));

            _ = InitializeAsync();
        }

        // ═══════════════════════════════════════
        // Collections
        // ═══════════════════════════════════════

        public ObservableCollection<RoomDto> Rooms { get; } = new();
        public ObservableCollection<PCModel> PCs { get; } = new();
        public ObservableCollection<FileItemModel> LocalFiles { get; } = new();
        public ObservableCollection<FileItemModel> RemoteFiles { get; } = new();

        // ═══════════════════════════════════════
        // Properties
        // ═══════════════════════════════════════

        public RoomDto? SelectedRoom
        {
            get => _selectedRoom;
            set { _selectedRoom = value; OnPropertyChanged(); _ = LoadPCsAsync(); }
        }

        public PCModel? SelectedPC
        {
            get => _selectedPC;
            set
            {
                _selectedPC = value;
                OnPropertyChanged();
                RaiseAllRemoteCanExecuteChanged();
                if (_selectedPC != null)
                    _ = LoadRemoteDrivesAsync();
                else
                {
                    RemoteFiles.Clear();
                    CurrentRemotePath = string.Empty;
                    IsAtRemoteDriveRoot = true;
                }
            }
        }

        public string CurrentLocalPath
        {
            get => _currentLocalPath;
            set { _currentLocalPath = value; OnPropertyChanged(); }
        }

        public FileItemModel? SelectedLocalFile
        {
            get => _selectedLocalFile;
            set { _selectedLocalFile = value; OnPropertyChanged(); }
        }

        public string CurrentRemotePath
        {
            get => _currentRemotePath;
            set { _currentRemotePath = value; OnPropertyChanged(); }
        }

        public FileItemModel? SelectedRemoteFile
        {
            get => _selectedRemoteFile;
            set { _selectedRemoteFile = value; OnPropertyChanged(); }
        }

        public bool IsAtLocalDriveRoot
        {
            get => _isAtLocalDriveRoot;
            private set
            {
                _isAtLocalDriveRoot = value;
                OnPropertyChanged();
                (NavigateUpLocalCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public bool IsAtRemoteDriveRoot
        {
            get => _isAtRemoteDriveRoot;
            private set
            {
                _isAtRemoteDriveRoot = value;
                OnPropertyChanged();
                (NavigateUpRemoteCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        // ═══════════════════════════════════════
        // Commands
        // ═══════════════════════════════════════

        public ICommand RefreshPCsCommand { get; }
        public ICommand NavigateUpLocalCommand { get; }
        public ICommand NavigateToLocalPathCommand { get; }
        public ICommand GoToLocalDrivesCommand { get; }
        public ICommand NavigateUpRemoteCommand { get; }
        public ICommand NavigateToRemotePathCommand { get; }
        public ICommand GoToRemoteDrivesCommand { get; }
        public ICommand DeleteLocalFileCommand { get; }
        public ICommand DeleteRemoteFileCommand { get; }
        public ICommand RenameLocalFileCommand { get; }
        public ICommand RenameRemoteFileCommand { get; }
        public ICommand DownloadRemoteFileCommand { get; }
        public ICommand UploadLocalFileCommand { get; }

        // ═══════════════════════════════════════
        // Initialization
        // ═══════════════════════════════════════

        private async Task InitializeAsync()
        {
            try
            {
                var rooms = await _roomService.GetRoomsAsync();
                Rooms.Clear();
                Rooms.Add(new RoomDto(-1, "All Rooms", string.Empty, 0, true, DateTime.UtcNow));
                foreach (var room in rooms.OrderBy(r => r.RoomNumber))
                    Rooms.Add(room);

                SelectedRoom = Rooms.FirstOrDefault();
                await LoadPCsAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Init failed: {ex.Message}";
            }

            await LoadLocalDrivesAsync();
        }

        // ═══════════════════════════════════════
        // PC Loading
        // ═══════════════════════════════════════

        private async Task LoadPCsAsync()
        {
            try
            {
                int? roomId = SelectedRoom != null && SelectedRoom.Id > 0 ? SelectedRoom.Id : null;
                var pcs = await _deploymentDataService.GetRegisteredPCsAsync(roomId);

                PCs.Clear();
                foreach (var pc in pcs)
                {
                    PCs.Add(new PCModel
                    {
                        Id = pc.Id,
                        Hostname = pc.Hostname ?? "Unknown",
                        IPAddress = pc.IpAddress ?? "N/A",
                        Status = pc.Status,
                        RoomNumber = pc.RoomNumber
                    });
                }

                if (_selectedPC == null)
                    SelectedPC = PCs.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.IPAddress) && p.IPAddress != "N/A");

                StatusMessage = $"Loaded {PCs.Count} PCs.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to load PCs: {ex.Message}";
            }
        }

        // ═══════════════════════════════════════
        // LOCAL FILE BROWSER
        // ═══════════════════════════════════════

        public Task LoadLocalDrivesAsync()
        {
            LocalFiles.Clear();
            CurrentLocalPath = string.Empty;
            IsAtLocalDriveRoot = true;

            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    try
                    {
                        if (!drive.IsReady) continue;
                        LocalFiles.Add(new FileItemModel
                        {
                            Name = $"{drive.Name.TrimEnd('\\')} ({drive.VolumeLabel})",
                            FullPath = drive.Name,
                            IsDirectory = true,
                            IsDrive = true,
                            Length = drive.TotalSize,
                        });
                    }
                    catch { /* skip inaccessible drives */ }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Could not enumerate drives: {ex.Message}";
            }

            return Task.CompletedTask;
        }

        public async Task BrowseLocalPathAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                await LoadLocalDrivesAsync();
                return;
            }

            try
            {
                var fullPath = Path.GetFullPath(path);
                if (!Directory.Exists(fullPath))
                {
                    StatusMessage = $"Local path not found: {path}";
                    return;
                }

                var items = await Task.Run(() =>
                {
                    var list = new List<FileItemModel>();
                    var dirInfo = new DirectoryInfo(fullPath);

                    foreach (var dir in dirInfo.EnumerateDirectories())
                    {
                        try
                        {
                            list.Add(new FileItemModel
                            {
                                Name = dir.Name,
                                FullPath = dir.FullName,
                                IsDirectory = true,
                                LastWriteTimeUtc = dir.LastWriteTimeUtc
                            });
                        }
                        catch { /* skip inaccessible */ }
                    }

                    foreach (var file in dirInfo.EnumerateFiles())
                    {
                        try
                        {
                            list.Add(new FileItemModel
                            {
                                Name = file.Name,
                                FullPath = file.FullName,
                                IsDirectory = false,
                                Length = file.Length,
                                LastWriteTimeUtc = file.LastWriteTimeUtc
                            });
                        }
                        catch { /* skip inaccessible */ }
                    }

                    return list;
                });

                LocalFiles.Clear();
                foreach (var item in items.OrderByDescending(i => i.IsDirectory).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase))
                    LocalFiles.Add(item);

                CurrentLocalPath = fullPath;
                IsAtLocalDriveRoot = false;
                StatusMessage = $"Local: {LocalFiles.Count} item(s) in {fullPath}";
            }
            catch (UnauthorizedAccessException)
            {
                StatusMessage = $"Access denied: {path}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Local browse failed: {ex.Message}";
            }
        }

        public async Task OpenLocalItemAsync(FileItemModel? item)
        {
            if (item == null) return;

            if (item.IsDirectory || item.IsDrive)
            {
                await BrowseLocalPathAsync(item.FullPath);
                return;
            }

            // Double-click file → open with default program
            try
            {
                Process.Start(new ProcessStartInfo(item.FullPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                StatusMessage = $"Could not open file: {ex.Message}";
            }
        }

        private async Task NavigateUpLocalAsync()
        {
            if (string.IsNullOrWhiteSpace(CurrentLocalPath))
            {
                await LoadLocalDrivesAsync();
                return;
            }

            var parent = Directory.GetParent(CurrentLocalPath);
            if (parent == null)
            {
                await LoadLocalDrivesAsync();
                return;
            }

            await BrowseLocalPathAsync(parent.FullName);
        }

        private async Task DeleteLocalItemAsync(FileItemModel? item)
        {
            if (item == null) return;

            var confirm = MessageBox.Show(
                $"Delete '{item.Name}'?", "Delete",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                await Task.Run(() =>
                {
                    if (item.IsDirectory)
                        Directory.Delete(item.FullPath, true);
                    else
                        File.Delete(item.FullPath);
                });

                StatusMessage = $"Deleted '{item.Name}'.";
                await BrowseLocalPathAsync(CurrentLocalPath);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Delete failed: {ex.Message}";
            }
        }

        private async Task RenameLocalItemAsync(FileItemModel? item)
        {
            if (item == null) return;

            var newName = PromptForNewName(item.Name);
            if (string.IsNullOrWhiteSpace(newName) || newName == item.Name) return;

            try
            {
                var dir = Path.GetDirectoryName(item.FullPath)!;
                var newPath = Path.Combine(dir, newName);

                await Task.Run(() =>
                {
                    if (item.IsDirectory)
                        Directory.Move(item.FullPath, newPath);
                    else
                        File.Move(item.FullPath, newPath);
                });

                StatusMessage = $"Renamed '{item.Name}' → '{newName}'.";
                await BrowseLocalPathAsync(CurrentLocalPath);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Rename failed: {ex.Message}";
            }
        }

        private async Task UploadLocalItemAsync(FileItemModel? item)
        {
            if (item == null || _selectedPC == null) return;

            if (string.IsNullOrWhiteSpace(CurrentRemotePath))
            {
                StatusMessage = "Navigate to a remote directory first.";
                return;
            }

            try
            {
                IsBusy = true;

                if (item.IsDirectory)
                    await UploadDirectoryAsync(item.FullPath, CurrentRemotePath);
                else
                    await UploadSingleFileAsync(item.FullPath, CurrentRemotePath);

                StatusMessage = $"Uploaded '{item.Name}' to remote.";
                await BrowseRemotePathAsync(CurrentRemotePath);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Upload failed: {ex.Message}";
            }
            finally { IsBusy = false; }
        }

        // ═══════════════════════════════════════
        // REMOTE FILE BROWSER
        // ═══════════════════════════════════════

        public async Task LoadRemoteDrivesAsync()
        {
            if (_selectedPC == null) return;

            try
            {
                IsBusy = true;
                RemoteFiles.Clear();
                CurrentRemotePath = string.Empty;
                IsAtRemoteDriveRoot = true;
                _remoteParentPath = null;

                var responsePair = await SendAgentRequestWithFallbackAsync(
                    _selectedPC,
                    target => new HttpRequestMessage(HttpMethod.Get, BuildAgentUri(target, "/files/drives")));
                using var response = responsePair.Response;

                if (!response.IsSuccessStatusCode)
                {
                    StatusMessage = $"Could not list remote drives: {response.StatusCode}";
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();
                var drives = JsonSerializer.Deserialize<List<AgentDriveEntry>>(json, JsonOptions()) ?? [];

                foreach (var drive in drives)
                {
                    RemoteFiles.Add(new FileItemModel
                    {
                        Name = $"{drive.Name?.TrimEnd('\\')} ({drive.Label})",
                        FullPath = drive.Name ?? string.Empty,
                        IsDirectory = true,
                        IsDrive = true,
                        Length = drive.TotalSize
                    });
                }

                StatusMessage = $"Remote: {RemoteFiles.Count} drive(s) on {responsePair.Target}.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Remote drives failed: {ex.Message}";
            }
            finally { IsBusy = false; }
        }

        public async Task BrowseRemotePathAsync(string path)
        {
            if (_selectedPC == null) return;

            if (string.IsNullOrWhiteSpace(path))
            {
                await LoadRemoteDrivesAsync();
                return;
            }

            try
            {
                IsBusy = true;
                RemoteFiles.Clear();

                var apiPath = $"/files/browse?path={Uri.EscapeDataString(path)}";
                var responsePair = await SendAgentRequestWithFallbackAsync(
                    _selectedPC,
                    target => new HttpRequestMessage(HttpMethod.Get, BuildAgentUri(target, apiPath)));
                using var response = responsePair.Response;

                if (!response.IsSuccessStatusCode)
                {
                    StatusMessage = $"Remote browse failed: {response.StatusCode}";
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();
                var browse = JsonSerializer.Deserialize<AgentBrowseResponse>(json, JsonOptions());
                var files = browse?.Entries ?? [];

                CurrentRemotePath = browse?.CurrentPath ?? path;
                _remoteParentPath = browse?.ParentPath;
                IsAtRemoteDriveRoot = false;

                foreach (var file in files)
                {
                    RemoteFiles.Add(new FileItemModel
                    {
                        Name = file.Name ?? string.Empty,
                        FullPath = file.FullPath ?? string.Empty,
                        IsDirectory = file.IsDirectory,
                        Length = file.Length,
                        LastWriteTimeUtc = file.LastWriteTimeUtc
                    });
                }

                StatusMessage = $"Remote: {RemoteFiles.Count} item(s) at '{CurrentRemotePath}' via {responsePair.Target}.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Remote browse failed: {ex.Message}";
            }
            finally { IsBusy = false; }
        }

        public async Task OpenRemoteItemAsync(FileItemModel? item)
        {
            if (item == null) return;

            if (item.IsDirectory || item.IsDrive)
            {
                await BrowseRemotePathAsync(item.FullPath);
                return;
            }

            // Double-click file → download to local current directory
            await DownloadRemoteItemToLocalAsync(item);
        }

        private async Task NavigateUpRemoteAsync()
        {
            if (string.IsNullOrWhiteSpace(_remoteParentPath))
            {
                await LoadRemoteDrivesAsync();
                return;
            }

            await BrowseRemotePathAsync(_remoteParentPath);
        }

        private async Task DeleteRemoteItemAsync(FileItemModel? item)
        {
            if (item == null || _selectedPC == null) return;

            var confirm = MessageBox.Show(
                $"Delete '{item.Name}' on {_selectedPC.Hostname}?",
                "Delete Remote Item", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                IsBusy = true;
                var apiPath = $"/files?path={Uri.EscapeDataString(item.FullPath)}";
                var responsePair = await SendAgentRequestWithFallbackAsync(
                    _selectedPC,
                    target => new HttpRequestMessage(HttpMethod.Delete, BuildAgentUri(target, apiPath)));
                using var response = responsePair.Response;

                if (!response.IsSuccessStatusCode)
                {
                    StatusMessage = $"Delete failed: {response.StatusCode}";
                    return;
                }

                StatusMessage = $"Deleted '{item.Name}' on remote.";
                await BrowseRemotePathAsync(CurrentRemotePath);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Delete failed: {ex.Message}";
            }
            finally { IsBusy = false; }
        }

        private async Task RenameRemoteItemAsync(FileItemModel? item)
        {
            if (item == null || _selectedPC == null) return;

            var newName = PromptForNewName(item.Name);
            if (string.IsNullOrWhiteSpace(newName) || newName == item.Name) return;

            try
            {
                IsBusy = true;
                var apiPath = $"/files/rename?path={Uri.EscapeDataString(item.FullPath)}&newName={Uri.EscapeDataString(newName)}";
                var responsePair = await SendAgentRequestWithFallbackAsync(
                    _selectedPC,
                    target => new HttpRequestMessage(HttpMethod.Post, BuildAgentUri(target, apiPath)));
                using var response = responsePair.Response;

                if (!response.IsSuccessStatusCode)
                {
                    StatusMessage = $"Rename failed: {response.StatusCode}";
                    return;
                }

                StatusMessage = $"Renamed '{item.Name}' → '{newName}'.";
                await BrowseRemotePathAsync(CurrentRemotePath);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Rename failed: {ex.Message}";
            }
            finally { IsBusy = false; }
        }

        private async Task DownloadRemoteItemAsync(FileItemModel? item)
        {
            if (item == null || _selectedPC == null) return;

            try
            {
                IsBusy = true;
                var defaultFileName = item.IsDirectory ? $"{item.Name}.zip" : item.Name;
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = defaultFileName,
                    Filter = item.IsDirectory
                        ? "Zip files (*.zip)|*.zip|All files (*.*)|*.*"
                        : "All files (*.*)|*.*",
                    InitialDirectory = string.IsNullOrWhiteSpace(CurrentLocalPath)
                        ? Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                        : CurrentLocalPath
                };

                if (dialog.ShowDialog() != true) return;

                await DownloadToPathAsync(item, dialog.FileName);
                StatusMessage = $"Downloaded '{item.Name}' → '{dialog.FileName}'.";

                // Refresh local pane if downloading into current local dir
                if (!string.IsNullOrWhiteSpace(CurrentLocalPath))
                    await BrowseLocalPathAsync(CurrentLocalPath);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Download failed: {ex.Message}";
            }
            finally { IsBusy = false; }
        }

        private async Task DownloadRemoteItemToLocalAsync(FileItemModel item)
        {
            if (_selectedPC == null) return;

            try
            {
                IsBusy = true;
                var localDir = string.IsNullOrWhiteSpace(CurrentLocalPath)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                    : CurrentLocalPath;
                var localPath = Path.Combine(localDir, item.IsDirectory ? $"{item.Name}.zip" : item.Name);

                if (File.Exists(localPath))
                {
                    var confirm = MessageBox.Show(
                        $"'{Path.GetFileName(localPath)}' already exists locally. Overwrite?",
                        "Overwrite", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (confirm != MessageBoxResult.Yes) return;
                }

                await DownloadToPathAsync(item, localPath);
                StatusMessage = $"Downloaded '{item.Name}' → '{localPath}'.";

                if (!string.IsNullOrWhiteSpace(CurrentLocalPath))
                    await BrowseLocalPathAsync(CurrentLocalPath);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Download failed: {ex.Message}";
            }
            finally { IsBusy = false; }
        }

        private async Task DownloadToPathAsync(FileItemModel item, string localPath)
        {
            var apiPath = $"/files/download?path={Uri.EscapeDataString(item.FullPath)}";
            var responsePair = await SendAgentRequestWithFallbackAsync(
                _selectedPC!,
                target => new HttpRequestMessage(HttpMethod.Get, BuildAgentUri(target, apiPath)),
                HttpCompletionOption.ResponseHeadersRead);
            using var response = responsePair.Response;

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Download failed: {response.StatusCode}");

            await using var remoteStream = await response.Content.ReadAsStreamAsync();
            await using var localFile = File.Create(localPath);
            await remoteStream.CopyToAsync(localFile);
        }

        // ═══════════════════════════════════════
        // UPLOAD (Local → Remote)
        // ═══════════════════════════════════════

        public async Task UploadDroppedFilesToRemoteAsync(string[] localPaths)
        {
            if (_selectedPC == null || string.IsNullOrWhiteSpace(CurrentRemotePath))
            {
                StatusMessage = "Select a PC and navigate to a remote directory first.";
                return;
            }

            try
            {
                IsBusy = true;
                int count = 0;

                foreach (var path in localPaths)
                {
                    if (File.Exists(path))
                    {
                        await UploadSingleFileAsync(path, CurrentRemotePath);
                        count++;
                    }
                    else if (Directory.Exists(path))
                    {
                        await UploadDirectoryAsync(path, CurrentRemotePath);
                        count++;
                    }
                }

                StatusMessage = $"Uploaded {count} item(s) to remote.";
                await BrowseRemotePathAsync(CurrentRemotePath);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Upload failed: {ex.Message}";
            }
            finally { IsBusy = false; }
        }

        private async Task UploadSingleFileAsync(string localFilePath, string remoteDir)
        {
            var fileName = Path.GetFileName(localFilePath);
            var apiPath = $"/files/upload?path={Uri.EscapeDataString(remoteDir)}&fileName={Uri.EscapeDataString(fileName)}";

            var (response, _) = await SendAgentRequestWithFallbackAsync(
                _selectedPC!,
                target =>
                {
                    var req = new HttpRequestMessage(HttpMethod.Post, BuildAgentUri(target, apiPath))
                    {
                        Content = new StreamContent(File.OpenRead(localFilePath))
                    };
                    req.Content.Headers.ContentType =
                        new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                    return req;
                });

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Upload failed ({response.StatusCode})");
        }

        private async Task UploadDirectoryAsync(string localDirPath, string remoteBaseDir)
        {
            var dirName = Path.GetFileName(localDirPath);
            var remotePath = string.IsNullOrWhiteSpace(remoteBaseDir)
                ? dirName
                : Path.Combine(remoteBaseDir, dirName);

            foreach (var file in Directory.EnumerateFiles(localDirPath))
            {
                await UploadSingleFileAsync(file, remotePath);
            }

            foreach (var subDir in Directory.EnumerateDirectories(localDirPath))
            {
                await UploadDirectoryAsync(subDir, remotePath);
            }
        }

        // ═══════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════

        private static string? PromptForNewName(string currentName)
        {
            var dialog = new Views.Dialogs.RenameDialog(currentName);
            dialog.Owner = Application.Current.MainWindow;
            return dialog.ShowDialog() == true ? dialog.NewName : null;
        }

        private void RaiseAllRemoteCanExecuteChanged()
        {
            (NavigateUpRemoteCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (NavigateToRemotePathCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (GoToRemoteDrivesCommand as RelayCommand)?.RaiseCanExecuteChanged();
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
                catch (HttpRequestException ex) { lastException = ex; }
                catch (TaskCanceledException ex) { lastException = ex; }
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
                    targets.Add(trimmed);
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
                catch { /* best-effort fallback only */ }
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

        private static JsonSerializerOptions JsonOptions() =>
            new() { PropertyNameCaseInsensitive = true };

        // ═══════════════════════════════════════
        // INotifyPropertyChanged
        // ═══════════════════════════════════════

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        // ═══════════════════════════════════════
        // Agent response models
        // ═══════════════════════════════════════

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

        private sealed class AgentDriveEntry
        {
            public string? Name { get; set; }
            public string? Label { get; set; }
            public long TotalSize { get; set; }
            public long FreeSpace { get; set; }
        }
    }
}
