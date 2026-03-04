using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace IRIS.UI.Models
{
    public class PCModel : INotifyPropertyChanged
    {
        private bool _isSelected;
        private double _deploymentProgress;
        private string _deploymentStatus = "Idle";

        public int Id { get; set; }
        public string Hostname { get; set; } = string.Empty;
        public string IPAddress { get; set; } = string.Empty;
        public string Status { get; set; } = "Offline";
        public string RoomNumber { get; set; } = string.Empty;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public double DeploymentProgress
        {
            get => _deploymentProgress;
            set
            {
                if (Math.Abs(_deploymentProgress - value) < 0.1) return;
                _deploymentProgress = value;
                OnPropertyChanged();
            }
        }

        public string DeploymentStatus
        {
            get => _deploymentStatus;
            set
            {
                if (_deploymentStatus == value) return;
                _deploymentStatus = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
