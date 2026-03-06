using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using IRIS.Core.Services.Contracts;
using IRIS.UI.Helpers;
using IRIS.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace IRIS.UI.ViewModels
{
    /// <summary>
    /// Parameter object passed from the Dashboard when navigating to the analytics page.
    /// </summary>
    public class NetworkAnalyticsParameter
    {
        public string ChartType { get; set; } = "Latency";   // "Latency", "Bandwidth", "PacketLoss"
        public int? RoomId { get; set; }
        public string RoomDescription { get; set; } = "All Labs";
        public string RangeDescription { get; set; } = "Last 24 hours";
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
    }

    public class NetworkAnalyticsViewModel : INotifyPropertyChanged
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private bool _isLoading;
        private string _title = "Network Analytics";
        private string _subtitle = string.Empty;
        private PlotModel _chartModel = new();

        // Pagination
        private int _pageSize = 25;
        private List<NetworkStatRow> _allRows = new();
        private int _currentPage = 1;
        private int _totalPages = 1;

        public NetworkAnalyticsViewModel(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
            NextPageCommand = new RelayCommand(() => GoToPage(_currentPage + 1), () => _currentPage < _totalPages);
            PreviousPageCommand = new RelayCommand(() => GoToPage(_currentPage - 1), () => _currentPage > 1);
            FirstPageCommand = new RelayCommand(() => GoToPage(1), () => _currentPage > 1);
            LastPageCommand = new RelayCommand(() => GoToPage(_totalPages), () => _currentPage < _totalPages);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); }
        }

        public string Subtitle
        {
            get => _subtitle;
            set { _subtitle = value; OnPropertyChanged(); }
        }

        public PlotModel ChartModel
        {
            get => _chartModel;
            set { _chartModel = value; OnPropertyChanged(); }
        }

        public ObservableCollection<NetworkStatRow> PagedData { get; } = new();
        public int[] PageSizeOptions { get; } = { 10, 25, 50, 100 };

        public int PageSize
        {
            get => _pageSize;
            set
            {
                if (_pageSize != value)
                {
                    _pageSize = value;
                    OnPropertyChanged();
                    GoToPage(1);
                }
            }
        }

        public int CurrentPage
        {
            get => _currentPage;
            set { _currentPage = value; OnPropertyChanged(); OnPropertyChanged(nameof(PageInfo)); }
        }

        public int TotalPages
        {
            get => _totalPages;
            set { _totalPages = value; OnPropertyChanged(); OnPropertyChanged(nameof(PageInfo)); }
        }

        public int TotalRecords => _allRows.Count;

        public string PageInfo => $"Page {CurrentPage} of {TotalPages}  ({TotalRecords} records)";

        public ICommand NextPageCommand { get; }
        public ICommand PreviousPageCommand { get; }
        public ICommand FirstPageCommand { get; }
        public ICommand LastPageCommand { get; }

        public async Task LoadAsync(NetworkAnalyticsParameter param)
        {
            IsLoading = true;

            try
            {
                Title = param.ChartType switch
                {
                    "Latency" => "Latency — Detailed View",
                    "Bandwidth" => "Bandwidth — Detailed View",
                    "PacketLoss" => "Packet Loss — Detailed View",
                    _ => "Network Analytics"
                };

                Subtitle = $"{param.RoomDescription}  •  {param.RangeDescription}";

                using var scope = _scopeFactory.CreateScope();
                var monSvc = scope.ServiceProvider.GetRequiredService<IMonitoringService>();

                var rangeSpan = param.EndUtc - param.StartUtc;
                var timeFormat = rangeSpan.TotalHours <= 48 ? "HH:mm" : "MM/dd HH:mm";

                _allRows.Clear();

                switch (param.ChartType)
                {
                    case "Latency":
                        var latency = await monSvc.GetLatencyHistoryAsync(param.StartUtc, param.EndUtc, param.RoomId);
                        ChartModel = BuildDetailedPlot("Latency (ms)", latency.Select(p => (p.Timestamp, p.Value)), timeFormat, v => $"{v:F0} ms");
                        foreach (var p in latency.OrderByDescending(x => x.Timestamp))
                            _allRows.Add(new NetworkStatRow { Timestamp = p.Timestamp.ToLocalTime(), Metric = "Latency", Value = $"{p.Value:F0} ms", NumericValue = p.Value });
                        break;

                    case "Bandwidth":
                        var bw = await monSvc.GetBandwidthHistoryAsync(param.StartUtc, param.EndUtc, param.RoomId);
                        ChartModel = BuildDetailedPlot("Bandwidth (Mbps)", bw.Select(p => (p.Timestamp, p.Value)), timeFormat, v => $"{v:F1} Mbps");
                        foreach (var p in bw.OrderByDescending(x => x.Timestamp))
                            _allRows.Add(new NetworkStatRow { Timestamp = p.Timestamp.ToLocalTime(), Metric = "Bandwidth", Value = $"{p.Value:F1} Mbps", NumericValue = p.Value });
                        break;

                    case "PacketLoss":
                        var pl = await monSvc.GetPacketLossHistoryAsync(param.StartUtc, param.EndUtc, param.RoomId);
                        ChartModel = BuildDetailedPlot("Packet Loss (%)", pl.Select(p => (p.Timestamp, p.Value)), timeFormat, v => $"{v:F1}%");
                        foreach (var p in pl.OrderByDescending(x => x.Timestamp))
                            _allRows.Add(new NetworkStatRow { Timestamp = p.Timestamp.ToLocalTime(), Metric = "Packet Loss", Value = $"{p.Value:F1}%", NumericValue = p.Value });
                        break;
                }

                OnPropertyChanged(nameof(ChartModel));
                OnPropertyChanged(nameof(TotalRecords));
                GoToPage(1);
            }
            catch
            {
                // non-critical
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void GoToPage(int page)
        {
            TotalPages = Math.Max(1, (int)Math.Ceiling(_allRows.Count / (double)_pageSize));
            CurrentPage = Math.Clamp(page, 1, TotalPages);

            PagedData.Clear();
            foreach (var row in _allRows.Skip((CurrentPage - 1) * _pageSize).Take(_pageSize))
                PagedData.Add(row);

            OnPropertyChanged(nameof(PageInfo));
            (NextPageCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (PreviousPageCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (FirstPageCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (LastPageCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private static PlotModel BuildDetailedPlot(string title, IEnumerable<(DateTime Timestamp, double Value)> points, string timeFormat, Func<double, string> valueFormatter)
        {
            var model = new PlotModel
            {
                Title = title,
                TitleFontSize = 16,
                TitlePadding = 6,
                Subtitle = "Scroll to zoom  •  Drag to pan  •  Hover for exact values"
            };

            // Convert UTC timestamps to local time for display
            var localPoints = points.Select(p => (Timestamp: p.Timestamp.ToLocalTime(), p.Value)).OrderBy(p => p.Timestamp).ToList();

            var timeAxis = new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = timeFormat,
                IntervalType = DateTimeIntervalType.Auto,
                MinorIntervalType = DateTimeIntervalType.Auto,
                FontSize = 12,
                IsZoomEnabled = true,
                IsPanEnabled = true,
                Title = "Time"
            };

            var valueAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                LabelFormatter = valueFormatter,
                FontSize = 12,
                IsZoomEnabled = true,
                IsPanEnabled = true,
                MinimumPadding = 0.1,
                MaximumPadding = 0.1
            };

            model.Axes.Add(timeAxis);
            model.Axes.Add(valueAxis);

            var series = new LineSeries
            {
                Title = title,
                Color = OxyColor.FromRgb(180, 40, 40),
                StrokeThickness = 2,
                MarkerType = MarkerType.Circle,
                MarkerSize = 3,
                MarkerStroke = OxyColor.FromRgb(180, 40, 40),
                MarkerFill = OxyColors.White,
                LineJoin = LineJoin.Round,
                CanTrackerInterpolatePoints = false,
                TrackerFormatString = "{0}\nTime: {2:yyyy-MM-dd HH:mm:ss}\nValue: {4:0.###}"
            };

            foreach (var p in localPoints)
            {
                series.Points.Add(new OxyPlot.DataPoint(DateTimeAxis.ToDouble(p.Timestamp), p.Value));
            }

            model.Series.Add(series);
            return model;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class NetworkStatRow
    {
        public DateTime Timestamp { get; set; }
        public string Metric { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public double NumericValue { get; set; }
    }
}
