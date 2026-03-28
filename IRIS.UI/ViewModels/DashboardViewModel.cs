using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Input;
using System.Windows.Threading;
using System.Collections.Generic;
using IRIS.Core.DTOs;
using IRIS.Core.Services.Contracts;
using IRIS.Core.Services.ServiceModels;
using IRIS.UI.Services;
using IRIS.UI.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace IRIS.UI.ViewModels
{
    public class DashboardViewModel : INotifyPropertyChanged, INavigationAware
    {
        private readonly IPCDataCacheService _cache;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly DispatcherTimer _refreshTimer;
        private int? _selectedRoomId = null; // null means all rooms
        private readonly SemaphoreSlim _loadDataSemaphore = new(1, 1);
        private bool _isActive = true;
        private bool _isLoading;
        private DateTime _lastUpdatedUtc = DateTime.MinValue;
        private DateTime _startDate = DateTime.Now.AddHours(-24);
        private DateTime _endDate = DateTime.Now;
        private bool _useRolling24HourDefault = true;
        private string _selectedRangePreset = "Last 24h";

        public DashboardViewModel(IPCDataCacheService cache, IServiceScopeFactory scopeFactory)
        {
            _cache = cache;
            _scopeFactory = scopeFactory;
            ApplyDateFilterCommand = new RelayCommand(async () => await ApplyDateFilterAsync(), () => true);
            ExportNetworkAnalyticsCommand = new RelayCommand(async () => await ExportNetworkAnalyticsAsync(), () => true);
            ExportHardwareAnalyticsCommand = new RelayCommand(async () => await ExportHardwareAnalyticsAsync(), () => true);
            ExportSelectedCommand = new RelayCommand(async () => await ExportSelectedAsync(), () => true);

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _refreshTimer.Tick += async (s, e) => await RefreshDataAsync();

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
                OnPropertyChanged(nameof(ActiveRoomDescription));
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

        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        public string LastUpdatedText => _lastUpdatedUtc == DateTime.MinValue
            ? "Not yet updated"
            : $"Updated {DateTimeDisplayHelper.ToManilaFromUtc(_lastUpdatedUtc):HH:mm:ss}";

        public bool HasHeavyApplications => HeavyApplications.Count > 0;
        public bool HasLabStatuses => LabStatuses.Count > 0;

        public ObservableCollection<DataPoint> LatencyData { get; } = new();
        public ObservableCollection<DataPoint> BandwidthData { get; } = new();
        public ObservableCollection<DataPoint> PacketLossData { get; } = new();
        public ObservableCollection<HeavyApp> HeavyApplications { get; } = new();
        public ObservableCollection<LabStatus> LabStatuses { get; } = new();

        public DateTime StartDate
        {
            get => _startDate;
            set
            {
                if (_startDate == value)
                {
                    return;
                }

                _startDate = value;
                if (IsCustomRange)
                {
                    _useRolling24HourDefault = false;
                }
                OnPropertyChanged();
            }
        }

        public DateTime EndDate
        {
            get => _endDate;
            set
            {
                if (_endDate == value)
                {
                    return;
                }

                _endDate = value;
                if (IsCustomRange)
                {
                    _useRolling24HourDefault = false;
                }
                OnPropertyChanged();
            }
        }

        public string[] RangePresets { get; } = { "Last 24h", "Last 7d", "Custom" };

        public string SelectedRangePreset
        {
            get => _selectedRangePreset;
            set
            {
                if (string.Equals(_selectedRangePreset, value, StringComparison.Ordinal))
                {
                    return;
                }

                _selectedRangePreset = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsCustomRange));
                OnPropertyChanged(nameof(ActiveRangeDescription));
                // Only auto-apply preset if not already in custom mode
                if (!string.Equals(value, "Custom", StringComparison.OrdinalIgnoreCase))
                {
                    _ = ApplyPresetAsync();
                }
            }
        }

        public bool IsCustomRange => string.Equals(SelectedRangePreset, "Custom", StringComparison.OrdinalIgnoreCase);

        public string ActiveRangeDescription
        {
            get
            {
                if (string.Equals(SelectedRangePreset, "Last 24h", StringComparison.OrdinalIgnoreCase))
                {
                    return "Last 24 hours";
                }

                if (string.Equals(SelectedRangePreset, "Last 7d", StringComparison.OrdinalIgnoreCase))
                {
                    return "Last 7 days";
                }

                return $"{StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}";
            }
        }

        public string ActiveRoomDescription => _selectedRoomId.HasValue
            ? (SelectedRoom?.RoomNumber ?? "Selected room")
            : "All Labs";

        public ICommand ApplyDateFilterCommand { get; }
        public ICommand ExportNetworkAnalyticsCommand { get; }
        public ICommand ExportHardwareAnalyticsCommand { get; }
        public ICommand ExportSelectedCommand { get; }
        public PlotController HoverController { get; } = CreateHoverController();

        public string[] ExportOptions { get; } = { "Network Analytics", "Hardware Analytics" };

        private string _selectedExportOption = "Network Analytics";
        public string SelectedExportOption
        {
            get => _selectedExportOption;
            set { _selectedExportOption = value; OnPropertyChanged(); }
        }

        public PlotModel LatencyPlot { get; private set; } = new();
        public PlotModel BandwidthPlot { get; private set; } = new();
        public PlotModel PacketLossPlot { get; private set; } = new();

        private async Task InitializeAsync()
        {
            await LoadRoomsAsync();
            await ApplyPresetAsync();
            await LoadDataAsync();
            _refreshTimer.Start();
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

                IsLoading = true;

                // Force refresh from DB instead of using stale cache
                _cache.CurrentRoomFilter = _selectedRoomId;
                await _cache.RefreshDashboardSummaryAsync();
                await _cache.RefreshPCDataAsync();
                ApplyCachedSummary();

                // Load chart data with current date range — own scope for safe DB access
                try
                {
                    var (startUtc, endUtc) = GetRangeUtc();
                    var rangeSpan = endUtc - startUtc;
                    var timeFormat = rangeSpan.TotalHours <= 48 ? "HH:mm" : "MM/dd HH:mm";

                    using var scope = _scopeFactory.CreateScope();
                    var monitoringService = scope.ServiceProvider.GetRequiredService<IMonitoringService>();

                    var latency = await monitoringService.GetLatencyHistoryAsync(startUtc, endUtc, _selectedRoomId);
                    LatencyData.Clear();
                    foreach (var point in latency)
                        LatencyData.Add(new DataPoint { Time = point.Timestamp, Value = point.Value });
                    var latencyPoints = latency.Select(p => (p.Timestamp, p.Value));
                    LatencyPlot = BuildLinePlot("Latency (ms)", latencyPoints, timeFormat, v => $"{v:F0} ms", "{0}\nTime: {2:MMM dd HH:mm}\nLatency: {4:F0} ms");
                    OnPropertyChanged(nameof(LatencyPlot));

                    var bandwidth = await monitoringService.GetBandwidthHistoryAsync(startUtc, endUtc, _selectedRoomId);
                    BandwidthData.Clear();
                    foreach (var point in bandwidth)
                        BandwidthData.Add(new DataPoint { Time = point.Timestamp, Value = point.Value });
                    BandwidthPlot = BuildLinePlot("Bandwidth (Mbps)", bandwidth.Select(p => (p.Timestamp, p.Value)), timeFormat, v => $"{v:F1} Mbps", "{0}\nTime: {2:MMM dd HH:mm}\nBandwidth: {4:F1} Mbps");
                    OnPropertyChanged(nameof(BandwidthPlot));

                    var packetLoss = await monitoringService.GetPacketLossHistoryAsync(startUtc, endUtc, _selectedRoomId);
                    PacketLossData.Clear();
                    foreach (var point in packetLoss)
                        PacketLossData.Add(new DataPoint { Time = point.Timestamp, Value = point.Value });
                    PacketLossPlot = BuildLinePlot("Packet Loss (%)", packetLoss.Select(p => (p.Timestamp, p.Value)), timeFormat, v => $"{v:F1}%", "{0}\nTime: {2:MMM dd HH:mm}\nPacket Loss: {4:F1}%");
                    OnPropertyChanged(nameof(PacketLossPlot));
                }
                catch { /* chart load non-critical */ }

                _lastUpdatedUtc = DateTime.UtcNow;
                OnPropertyChanged(nameof(LastUpdatedText));
            }
            catch { }
            finally
            {
                IsLoading = false;
                _loadDataSemaphore.Release();
            }
        }

        private async Task RefreshDataAsync() => await LoadDataAsync();

        private async Task ApplyDateFilterAsync()
        {
            _useRolling24HourDefault = false;
            _selectedRangePreset = "Custom";
            OnPropertyChanged(nameof(SelectedRangePreset));
            OnPropertyChanged(nameof(IsCustomRange));
            OnPropertyChanged(nameof(ActiveRangeDescription));
            
            // Apply room filter
            _cache.CurrentRoomFilter = _selectedRoomId;
            
            await LoadDataAsync();
        }

        private void ApplyCachedSummary()
        {
            var summary = _cache.CachedDashboardSummary;
            if (summary == null) return;

            AverageLatency = summary.AverageLatency;
            CurrentBandwidth = summary.CurrentBandwidth;
            PeakBandwidth = summary.PeakBandwidth;
            OnlinePCs = summary.OnlinePCs;
            TotalPCs = summary.TotalPCs;

            LabStatuses.Clear();
            foreach (var lab in summary.LabStatuses)
                LabStatuses.Add(new LabStatus { Name = lab.Key, ActivePCs = lab.Value });
            OnPropertyChanged(nameof(HasLabStatuses));

            HeavyApplications.Clear();
            foreach (var app in summary.HeavyApplications)
                HeavyApplications.Add(new HeavyApp
                {
                    Name = app.Name,
                    Icon = app.Icon,
                    Instances = app.InstanceCount,
                    RamUsage = app.AverageRamUsage
                });
            OnPropertyChanged(nameof(HasHeavyApplications));
        }

        private async Task ApplyPresetAsync()
        {
            if (string.Equals(SelectedRangePreset, "Last 24h", StringComparison.OrdinalIgnoreCase))
            {
                _useRolling24HourDefault = true;
                var now = DateTime.Now;
                _startDate = now.AddHours(-24);
                _endDate = now;
                OnPropertyChanged(nameof(StartDate));
                OnPropertyChanged(nameof(EndDate));
                OnPropertyChanged(nameof(ActiveRangeDescription));
            }
            else if (string.Equals(SelectedRangePreset, "Last 7d", StringComparison.OrdinalIgnoreCase))
            {
                _useRolling24HourDefault = false;
                var now = DateTime.Now;
                _startDate = now.Date.AddDays(-7);
                _endDate = now;
                OnPropertyChanged(nameof(StartDate));
                OnPropertyChanged(nameof(EndDate));
                OnPropertyChanged(nameof(ActiveRangeDescription));
            }
            else
            {
                _useRolling24HourDefault = false;
                OnPropertyChanged(nameof(ActiveRangeDescription));
            }

            OnPropertyChanged(nameof(IsCustomRange));
            if (_isActive)
            {
                await LoadDataAsync();
            }
        }

        private async Task ExportNetworkAnalyticsAsync()
        {
            var (startUtc, endUtc) = GetRangeUtc();
            using var scope = _scopeFactory.CreateScope();
            var monitoringService = scope.ServiceProvider.GetRequiredService<IMonitoringService>();
            var bytes = await monitoringService.ExportNetworkAnalyticsCsvAsync(startUtc, endUtc, _selectedRoomId);

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                DefaultExt = "csv",
                FileName = $"NetworkAnalytics_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                await System.IO.File.WriteAllBytesAsync(saveFileDialog.FileName, bytes);
            }
        }

        private async Task ExportHardwareAnalyticsAsync()
        {
            var (startUtc, endUtc) = GetRangeUtc();
            using var scope = _scopeFactory.CreateScope();
            var monitoringService = scope.ServiceProvider.GetRequiredService<IMonitoringService>();
            var bytes = await monitoringService.ExportHardwareAnalyticsCsvAsync(startUtc, endUtc, _selectedRoomId);

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                DefaultExt = "csv",
                FileName = $"HardwareAnalytics_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                await System.IO.File.WriteAllBytesAsync(saveFileDialog.FileName, bytes);
            }
        }

        private async Task ExportSelectedAsync()
        {
            if (SelectedExportOption == "Hardware Analytics")
                await ExportHardwareAnalyticsAsync();
            else
                await ExportNetworkAnalyticsAsync();
        }

        private async Task LoadRoomsAsync()
        {
            try
            {
                await _cache.RefreshRoomsAsync();
                var rooms = _cache.CachedRooms;
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

        private (DateTime startUtc, DateTime endUtc) GetRangeUtc()
        {
            if (_useRolling24HourDefault)
            {
                var endUtc = DateTime.UtcNow;
                return (endUtc.AddHours(-24), endUtc);
            }

            var startLocal = StartDate.Date;
            var endLocal = EndDate.Date.AddDays(1).AddTicks(-1);
            if (endLocal < startLocal)
            {
                endLocal = startLocal.AddDays(1).AddTicks(-1);
            }

            var startUtc = DateTime.SpecifyKind(startLocal, DateTimeKind.Local).ToUniversalTime();
            var endUtcConverted = DateTime.SpecifyKind(endLocal, DateTimeKind.Local).ToUniversalTime();
            return (startUtc, endUtcConverted);
        }

        private PlotModel BuildLinePlot(string title, IEnumerable<(DateTime Timestamp, double Value)> points, string timeFormat, Func<double, string> valueFormatter, string trackerFormat)
        {
            var model = new PlotModel { Title = title, TitleFontSize = 14, TitlePadding = 4 };

            var timeAxis = new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = timeFormat,
                IntervalType = DateTimeIntervalType.Hours,
                MinorIntervalType = DateTimeIntervalType.Minutes,
                FontSize = 11,
                IsZoomEnabled = true,
                IsPanEnabled = true
            };

            var valueAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                LabelFormatter = valueFormatter,
                FontSize = 11,
                IsZoomEnabled = true,
                IsPanEnabled = true,
                MinimumPadding = 0.1,
                MaximumPadding = 0.1
            };

            model.Axes.Add(timeAxis);
            model.Axes.Add(valueAxis);

            // Convert UTC timestamps to Manila time for display
            var localPoints = points.Select(p => (Timestamp: DateTimeDisplayHelper.ToManilaFromUtc(p.Timestamp), p.Value)).OrderBy(p => p.Timestamp).ToList();

            var series = new LineSeries
            {
                Color = OxyColor.FromRgb(180, 40, 40),
                StrokeThickness = 2,
                MarkerType = MarkerType.None,
                LineJoin = LineJoin.Round,
                CanTrackerInterpolatePoints = true,
                TrackerFormatString = trackerFormat
            };

            foreach (var p in localPoints)
            {
                series.Points.Add(new OxyPlot.DataPoint(DateTimeAxis.ToDouble(p.Timestamp), p.Value));
            }

            model.Series.Add(series);
            return model;
        }

        private static PlotController CreateHoverController()
        {
            var controller = new PlotController();
            controller.UnbindMouseDown(OxyMouseButton.Left);
            controller.BindMouseEnter(PlotCommands.HoverSnapTrack);
            return controller;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public void OnNavigatedTo()
        {
            _isActive = true;
            _ = LoadDataAsync();
            _refreshTimer.Start();
        }

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
