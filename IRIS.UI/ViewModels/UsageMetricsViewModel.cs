using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using IRIS.Core.Services.Contracts;
using IRIS.UI.Helpers;

namespace IRIS.UI.ViewModels
{
    public class UsageMetricsViewModel : INotifyPropertyChanged
    {
        private readonly IUsageMetricsService _usageMetricsService;
        private DateTime _startDate = DateTime.UtcNow.Date.AddDays(-7);
        private DateTime _endDate = DateTime.UtcNow.Date;
        private int _totalApplications;
        private int _totalWebsites;
        private double _totalHours;
        private bool _isLoading;
        private string _appSearchText = string.Empty;
        private string _webSearchText = string.Empty;

        private List<AppUsageRow> _allApplicationUsage = new();
        private List<WebUsageRow> _allWebsiteUsage = new();

        public UsageMetricsViewModel(IUsageMetricsService usageMetricsService)
        {
            _usageMetricsService = usageMetricsService;
            ApplyFilterCommand = new RelayCommand(async () => await LoadDataAsync(), () => !IsLoading);
            ExportAppUsageCommand = new RelayCommand(async () => await Task.CompletedTask, () => true);
            ExportWebUsageCommand = new RelayCommand(async () => await Task.CompletedTask, () => true);
            _ = LoadDataAsync();
        }

        public ObservableCollection<AppUsageRow> FilteredApplicationUsage { get; } = new();
        public ObservableCollection<WebUsageRow> FilteredWebsiteUsage { get; } = new();

        public DateTime StartDate
        {
            get => _startDate;
            set { _startDate = value; OnPropertyChanged(); }
        }

        public DateTime EndDate
        {
            get => _endDate;
            set { _endDate = value; OnPropertyChanged(); }
        }

        public int TotalApplications
        {
            get => _totalApplications;
            set { _totalApplications = value; OnPropertyChanged(); }
        }

        public int TotalWebsites
        {
            get => _totalWebsites;
            set { _totalWebsites = value; OnPropertyChanged(); }
        }

        public double TotalHours
        {
            get => _totalHours;
            set { _totalHours = value; OnPropertyChanged(); }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        public string AppSearchText
        {
            get => _appSearchText;
            set { _appSearchText = value; OnPropertyChanged(); ApplyAppFilter(); }
        }

        public string WebSearchText
        {
            get => _webSearchText;
            set { _webSearchText = value; OnPropertyChanged(); ApplyWebFilter(); }
        }

        public ICommand ApplyFilterCommand { get; }
        public ICommand ExportAppUsageCommand { get; }
        public ICommand ExportWebUsageCommand { get; }

        private async Task LoadDataAsync()
        {
            IsLoading = true;
            try
            {
                var startUtc = DateTime.SpecifyKind(StartDate.Date, DateTimeKind.Utc);
                var endUtc = DateTime.SpecifyKind(EndDate.Date.AddDays(1).AddSeconds(-1), DateTimeKind.Utc);

                var summary = await _usageMetricsService.GetUsageSummaryAsync(startUtc, endUtc);
                TotalApplications = summary.TotalApplications;
                TotalWebsites = summary.TotalWebsites;
                TotalHours = summary.TotalHours;

                var appDetails = await _usageMetricsService.GetApplicationUsageDetailsAsync(startUtc, endUtc);
                _allApplicationUsage = appDetails.Select(a => new AppUsageRow
                {
                    ApplicationName = a.ApplicationName,
                    PCName = a.PCName,
                    RoomNumber = a.RoomNumber,
                    StartTime = a.StartTime,
                    EndTime = a.EndTime,
                    Duration = a.Duration
                }).ToList();

                var webDetails = await _usageMetricsService.GetWebsiteUsageDetailsAsync(startUtc, endUtc);
                _allWebsiteUsage = webDetails.Select(w => new WebUsageRow
                {
                    Url = w.Url,
                    Title = w.Title,
                    PCName = w.PCName,
                    RoomNumber = w.RoomNumber,
                    VisitTime = w.VisitTime,
                    VisitCount = w.VisitCount
                }).ToList();

                ApplyAppFilter();
                ApplyWebFilter();
            }
            catch (Exception ex)
            {
                var logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
                var errorText = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] USAGE METRICS LOAD ERROR\n" +
                                $"  Type: {ex.GetType().FullName}\n" +
                                $"  Message: {ex.Message}\n" +
                                $"  Inner: {ex.InnerException?.Message}\n" +
                                $"  InnerInner: {ex.InnerException?.InnerException?.Message}\n" +
                                $"  StackTrace:\n{ex.StackTrace}\n\n";
                System.IO.File.AppendAllText(logPath, errorText);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ApplyAppFilter()
        {
            FilteredApplicationUsage.Clear();
            var filtered = string.IsNullOrEmpty(AppSearchText)
                ? _allApplicationUsage
                : _allApplicationUsage.Where(a =>
                    a.ApplicationName.Contains(AppSearchText, StringComparison.OrdinalIgnoreCase) ||
                    a.PCName.Contains(AppSearchText, StringComparison.OrdinalIgnoreCase));

            foreach (var item in filtered)
                FilteredApplicationUsage.Add(item);
        }

        private void ApplyWebFilter()
        {
            FilteredWebsiteUsage.Clear();
            var filtered = string.IsNullOrEmpty(WebSearchText)
                ? _allWebsiteUsage
                : _allWebsiteUsage.Where(w =>
                    w.Url.Contains(WebSearchText, StringComparison.OrdinalIgnoreCase) ||
                    w.Title.Contains(WebSearchText, StringComparison.OrdinalIgnoreCase) ||
                    w.PCName.Contains(WebSearchText, StringComparison.OrdinalIgnoreCase));

            foreach (var item in filtered)
                FilteredWebsiteUsage.Add(item);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class AppUsageRow : INotifyPropertyChanged
    {
        public string ApplicationName { get; set; } = string.Empty;
        public string PCName { get; set; } = string.Empty;
        public string RoomNumber { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan? Duration { get; set; }

        public string FormattedDuration
        {
            get
            {
                if (Duration == null) return "Active";
                var d = Duration.Value;
                if (d.TotalHours >= 1)
                    return $"{(int)d.TotalHours}h {d.Minutes}m";
                if (d.TotalMinutes >= 1)
                    return $"{d.Minutes}m {d.Seconds}s";
                return $"{d.Seconds}s";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class WebUsageRow : INotifyPropertyChanged
    {
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string PCName { get; set; } = string.Empty;
        public string RoomNumber { get; set; } = string.Empty;
        public DateTime VisitTime { get; set; }
        public int VisitCount { get; set; }

        public string FormattedDuration => $"{VisitCount} visits";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}