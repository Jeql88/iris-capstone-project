using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using IRIS.UI.Helpers;

namespace IRIS.UI.ViewModels
{
    public class PolicyEnforcementViewModel : INotifyPropertyChanged
    {
        private PolicyItem? _selectedPolicy;
        private bool _isEditMode;

        public ObservableCollection<PolicyItem> Policies { get; set; }
        public ObservableCollection<LabItem> Labs { get; set; }

        public PolicyItem? SelectedPolicy
        {
            get => _selectedPolicy;
            set
            {
                _selectedPolicy = value;
                OnPropertyChanged();
                IsEditMode = false;
            }
        }

        public bool IsEditMode
        {
            get => _isEditMode;
            set
            {
                _isEditMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsReadOnly));
            }
        }

        public bool IsReadOnly => !IsEditMode;

        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand DeployCommand { get; }
        public ICommand ClearSelectionCommand { get; }

        public PolicyEnforcementViewModel()
        {
            Policies = new ObservableCollection<PolicyItem>
            {
                new PolicyItem
                {
                    Name = "Wallpaper Reset on Start-up",
                    UpdatedAt = DateTime.Now.AddDays(-7),
                    ScriptContent = @"# SetWallpaper.ps1

# Change this to your desired default wallpaper path
$wallpaperPath = ""C:\Wallpapers\default.jpg""

# Use Windows API to change the wallpaper
Add-Type @""
using System.Runtime.InteropServices;
public class Wallpaper {
    [DllImport(""user32.dll"", SetLastError = true)]
    public static extern bool SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);
}
""@

# Set wallpaper (action 20), update INI file (1), and broadcast change (2)
[Wallpaper]::SystemParametersInfo(20, 0, $wallpaperPath, 3)"
                },
                new PolicyItem
                {
                    Name = "Access Control Enabled",
                    UpdatedAt = DateTime.Now.AddDays(-8).AddHours(-5),
                    ScriptContent = "# Access control script content here"
                },
                new PolicyItem
                {
                    Name = "Auto-Shutdown After 30 Minutes",
                    UpdatedAt = DateTime.Now.AddDays(-14),
                    ScriptContent = "# Auto-shutdown script content here"
                }
            };

            Labs = new ObservableCollection<LabItem>
            {
                new LabItem { Name = "Lab 1", OnlineCount = 18, TotalCount = 20, IsSelected = false },
                new LabItem { Name = "Lab 2", OnlineCount = 18, TotalCount = 20, IsSelected = false },
                new LabItem { Name = "Lab 3", OnlineCount = 18, TotalCount = 20, IsSelected = false },
                new LabItem { Name = "Lab 4", OnlineCount = 0, TotalCount = 20, IsSelected = false }
            };

            SelectedPolicy = Policies.FirstOrDefault();

            EditCommand = new RelayCommand(async () => { IsEditMode = true; await Task.CompletedTask; }, () => true);
            DeleteCommand = new RelayCommand(async () => { DeletePolicy(); await Task.CompletedTask; }, () => SelectedPolicy != null);
            DeployCommand = new RelayCommand(async () => { DeployPolicy(); await Task.CompletedTask; }, () => Labs.Any(l => l.IsSelected));
            ClearSelectionCommand = new RelayCommand(async () => { ClearLabSelection(); await Task.CompletedTask; }, () => true);
        }

        private void DeletePolicy()
        {
            if (SelectedPolicy != null)
            {
                Policies.Remove(SelectedPolicy);
                SelectedPolicy = Policies.FirstOrDefault();
            }
        }

        private void DeployPolicy()
        {
            // Deploy policy to selected labs
            var selectedLabs = Labs.Where(l => l.IsSelected).ToList();
            // Implementation here
        }

        private void ClearLabSelection()
        {
            foreach (var lab in Labs)
            {
                lab.IsSelected = false;
            }
        }

        public void SelectPolicy(PolicyItem? policy)
        {
            if (policy != null)
            {
                SelectedPolicy = policy;
            }
        }

        public void ToggleLab(LabItem? lab)
        {
            if (lab != null)
            {
                lab.IsSelected = !lab.IsSelected;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class PolicyItem : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private DateTime _updatedAt;
        private string _scriptContent = string.Empty;

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        public DateTime UpdatedAt
        {
            get => _updatedAt;
            set
            {
                _updatedAt = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UpdatedAtText));
            }
        }

        public string UpdatedAtText
        {
            get
            {
                var timeSpan = DateTime.Now - UpdatedAt;
                if (timeSpan.TotalDays >= 7)
                {
                    int weeks = (int)(timeSpan.TotalDays / 7);
                    return $"Updated {weeks} week{(weeks > 1 ? "s" : "")} ago";
                }
                else if (timeSpan.TotalDays >= 1)
                {
                    int days = (int)timeSpan.TotalDays;
                    return $"Updated {days} day{(days > 1 ? "s" : "")} ago";
                }
                else if (timeSpan.TotalHours >= 1)
                {
                    int hours = (int)timeSpan.TotalHours;
                    return $"Updated {hours} hour{(hours > 1 ? "s" : "")} ago";
                }
                return "Updated recently";
            }
        }

        public string ScriptContent
        {
            get => _scriptContent;
            set
            {
                _scriptContent = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class LabItem : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private int _onlineCount;
        private int _totalCount;
        private bool _isSelected;

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        public int OnlineCount
        {
            get => _onlineCount;
            set
            {
                _onlineCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
            }
        }

        public int TotalCount
        {
            get => _totalCount;
            set
            {
                _totalCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
            }
        }

        public string StatusText => $"{OnlineCount}/{TotalCount} Online";

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
