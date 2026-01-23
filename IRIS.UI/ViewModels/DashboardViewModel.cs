using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using IRIS.Core.Services;
using IRIS.Core.Services.ServiceModels;

namespace IRIS.UI.ViewModels
{
    public class DashboardViewModel : INotifyPropertyChanged
    {
        private readonly IMonitoringService _monitoringService;
        private readonly DispatcherTimer _refreshTimer;
        private int? _selectedRoomId;

        public DashboardViewModel(IMonitoringService monitoringService)
        {
            _monitoringService = monitoringService;
            
            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _refreshTimer.Tick += async (s, e) => await RefreshDataAsync();
            _refreshTimer.Start();

            _ = LoadDataAsync();
        }

        private string _selectedLab = "Archi Lab 1";
        public string SelectedLab
        {
            get => _selectedLab;
            set { _selectedLab = value; OnPropertyChanged(); _ = LoadDataAsync(); }
        }

        private double _averageLatency;
        public double AverageLatency
        {
            get => _averageLatency;
            set { _averageLatency = value; OnPropertyChanged(); }
        }

        private double _currentBandwidth;
        public double CurrentBandwidth
        {
            get => _currentBandwidth;
            set { _currentBandwidth = value; OnPropertyChanged(); }
        }

        private double _peakBandwidth;
        public double PeakBandwidth
        {
            get => _peakBandwidth;
            set { _peakBandwidth = value; OnPropertyChanged(); }
        }

        private int _onlinePCs;
        public int OnlinePCs
        {
            get => _onlinePCs;
            set { _onlinePCs = value; OnPropertyChanged(); }
        }

        private int _totalPCs;
        public int TotalPCs
        {
            get => _totalPCs;
            set { _totalPCs = value; OnPropertyChanged(); }
        }

        public ObservableCollection<DataPoint> LatencyData { get; } = new();
        public ObservableCollection<DataPoint> BandwidthData { get; } = new();
        public ObservableCollection<DataPoint> PacketLossData { get; } = new();
        public ObservableCollection<HeavyApp> HeavyApplications { get; } = new();
        public ObservableCollection<LabStatus> LabStatuses { get; } = new();

        private async Task LoadDataAsync()
        {
            try
            {
                var metrics = await _monitoringService.GetDashboardMetricsAsync(_selectedRoomId);
                AverageLatency = metrics.AverageLatency;
                CurrentBandwidth = metrics.CurrentBandwidth;
                PeakBandwidth = metrics.PeakBandwidth;
                OnlinePCs = metrics.OnlinePCs;
                TotalPCs = metrics.TotalPCs;

                var latency = await _monitoringService.GetLatencyHistoryAsync(24);
                LatencyData.Clear();
                foreach (var point in latency)
                    LatencyData.Add(new DataPoint { Time = point.Timestamp, Value = point.Value });

                var bandwidth = await _monitoringService.GetBandwidthHistoryAsync(24);
                BandwidthData.Clear();
                foreach (var point in bandwidth)
                    BandwidthData.Add(new DataPoint { Time = point.Timestamp, Value = point.Value });

                var packetLoss = await _monitoringService.GetPacketLossHistoryAsync(24);
                PacketLossData.Clear();
                foreach (var point in packetLoss)
                    PacketLossData.Add(new DataPoint { Time = point.Timestamp, Value = point.Value });

                var apps = await _monitoringService.GetHeavyApplicationsAsync(_selectedRoomId);
                HeavyApplications.Clear();
                foreach (var app in apps)
                    HeavyApplications.Add(new HeavyApp
                    {
                        Name = app.Name,
                        Icon = app.Icon,
                        Instances = app.InstanceCount,
                        RamUsage = app.AverageRamUsage
                    });

                var labs = await _monitoringService.GetActiveLabPCsAsync();
                LabStatuses.Clear();
                foreach (var lab in labs)
                    LabStatuses.Add(new LabStatus { Name = lab.Key, ActivePCs = lab.Value });
            }
            catch { }
        }

        private async Task RefreshDataAsync() => await LoadDataAsync();

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class DataPoint
    {
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }

    public class HeavyApp
    {
        public string Name { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public int Instances { get; set; }
        public double RamUsage { get; set; }
    }

    public class LabStatus
    {
        public string Name { get; set; } = string.Empty;
        public int ActivePCs { get; set; }
    }
}
