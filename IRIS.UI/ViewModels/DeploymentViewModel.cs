using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
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
    public class DeploymentViewModel : INotifyPropertyChanged
    {
        private readonly IDeploymentDataService _deploymentDataService;
        private readonly IRoomService _roomService;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(60) };

        private bool _isDeploying;
        private string _selectedMsiLocalPath = string.Empty;
        private string _selectedMsiUncPath = string.Empty;
        private string _currentRemotePath = ".";
        private string _remoteSearchText = string.Empty;
        private string? _remoteParentPath;
        private string _statusMessage = "Ready";
        private RoomDto? _selectedRoom;
        private PCModel? _selectedFileManagementPC;
        private RemoteFileItemModel? _selectedRemoteFile;

        private readonly string _psExecPath;
        private readonly string _remoteUsername;
        private readonly string _remotePassword;
        private readonly int _installTimeoutSeconds;
        private readonly int _agentFileApiPort;
        private readonly string _agentApiToken;
        private readonly bool _useAgentCache;

        public DeploymentViewModel(
            IDeploymentDataService deploymentDataService,
            IRoomService roomService,
            IConfiguration configuration)
        {
            _deploymentDataService = deploymentDataService;
            _roomService = roomService;
            _configuration = configuration;

            _psExecPath = _configuration["DeploymentSettings:PsExecPath"] ?? "psexec.exe";
            _remoteUsername = _configuration["DeploymentSettings:RemoteUsername"] ?? string.Empty;
            _remotePassword = _configuration["DeploymentSettings:RemotePassword"] ?? string.Empty;
            _installTimeoutSeconds = int.TryParse(_configuration["DeploymentSettings:InstallTimeoutSeconds"], out var timeout) ? timeout : 900;
            _agentFileApiPort = int.TryParse(_configuration["AgentSettings:FileApiPort"], out var port) ? port : 5065;
            _agentApiToken = _configuration["AgentSettings:FileApiToken"] ?? string.Empty;
            _useAgentCache = !string.Equals(_configuration["DeploymentSettings:UseAgentCache"], "false", StringComparison.OrdinalIgnoreCase);

            RefreshPCsCommand = new RelayCommand(async () => await LoadPCsAsync(), () => !IsDeploying);
            BrowseMsiCommand = new RelayCommand(BrowseMsi, () => !IsDeploying);
            DeploySoftwareCommand = new RelayCommand(async () => await DeploySoftwareAsync(), CanDeploySoftware);
            SelectAllPCsCommand = new RelayCommand(SelectAllPCs, () => true);
            ClearSelectionCommand = new RelayCommand(ClearSelection, () => true);
            RefreshLogsCommand = new RelayCommand(async () => await LoadLogsAsync(), () => true);

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
        public ObservableCollection<DeploymentLogModel> DeploymentLogs { get; } = new();
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

        public string SelectedMsiLocalPath
        {
            get => _selectedMsiLocalPath;
            set
            {
                _selectedMsiLocalPath = value;
                OnPropertyChanged();
                (DeploySoftwareCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public string SelectedMsiUncPath
        {
            get => _selectedMsiUncPath;
            set
            {
                _selectedMsiUncPath = value;
                OnPropertyChanged();
                (DeploySoftwareCommand as RelayCommand)?.RaiseCanExecuteChanged();
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

        public bool IsDeploying
        {
            get => _isDeploying;
            set
            {
                _isDeploying = value;
                OnPropertyChanged();
                (RefreshPCsCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (BrowseMsiCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (DeploySoftwareCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public bool HasSelectedPCs => PCs.Any(p => p.IsSelected);

        public ICommand RefreshPCsCommand { get; }
        public ICommand BrowseMsiCommand { get; }
        public ICommand DeploySoftwareCommand { get; }
        public ICommand SelectAllPCsCommand { get; }
        public ICommand ClearSelectionCommand { get; }
        public ICommand RefreshLogsCommand { get; }

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
                await LoadLogsAsync();
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
                        RoomNumber = pc.RoomNumber,
                        DeploymentProgress = 0,
                        DeploymentStatus = "Idle"
                    };
                    item.PropertyChanged += Pc_PropertyChanged;
                    PCs.Add(item);
                }

                if (SelectedFileManagementPC == null)
                {
                    SelectedFileManagementPC = PCs.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.IPAddress) && p.IPAddress != "N/A");
                }

                StatusMessage = $"Loaded {PCs.Count} registered PCs.";
                OnPropertyChanged(nameof(HasSelectedPCs));
                (DeploySoftwareCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (UploadFilesCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to load PCs: {ex.Message}";
            }
        }

        private void Pc_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PCModel.IsSelected))
            {
                OnPropertyChanged(nameof(HasSelectedPCs));
                (DeploySoftwareCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (UploadFilesCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        private void BrowseMsi()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "MSI package (*.msi)|*.msi",
                Multiselect = false,
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                SelectedMsiLocalPath = dialog.FileName;
                SelectedMsiUncPath = ToUncPath(dialog.FileName);

                if (!SelectedMsiUncPath.StartsWith("\\\\", StringComparison.Ordinal))
                {
                    StatusMessage = "Selected MSI is local. Enter a server UNC path (e.g. \\\\SERVER\\Share\\app.msi) if targets cannot access this machine.";
                }
            }
        }

        private bool CanDeploySoftware()
        {
            return !IsDeploying
                && HasSelectedPCs
                && !string.IsNullOrWhiteSpace(SelectedMsiUncPath)
                && SelectedMsiUncPath.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(_remoteUsername)
                && !string.IsNullOrWhiteSpace(_remotePassword);
        }

        private async Task DeploySoftwareAsync()
        {
            var selectedPcs = PCs.Where(p => p.IsSelected).ToList();
            if (!selectedPcs.Any())
            {
                MessageBox.Show("Select at least one PC.", "Deployment", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var psexecExe = ResolvePsExecExecutable();
            if (string.IsNullOrWhiteSpace(psexecExe))
            {
                MessageBox.Show(
                    $"PsExec not found.\n\nConfigured value: {_psExecPath}\n\n" +
                    "Set DeploymentSettings:PsExecPath to a valid executable path or install PsExec and add it to PATH.",
                    "Deployment",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            IsDeploying = true;
            StatusMessage = $"Starting deployment to {selectedPcs.Count} PC(s)...";

            var msiFileName = Path.GetFileName(SelectedMsiUncPath);

            var tasks = selectedPcs.Select(pc => DeployToPcAsync(pc, msiFileName, psexecExe));
            await Task.WhenAll(tasks);

            IsDeploying = false;
            StatusMessage = "Deployment finished.";
            await LoadLogsAsync();
        }

        private async Task DeployToPcAsync(PCModel pc, string msiFileName, string psexecExe)
        {
            try
            {
                pc.DeploymentProgress = 5;
                pc.DeploymentStatus = "Preparing";

                var installPath = SelectedMsiUncPath;
                if (_useAgentCache)
                {
                    pc.DeploymentProgress = 15;
                    pc.DeploymentStatus = "Checking agent cache";
                    installPath = await EnsureAgentHasMsiAsync(pc, SelectedMsiUncPath, msiFileName) ?? SelectedMsiUncPath;
                }

                pc.DeploymentProgress = 40;
                pc.DeploymentStatus = "Running PsExec";

                var args = BuildPsExecInstallArguments(pc.IPAddress, _remoteUsername, _remotePassword, installPath);
                var result = await RunProcessAsync(psexecExe, args, _installTimeoutSeconds);

                var success = result.ExitCode == 0;
                pc.DeploymentProgress = 100;
                pc.DeploymentStatus = success ? "Installed" : "Failed";

                await _deploymentDataService.LogDeploymentResultAsync(new DeploymentLogCreateDto(
                    pc.Id,
                    pc.Hostname,
                    pc.IPAddress,
                    msiFileName,
                    success ? "Success" : "Failed",
                    success ? "Installation completed." : BuildErrorMessage(result),
                    DateTime.UtcNow));
            }
            catch (Exception ex)
            {
                pc.DeploymentProgress = 100;
                pc.DeploymentStatus = "Failed";

                await _deploymentDataService.LogDeploymentResultAsync(new DeploymentLogCreateDto(
                    pc.Id,
                    pc.Hostname,
                    pc.IPAddress,
                    msiFileName,
                    "Failed",
                    ex.Message,
                    DateTime.UtcNow));
            }
        }

        private static string BuildPsExecInstallArguments(string ip, string username, string password, string installerPath)
        {
            // psexec \\<IP> -u <USERNAME> -p <PASSWORD> msiexec /i "<path>" /quiet /norestart
            return $"\\\\{ip} -u \"{username}\" -p \"{password}\" msiexec /i \"{installerPath}\" /quiet /norestart";
        }

        private static string ToUncPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            if (path.StartsWith("\\\\", StringComparison.Ordinal))
            {
                return path;
            }

            // Convert local path like C:\folder\app.msi -> \\MACHINE\C$\folder\app.msi
            if (Path.IsPathRooted(path) && path.Length >= 3 && path[1] == ':' && path[2] == '\\')
            {
                var drive = char.ToUpperInvariant(path[0]);
                var tail = path[3..].Replace('\\', '\\');
                return $"\\\\{Environment.MachineName}\\{drive}$\\{tail}";
            }

            return path;
        }

        private string? ResolvePsExecExecutable()
        {
            // 1) Configured absolute/relative path
            if (!string.IsNullOrWhiteSpace(_psExecPath) &&
                !string.Equals(_psExecPath, "psexec.exe", StringComparison.OrdinalIgnoreCase) &&
                File.Exists(_psExecPath))
            {
                return _psExecPath;
            }

            // 2) PATH lookup
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                try
                {
                    var candidate = Path.Combine(dir, "psexec.exe");
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch
                {
                    // ignore malformed PATH entries
                }
            }

            // 3) Common fallback locations
            var commonCandidates = new[]
            {
                @"C:\Tools\PsExec\psexec.exe",
                @"C:\Sysinternals\psexec.exe",
                @"C:\Program Files\PsExec\psexec.exe"
            };

            return commonCandidates.FirstOrDefault(File.Exists);
        }

        private static string BuildErrorMessage(ProcessResult result)
        {
            var details = new StringBuilder();
            details.Append($"ExitCode={result.ExitCode}");
            if (!string.IsNullOrWhiteSpace(result.StandardError))
            {
                details.Append($" | STDERR: {result.StandardError.Trim()}");
            }
            if (!string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                details.Append($" | STDOUT: {result.StandardOutput.Trim()}");
            }
            return details.ToString();
        }

        private async Task<ProcessResult> RunProcessAsync(string fileName, string arguments, int timeoutSeconds)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            var stdOutTask = process.StandardOutput.ReadToEndAsync();
            var stdErrTask = process.StandardError.ReadToEndAsync();

            var waitTask = process.WaitForExitAsync();
            var completed = await Task.WhenAny(waitTask, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds)));

            if (completed != waitTask)
            {
                try { process.Kill(true); } catch { }
                return new ProcessResult(-1, await stdOutTask, "Process timed out.");
            }

            return new ProcessResult(process.ExitCode, await stdOutTask, await stdErrTask);
        }

        private async Task<string?> EnsureAgentHasMsiAsync(PCModel pc, string sourceUncPath, string msiFileName)
        {
            if (string.IsNullOrWhiteSpace(pc.IPAddress) || pc.IPAddress == "N/A")
            {
                return null;
            }

            var requestBody = JsonSerializer.Serialize(new AgentCacheRequest
            {
                SourceUncPath = sourceUncPath,
                LocalFileName = msiFileName
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, BuildAgentUri(pc.IPAddress, "/deployment/cache-msi"))
            {
                Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
            };

            AttachAuthHeader(request);

            try
            {
                using var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var dto = JsonSerializer.Deserialize<AgentCacheResponse>(json, JsonOptions());
                return dto?.LocalPath;
            }
            catch
            {
                return null;
            }
        }

        private async Task LoadLogsAsync()
        {
            try
            {
                var logs = await _deploymentDataService.GetRecentDeploymentLogsAsync(150);
                DeploymentLogs.Clear();
                foreach (var log in logs)
                {
                    DeploymentLogs.Add(new DeploymentLogModel
                    {
                        PCName = log.PCName,
                        IPAddress = log.IPAddress ?? string.Empty,
                        FileName = log.FileName,
                        Status = log.Status,
                        Details = log.Details ?? string.Empty,
                        Timestamp = log.Timestamp.ToLocalTime()
                    });
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to load logs: {ex.Message}";
            }
        }

        private void SelectAllPCs()
        {
            foreach (var pc in PCs)
            {
                pc.IsSelected = true;
            }
        }

        private void ClearSelection()
        {
            foreach (var pc in PCs)
            {
                pc.IsSelected = false;
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

            var hasBatchSelection = PCs.Any(p => p.IsSelected);
            var hasSingleTarget = SelectedFileManagementPC != null &&
                                  !string.IsNullOrWhiteSpace(SelectedFileManagementPC.IPAddress) &&
                                  SelectedFileManagementPC.IPAddress != "N/A";

            return hasBatchSelection || hasSingleTarget;
        }

        private async Task UploadFilesAsync()
        {
            var selectedPcs = PCs.Where(p => p.IsSelected).ToList();
            if (!selectedPcs.Any() && SelectedFileManagementPC != null)
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
            var requestUri = BuildAgentUri(
                pc.IPAddress,
                $"/files/upload?path={Uri.EscapeDataString(CurrentRemotePath)}&fileName={Uri.EscapeDataString(fileName)}");

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = new StreamContent(File.OpenRead(localPath))
            };

            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            AttachAuthHeader(request);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Upload failed for {pc.Hostname} ({response.StatusCode})");
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

                var requestUri = BuildAgentUri(
                    SelectedFileManagementPC.IPAddress,
                    $"/files/browse?path={Uri.EscapeDataString(CurrentRemotePath)}&search={Uri.EscapeDataString(RemoteSearchText ?? string.Empty)}");
                using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
                AttachAuthHeader(request);

                using var response = await _httpClient.SendAsync(request);
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

                StatusMessage = $"Showing {RemoteFiles.Count} item(s) at '{CurrentRemotePath}'.";
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

                var requestUri = BuildAgentUri(SelectedFileManagementPC.IPAddress, $"/files?path={Uri.EscapeDataString(item.FullPath)}");
                using var request = new HttpRequestMessage(HttpMethod.Delete, requestUri);
                AttachAuthHeader(request);

                using var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    StatusMessage = $"Delete failed: {response.StatusCode}";
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

                var requestUri = BuildAgentUri(SelectedFileManagementPC.IPAddress, $"/files/download?path={Uri.EscapeDataString(item.FullPath)}");
                using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
                AttachAuthHeader(request);

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode)
                {
                    StatusMessage = $"Download failed: {response.StatusCode}";
                    return;
                }

                await using var remoteStream = await response.Content.ReadAsStreamAsync();
                await using var localFile = File.Create(dialog.FileName);
                await remoteStream.CopyToAsync(localFile);

                StatusMessage = $"Downloaded '{item.Name}' to '{dialog.FileName}'.";
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

        private record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

        private sealed class AgentCacheRequest
        {
            public string SourceUncPath { get; set; } = string.Empty;
            public string LocalFileName { get; set; } = string.Empty;
        }

        private sealed class AgentCacheResponse
        {
            public string LocalPath { get; set; } = string.Empty;
            public bool AlreadyExists { get; set; }
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
