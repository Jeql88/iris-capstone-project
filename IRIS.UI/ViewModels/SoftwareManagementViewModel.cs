using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using IRIS.UI.Helpers;

namespace IRIS.UI.ViewModels
{
    public class SoftwareManagementViewModel : INotifyPropertyChanged
    {
        private bool _isDeployMode = true;
        private string _destinationPath = @"C:\Users\user\IRIS\Faculty-Uploads";
        private string _selectedLab = "Lab 1";
        private SoftwareItemViewModel? _selectedSoftwareForDeployment;
        private SoftwareItemViewModel? _selectedApplicationToUninstall;
        private string _selectedInventoryLab = "Archi Lab 1";

        public SoftwareManagementViewModel()
        {
            DeploySoftwareCommand = new RelayCommand(async () => await DeploySoftwareAsync(), CanDeploySoftware);
            UninstallSoftwareCommand = new RelayCommand(async () => await UninstallSoftwareAsync(), CanUninstallSoftware);
            BrowseFilesCommand = new RelayCommand(async () => await BrowseFilesAsync(), () => true);
            ApproveRequestCommand = new RelayCommand(async () => await ApproveRequestAsync(), () => true);
            RejectRequestCommand = new RelayCommand(async () => await RejectRequestAsync(), () => true);
            SelectAllOnlineCommand = new RelayCommand(async () => await SelectAllOnlineAsync(), () => true);
            ClearSelectionCommand = new RelayCommand(async () => await ClearSelectionAsync(), () => true);

            InitializeData();
        }

        public bool IsDeployMode
        {
            get => _isDeployMode;
            set
            {
                _isDeployMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsUninstallMode));
            }
        }

        public bool IsUninstallMode => !_isDeployMode;

        public string DestinationPath
        {
            get => _destinationPath;
            set
            {
                _destinationPath = value;
                OnPropertyChanged();
            }
        }

        public string SelectedLab
        {
            get => _selectedLab;
            set
            {
                _selectedLab = value;
                OnPropertyChanged();
                UpdatePCSelection();
            }
        }

        public string SelectedInventoryLab
        {
            get => _selectedInventoryLab;
            set
            {
                _selectedInventoryLab = value;
                OnPropertyChanged();
                LoadInventoryForLab(value);
            }
        }

        public SoftwareItemViewModel? SelectedSoftwareForDeployment
        {
            get => _selectedSoftwareForDeployment;
            set
            {
                _selectedSoftwareForDeployment = value;
                OnPropertyChanged();
                (DeploySoftwareCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public SoftwareItemViewModel? SelectedApplicationToUninstall
        {
            get => _selectedApplicationToUninstall;
            set
            {
                _selectedApplicationToUninstall = value;
                OnPropertyChanged();
                (UninstallSoftwareCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public ObservableCollection<string> UploadedFiles { get; } = new();
        public ObservableCollection<LabViewModel> Labs { get; } = new();
        public ObservableCollection<PCViewModel> PCs { get; } = new();
        public ObservableCollection<SoftwareItemViewModel> SoftwareInventory { get; } = new();
        public ObservableCollection<DeploymentViewModel> ActiveDeployments { get; } = new();
        public ObservableCollection<SoftwareRequestViewModel> SoftwareRequests { get; } = new();
        public ObservableCollection<SoftwareItemViewModel> ApplicationsToUninstall { get; } = new();

        public ICommand DeploySoftwareCommand { get; }
        public ICommand UninstallSoftwareCommand { get; }
        public ICommand BrowseFilesCommand { get; }
        public ICommand ApproveRequestCommand { get; }
        public ICommand RejectRequestCommand { get; }
        public ICommand SelectAllOnlineCommand { get; }
        public ICommand ClearSelectionCommand { get; }

        private void InitializeData()
        {
            // Initialize Labs
            Labs.Add(new LabViewModel { Name = "Lab 1", OnlineCount = 18, TotalCount = 20, IsOnline = true });
            Labs.Add(new LabViewModel { Name = "Lab 2", OnlineCount = 18, TotalCount = 20, IsOnline = true });
            Labs.Add(new LabViewModel { Name = "Lab 3", OnlineCount = 18, TotalCount = 20, IsOnline = true });
            Labs.Add(new LabViewModel { Name = "Lab 4", OnlineCount = 0, TotalCount = 20, IsOnline = false });

            // Initialize PCs for Lab 1
            UpdatePCSelection();

            // Initialize Software Inventory
            SoftwareInventory.Add(new SoftwareItemViewModel { Icon = "Ps", Name = "Adobe Photoshop", Version = "Version 2024 • 3.2 GB", InstallCount = "18/20" });
            SoftwareInventory.Add(new SoftwareItemViewModel { Icon = "Vs", Name = "Visual Studio Code", Version = "Version 2022 • 2.1 GB", InstallCount = "20/20" });
            SoftwareInventory.Add(new SoftwareItemViewModel { Icon = "Ch", Name = "Google Chrome", Version = "Version 121.0 • 1.8 GB", InstallCount = "20/20" });
            SoftwareInventory.Add(new SoftwareItemViewModel { Icon = "Bl", Name = "Blender", Version = "Version 4.0 • 4.1 GB", InstallCount = "12/20" });

            // Initialize Applications to Uninstall
            ApplicationsToUninstall.Add(new SoftwareItemViewModel { Icon = "Ps", Name = "Adobe Photoshop", Version = "Version 2024 • 3.2 GB" });
            ApplicationsToUninstall.Add(new SoftwareItemViewModel { Icon = "Vs", Name = "Visual Studio Code", Version = "Version 2022 • 2.1 GB" });
            ApplicationsToUninstall.Add(new SoftwareItemViewModel { Icon = "Ch", Name = "Google Chrome", Version = "Version 121.0 • 1.8 GB" });
            ApplicationsToUninstall.Add(new SoftwareItemViewModel { Icon = "Bl", Name = "Blender", Version = "Version 4.0 • 4.1 GB" });

            // Initialize Active Deployments
            ActiveDeployments.Add(new DeploymentViewModel { Name = "Visual Studio Code", Progress = 67, Status = "Installing to 5 PCs • 67% complete" });
            ActiveDeployments.Add(new DeploymentViewModel { Name = "Figma Desktop", Progress = 100, Status = "Uploaded 20 PCs • Completed 5 min ago" });

            // Initialize Software Requests
            SoftwareRequests.Add(new SoftwareRequestViewModel { SoftwareName = "Request for Adobe Photoshop", Requester = "Request made by Godwin Monserate" });
            SoftwareRequests.Add(new SoftwareRequestViewModel { SoftwareName = "Request for Visual Studio", Requester = "Request made by Godwin Monserate" });
            SoftwareRequests.Add(new SoftwareRequestViewModel { SoftwareName = "Request for Adobe Photoshop", Requester = "Request made by Godwin Monserate" });
            SoftwareRequests.Add(new SoftwareRequestViewModel { SoftwareName = "Request for Visual Studio", Requester = "Request made by Godwin Monserate" });
            SoftwareRequests.Add(new SoftwareRequestViewModel { SoftwareName = "Request for Adobe Photoshop", Requester = "Request made by Godwin Monserate" });
        }

        private void UpdatePCSelection()
        {
            PCs.Clear();
            var labNumber = SelectedLab.Replace("Lab ", "");
            
            for (int i = 1; i <= 20; i++)
            {
                var isOnline = labNumber != "4" && (i <= 3 || i == 6 || i == 7 || i == 8 || i == 10 || i == 11 || i == 12 || i == 13 || i == 15 || i == 16 || i == 17 || i == 20);
                var isSelected = labNumber != "4" && (i == 3 || i == 6 || i == 7 || i == 8 || i == 11 || i == 12 || i == 13 || i == 15 || i == 16 || i == 17 || i == 20);
                
                PCs.Add(new PCViewModel 
                { 
                    Name = $"PC {i:D2}", 
                    IsOnline = isOnline,
                    IsSelected = isSelected
                });
            }
        }

        private void LoadInventoryForLab(string labName)
        {
            // In a real implementation, this would load inventory specific to the selected lab
            // For now, we'll keep the same inventory
        }

        private bool CanDeploySoftware()
        {
            return UploadedFiles.Count > 0 && PCs.Any(pc => pc.IsSelected);
        }

        private async Task DeploySoftwareAsync()
        {
            try
            {
                var selectedPCs = PCs.Where(pc => pc.IsSelected).ToList();
                MessageBox.Show($"Deploying {UploadedFiles.Count} file(s) to {selectedPCs.Count} PC(s)...", "Deploy Software", MessageBoxButton.OK, MessageBoxImage.Information);
                
                // In a real implementation, this would trigger actual deployment
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Deployment failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanUninstallSoftware()
        {
            return SelectedApplicationToUninstall != null && PCs.Any(pc => pc.IsSelected);
        }

        private async Task UninstallSoftwareAsync()
        {
            try
            {
                var selectedPCs = PCs.Where(pc => pc.IsSelected).ToList();
                MessageBox.Show($"Uninstalling {SelectedApplicationToUninstall?.Name} from {selectedPCs.Count} PC(s)...", "Uninstall Software", MessageBoxButton.OK, MessageBoxImage.Information);
                
                // In a real implementation, this would trigger actual uninstallation
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Uninstallation failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task BrowseFilesAsync()
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Multiselect = true,
                    Filter = "All Files (*.*)|*.*"
                };

                if (dialog.ShowDialog() == true)
                {
                    foreach (var file in dialog.FileNames)
                    {
                        var fileName = System.IO.Path.GetFileName(file);
                        if (!UploadedFiles.Contains(fileName))
                        {
                            UploadedFiles.Add(fileName);
                        }
                    }
                    (DeploySoftwareCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"File selection failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ApproveRequestAsync()
        {
            await Task.CompletedTask;
            MessageBox.Show("Request approved", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task RejectRequestAsync()
        {
            await Task.CompletedTask;
            MessageBox.Show("Request rejected", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task SelectAllOnlineAsync()
        {
            foreach (var pc in PCs.Where(p => p.IsOnline))
            {
                pc.IsSelected = true;
            }
            await Task.CompletedTask;
        }

        private async Task ClearSelectionAsync()
        {
            foreach (var pc in PCs)
            {
                pc.IsSelected = false;
            }
            await Task.CompletedTask;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class LabViewModel : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private int _onlineCount;
        private int _totalCount;
        private bool _isOnline;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public int OnlineCount
        {
            get => _onlineCount;
            set { _onlineCount = value; OnPropertyChanged(); }
        }

        public int TotalCount
        {
            get => _totalCount;
            set { _totalCount = value; OnPropertyChanged(); }
        }

        public bool IsOnline
        {
            get => _isOnline;
            set { _isOnline = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class PCViewModel : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private bool _isOnline;
        private bool _isSelected;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public bool IsOnline
        {
            get => _isOnline;
            set { _isOnline = value; OnPropertyChanged(); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class SoftwareItemViewModel : INotifyPropertyChanged
    {
        private string _icon = string.Empty;
        private string _name = string.Empty;
        private string _version = string.Empty;
        private string _installCount = string.Empty;

        public string Icon
        {
            get => _icon;
            set { _icon = value; OnPropertyChanged(); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Version
        {
            get => _version;
            set { _version = value; OnPropertyChanged(); }
        }

        public string InstallCount
        {
            get => _installCount;
            set { _installCount = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class DeploymentViewModel : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private int _progress;
        private string _status = string.Empty;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public int Progress
        {
            get => _progress;
            set { _progress = value; OnPropertyChanged(); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class SoftwareRequestViewModel : INotifyPropertyChanged
    {
        private string _softwareName = string.Empty;
        private string _requester = string.Empty;

        public string SoftwareName
        {
            get => _softwareName;
            set { _softwareName = value; OnPropertyChanged(); }
        }

        public string Requester
        {
            get => _requester;
            set { _requester = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
