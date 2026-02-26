using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Threading;
using System.Collections.Generic;
using IRIS.Core.DTOs;
using IRIS.Core.Services.Contracts;
using IRIS.Core.Services.ServiceModels;
using IRIS.UI.Services;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace IRIS.UI.ViewModels
{
    public class DashboardViewModel : INotifyPropertyChanged, INavigationAware
    {
        private readonly IMonitoringService _monitoringService;
        private readonly DispatcherTimer _refreshTimer;
        private int? _selectedRoomId = null; // null means all rooms
        private readonly SemaphoreSlim _loadDataSemaphore = new(1, 1);
        private bool _isActive = true;

        public DashboardViewModel(IMonitoringService monitoringService)
        {
            _monitoringService = monitoringService;
            
            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _refreshTimer.Tick += async (s, e) => await RefreshDataAsync();
            _refreshTimer.Start();

            _ = InitializeAsync();
        }

        private RoomDto? _selectedRoom;
        public RoomDto? SelectedRoom
        {
            get => _selectedRoom;
            set
            {
                _selectedRoom = value;
                _selectedRoomId = value != null && value.Id > 0 ? value.Id : null;
                OnPropertyChanged();
                _ = LoadDataAsync();
            }
        }

        public ObservableCollection<RoomDto> Rooms { get; } = new();

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

        public PlotModel LatencyPlot { get; private set; } = new();
        public PlotModel BandwidthPlot { get; private set; } = new();
        public PlotModel PacketLossPlot { get; private set; } = new();

        private async Task InitializeAsync()
        {
            await LoadRoomsAsync();
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            if (!_isActive)
            {
                return;
            }

            if (!await _loadDataSemaphore.WaitAsync(0))
            {
                return;
            }

            try
            {
                if (!_isActive)
                {
                    return;
                }

                var summary = await _monitoringService.GetDashboardSummaryAsync(_selectedRoomId);
                AverageLatency = summary.AverageLatency;
                CurrentBandwidth = summary.CurrentBandwidth;
                PeakBandwidth = summary.PeakBandwidth;
                OnlinePCs = summary.OnlinePCs;
                TotalPCs = summary.TotalPCs;

                LabStatuses.Clear();
                foreach (var lab in summary.LabStatuses)
                    LabStatuses.Add(new LabStatus { Name = lab.Key, ActivePCs = lab.Value });

                HeavyApplications.Clear();
                foreach (var app in summary.HeavyApplications)
                    HeavyApplications.Add(new HeavyApp
                    {
                        Name = app.Name,
                        Icon = app.Icon,
                        Instances = app.InstanceCount,
                        RamUsage = app.AverageRamUsage
                    });

                var latency = await _monitoringService.GetLatencyHistoryAsync(24);
                LatencyData.Clear();
                foreach (var point in latency)
                    LatencyData.Add(new DataPoint { Time = point.Timestamp, Value = point.Value });
                LatencyPlot = BuildLinePlot("Latency (ms)", latency.Select(p => (p.Timestamp, p.Value)), "HH:mm", v => $"{v:F0} ms");
                OnPropertyChanged(nameof(LatencyPlot));

                var bandwidth = await _monitoringService.GetBandwidthHistoryAsync(24);
                BandwidthData.Clear();
                foreach (var point in bandwidth)
                    BandwidthData.Add(new DataPoint { Time = point.Timestamp, Value = point.Value });
                BandwidthPlot = BuildLinePlot("Bandwidth (Mbps)", bandwidth.Select(p => (p.Timestamp, p.Value)), "HH:mm", v => $"{v:F1} Mbps");
                OnPropertyChanged(nameof(BandwidthPlot));

                var packetLoss = await _monitoringService.GetPacketLossHistoryAsync(24);
                PacketLossData.Clear();
                foreach (var point in packetLoss)
                    PacketLossData.Add(new DataPoint { Time = point.Timestamp, Value = point.Value });
                PacketLossPlot = BuildLinePlot("Packet Loss (%)", packetLoss.Select(p => (p.Timestamp, p.Value)), "HH:mm", v => $"{v:F1}%");
                OnPropertyChanged(nameof(PacketLossPlot));

            }
            catch { }
            finally
            {
                _loadDataSemaphore.Release();
            }
        }

        private async Task RefreshDataAsync() => await LoadDataAsync();

        private async Task LoadRoomsAsync()
        {
            try
            {
                var rooms = await _monitoringService.GetRoomsAsync();
                Rooms.Clear();

                // All Labs option represented by null room id
                Rooms.Add(new RoomDto(-1, "All Labs", "", 0, true, DateTime.UtcNow));

                foreach (var room in rooms)
                {
                    Rooms.Add(room);
                }

                if (SelectedRoom == null && Rooms.Any())
                {
                    SelectedRoom = Rooms.First();
                }
            }
            catch { }
        }

        private PlotModel BuildLinePlot(string title, IEnumerable<(DateTime Timestamp, double Value)> points, string timeFormat, Func<double, string> valueFormatter)
        {
            var model = new PlotModel { Title = title, TitleFontSize = 14, TitlePadding = 4 };

            var timeAxis = new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = timeFormat,
                IntervalType = DateTimeIntervalType.Hours,
                MinorIntervalType = DateTimeIntervalType.Minutes,
                FontSize = 11,
                IsZoomEnabled = false,
                IsPanEnabled = false
            };

            var valueAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                LabelFormatter = valueFormatter,
                FontSize = 11,
                IsZoomEnabled = false,
                IsPanEnabled = false,
                MinimumPadding = 0.1,
                MaximumPadding = 0.1
            };

            model.Axes.Add(timeAxis);
            model.Axes.Add(valueAxis);

            var series = new LineSeries
            {
                Color = OxyColor.FromRgb(180, 40, 40),
                StrokeThickness = 2,
                MarkerType = MarkerType.None,
                LineJoin = LineJoin.Round,
                CanTrackerInterpolatePoints = false
            };

            foreach (var p in points.OrderBy(p => p.Timestamp))
            {
                series.Points.Add(new OxyPlot.DataPoint(DateTimeAxis.ToDouble(p.Timestamp), p.Value));
            }

            model.Series.Add(series);
            return model;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public void OnNavigatedFrom()
        {
            _isActive = false;
            _refreshTimer.Stop();
        }
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
