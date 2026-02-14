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
        private int _appCurrentPage = 1;
        private int _appTotalPages = 1;
        private int _webCurrentPage = 1;
        private int _webTotalPages = 1;
        private const int PageSize = 50;

        public UsageMetricsViewModel(IUsageMetricsService usageMetricsService)
        {
            _usageMetricsService = usageMetricsService;
            ApplyFilterCommand = new RelayCommand(async () => await LoadDataAsync(), () => !IsLoading);
            ExportAppUsageCommand = new RelayCommand(async () => await Task.CompletedTask, () => true);
            ExportWebUsageCommand = new RelayCommand(async () => await Task.CompletedTask, () => true);
            AppPreviousPageCommand = new RelayCommand(async () => await LoadAppPageAsync(_appCurrentPage - 1), () => _appCurrentPage > 1);
            AppNextPageCommand = new RelayCommand(async () => await LoadAppPageAsync(_appCurrentPage + 1), () => _appCurrentPage < _appTotalPages);
            WebPreviousPageCommand = new RelayCommand(async () => await LoadWebPageAsync(_webCurrentPage - 1), () => _webCurrentPage > 1);
            WebNextPageCommand = new RelayCommand(async () => await LoadWebPageAsync(_webCurrentPage + 1), () => _webCurrentPage < _webTotalPages);
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
            set { _appSearchText = value; OnPropertyChanged(); _ = LoadAppPageAsync(1); }
        }

        public string WebSearchText
        {
            get => _webSearchText;
            set { _webSearchText = value; OnPropertyChanged(); _ = LoadWebPageAsync(1); }
        }

        public int AppCurrentPage
        {
            get => _appCurrentPage;
            set { _appCurrentPage = value; OnPropertyChanged(); OnPropertyChanged(nameof(AppPageInfo)); }
        }

        public int AppTotalPages
        {
            get => _appTotalPages;
            set { _appTotalPages = value; OnPropertyChanged(); OnPropertyChanged(nameof(AppPageInfo)); }
        }

        public int WebCurrentPage
        {
            get => _webCurrentPage;
            set { _webCurrentPage = value; OnPropertyChanged(); OnPropertyChanged(nameof(WebPageInfo)); }
        }

        public int WebTotalPages
        {
            get => _webTotalPages;
            set { _webTotalPages = value; OnPropertyChanged(); OnPropertyChanged(nameof(WebPageInfo)); }
        }

        public string AppPageInfo => $"Page {AppCurrentPage} of {AppTotalPages}";
        public string WebPageInfo => $"Page {WebCurrentPage} of {WebTotalPages}";

        public ICommand ApplyFilterCommand { get; }
        public ICommand ExportAppUsageCommand { get; }
        public ICommand ExportWebUsageCommand { get; }
        public ICommand AppPreviousPageCommand { get; }
        public ICommand AppNextPageCommand { get; }
        public ICommand WebPreviousPageCommand { get; }
        public ICommand WebNextPageCommand { get; }

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

                await LoadAppPageAsync(1);
                await LoadWebPageAsync(1);
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

        private async Task LoadAppPageAsync(int pageNumber)
        {
            try
            {
                var startUtc = DateTime.SpecifyKind(StartDate.Date, DateTimeKind.Utc);
                var endUtc = DateTime.SpecifyKind(EndDate.Date.AddDays(1).AddSeconds(-1), DateTimeKind.Utc);

                var result = await _usageMetricsService.GetApplicationUsageDetailsPaginatedAsync(
                    startUtc, endUtc, pageNumber, PageSize, AppSearchText);

                FilteredApplicationUsage.Clear();
                foreach (var item in result.Items)
                {
                    FilteredApplicationUsage.Add(new AppUsageRow
                    {
                        ApplicationName = item.ApplicationName,
                        PCName = item.PCName,
                        RoomNumber = item.RoomNumber,
                        StartTime = item.StartTime,
                        EndTime = item.EndTime,
                        Duration = item.Duration
                    });
                }

                AppCurrentPage = result.PageNumber;
                AppTotalPages = result.TotalPages;
            }
            catch { }
        }

        private async Task LoadWebPageAsync(int pageNumber)
        {
            try
            {
                var startUtc = DateTime.SpecifyKind(StartDate.Date, DateTimeKind.Utc);
                var endUtc = DateTime.SpecifyKind(EndDate.Date.AddDays(1).AddSeconds(-1), DateTimeKind.Utc);

                var result = await _usageMetricsService.GetWebsiteUsageDetailsPaginatedAsync(
                    startUtc, endUtc, pageNumber, PageSize, WebSearchText);

                FilteredWebsiteUsage.Clear();
                foreach (var item in result.Items)
                {
                    FilteredWebsiteUsage.Add(new WebUsageRow
                    {
                        Url = item.Url,
                        Title = item.Title,
                        PCName = item.PCName,
                        RoomNumber = item.RoomNumber,
                        VisitTime = item.VisitTime,
                        VisitCount = item.VisitCount
                    });
                }

                WebCurrentPage = result.PageNumber;
                WebTotalPages = result.TotalPages;
            }
            catch { }
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