using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using IRIS.Core.Services.Contracts;
using IRIS.UI.Helpers;
using Microsoft.Win32;
using IRIS.UI.Services;
using System.Threading;

namespace IRIS.UI.ViewModels
{
    public class UsageMetricsViewModel : INotifyPropertyChanged, INavigationAware
    {
        private readonly IUsageMetricsService _usageMetricsService;
        private readonly RelayCommand _applyAppFiltersRelayCommand;
        private readonly RelayCommand _resetAppFiltersRelayCommand;
        private readonly RelayCommand _applyWebFiltersRelayCommand;
        private readonly RelayCommand _resetWebFiltersRelayCommand;
        private readonly RelayCommand _exportAppUsageRelayCommand;
        private readonly RelayCommand _exportWebUsageRelayCommand;
        private readonly SemaphoreSlim _loadDataSemaphore = new(1, 1);
        private readonly SemaphoreSlim _appPageSemaphore = new(1, 1);
        private readonly SemaphoreSlim _webPageSemaphore = new(1, 1);
        private DateTime? _startDate;
        private DateTime? _endDate;
        private DateTime? _appliedStartDate;
        private DateTime? _appliedEndDate;
        private int _totalApplications;
        private int _totalWebsites;
        private double _totalHours;
        private bool _isLoading;
        private string _appSearchText = string.Empty;
        private string _appliedAppSearchText = string.Empty;
        private string _selectedAppLaboratory = "All Laboratories";
        private string _appliedAppLaboratory = "All Laboratories";
        private string _webSearchText = string.Empty;
        private string _appliedWebSearchText = string.Empty;
        private string _selectedWebLaboratory = "All Laboratories";
        private string _appliedWebLaboratory = "All Laboratories";
        private int _appCurrentPage = 1;
        private int _appTotalPages = 1;
        private int _appTotalCount = 0;
        private int _webCurrentPage = 1;
        private int _webTotalPages = 1;
        private int _webTotalCount = 0;
        private int _pageSize = 10;
        private bool _isActive = true;
        public int[] PageSizeOptions { get; } = { 10, 25, 50, 100 };

        public UsageMetricsViewModel(IUsageMetricsService usageMetricsService)
        {
            _usageMetricsService = usageMetricsService;

            _applyAppFiltersRelayCommand = new RelayCommand(async () => await ApplyAppFiltersAsync(), () => !IsLoading);
            _resetAppFiltersRelayCommand = new RelayCommand(async () => await ResetAppFiltersAsync(), () => !IsLoading);
            _applyWebFiltersRelayCommand = new RelayCommand(async () => await ApplyWebFiltersAsync(), () => !IsLoading);
            _resetWebFiltersRelayCommand = new RelayCommand(async () => await ResetWebFiltersAsync(), () => !IsLoading);
            _exportAppUsageRelayCommand = new RelayCommand(async () => await ExportUsageMetricsAsync(), () => !IsLoading);
            _exportWebUsageRelayCommand = new RelayCommand(async () => await ExportUsageMetricsAsync(), () => !IsLoading);

            ApplyAppFiltersCommand = _applyAppFiltersRelayCommand;
            ResetAppFiltersCommand = _resetAppFiltersRelayCommand;
            ApplyWebFiltersCommand = _applyWebFiltersRelayCommand;
            ResetWebFiltersCommand = _resetWebFiltersRelayCommand;
            ExportAppUsageCommand = _exportAppUsageRelayCommand;
            ExportWebUsageCommand = _exportWebUsageRelayCommand;
            AppPreviousPageCommand = new RelayCommand(async () => await AppPreviousPageAsync(), () => true);
            AppNextPageCommand = new RelayCommand(async () => await AppNextPageAsync(), () => true);
            WebPreviousPageCommand = new RelayCommand(async () => await WebPreviousPageAsync(), () => true);
            WebNextPageCommand = new RelayCommand(async () => await WebNextPageAsync(), () => true);
            _ = LoadDataAsync();
        }

        public ObservableCollection<AppUsageRow> FilteredApplicationUsage { get; } = new();
        public ObservableCollection<WebUsageRow> FilteredWebsiteUsage { get; } = new();
        public ObservableCollection<string> AppLaboratoryOptions { get; } = new() { "All Laboratories" };
        public ObservableCollection<string> WebLaboratoryOptions { get; } = new() { "All Laboratories" };

        public DateTime? StartDate
        {
            get => _startDate;
            set { _startDate = value; OnPropertyChanged(); }
        }

        public DateTime? EndDate
        {
            get => _endDate;
            set { _endDate = value; OnPropertyChanged(); }
        }

        public int TotalApplications
        {
            get => _totalApplications;
            set { _totalApplications = value; OnPropertyChanged(); }
        }

        public double TotalHours
        {
            get => _totalHours;
            set { _totalHours = value; OnPropertyChanged(); }
        }

        public int TotalWebsites
        {
            get => _totalWebsites;
            set { _totalWebsites = value; OnPropertyChanged(); }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
                _applyAppFiltersRelayCommand.RaiseCanExecuteChanged();
                _resetAppFiltersRelayCommand.RaiseCanExecuteChanged();
                _applyWebFiltersRelayCommand.RaiseCanExecuteChanged();
                _resetWebFiltersRelayCommand.RaiseCanExecuteChanged();
                _exportAppUsageRelayCommand.RaiseCanExecuteChanged();
                _exportWebUsageRelayCommand.RaiseCanExecuteChanged();
            }
        }

        public string AppSearchText
        {
            get => _appSearchText;
            set { _appSearchText = value; OnPropertyChanged(); }
        }

        public string WebSearchText
        {
            get => _webSearchText;
            set { _webSearchText = value; OnPropertyChanged(); }
        }

        public string SelectedAppLaboratory
        {
            get => _selectedAppLaboratory;
            set { _selectedAppLaboratory = value; OnPropertyChanged(); }
        }

        public string SelectedWebLaboratory
        {
            get => _selectedWebLaboratory;
            set { _selectedWebLaboratory = value; OnPropertyChanged(); }
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

        public int AppTotalCount
        {
            get => _appTotalCount;
            set { _appTotalCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(AppPageInfo)); }
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

        public int WebTotalCount
        {
            get => _webTotalCount;
            set { _webTotalCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(WebPageInfo)); }
        }

        public int PageSize
        {
            get => _pageSize;
            set { _pageSize = value; OnPropertyChanged(); _ = LoadDataAsync(); }
        }

        public string AppPageInfo => $"Page {AppCurrentPage} of {AppTotalPages} ({AppTotalCount} total entries)";
        public string WebPageInfo => $"Page {WebCurrentPage} of {WebTotalPages} ({WebTotalCount} total entries)";

        public ICommand ApplyAppFiltersCommand { get; }
        public ICommand ResetAppFiltersCommand { get; }
        public ICommand ApplyWebFiltersCommand { get; }
        public ICommand ResetWebFiltersCommand { get; }
        public ICommand ExportAppUsageCommand { get; }
        public ICommand ExportWebUsageCommand { get; }
        public ICommand AppPreviousPageCommand { get; }
        public ICommand AppNextPageCommand { get; }
        public ICommand WebPreviousPageCommand { get; }
        public ICommand WebNextPageCommand { get; }

        private async Task ApplyAppFiltersAsync()
        {
            if (StartDate.HasValue && EndDate.HasValue && EndDate.Value.Date < StartDate.Value.Date)
            {
                MessageBox.Show(
                    "'To' date cannot be earlier than 'From' date.",
                    "Invalid Date Range",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            _appliedStartDate = StartDate?.Date;
            _appliedEndDate = EndDate?.Date;
            _appliedAppSearchText = AppSearchText;
            _appliedAppLaboratory = SelectedAppLaboratory;

            await LoadDataAsync();
        }

        private async Task ResetAppFiltersAsync()
        {
            StartDate = null;
            EndDate = null;
            AppSearchText = string.Empty;
            SelectedAppLaboratory = "All Laboratories";

            _appliedStartDate = null;
            _appliedEndDate = null;
            _appliedAppSearchText = AppSearchText;
            _appliedAppLaboratory = SelectedAppLaboratory;

            await LoadDataAsync();
        }

        private async Task ApplyWebFiltersAsync()
        {
            _appliedWebSearchText = WebSearchText;
            _appliedWebLaboratory = SelectedWebLaboratory;
            await LoadDataAsync();
        }

        private async Task ResetWebFiltersAsync()
        {
            WebSearchText = string.Empty;
            SelectedWebLaboratory = "All Laboratories";

            _appliedWebSearchText = WebSearchText;
            _appliedWebLaboratory = SelectedWebLaboratory;
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

            IsLoading = true;
            try
            {
                if (!_isActive)
                {
                    return;
                }

                var startUtc = DateTime.SpecifyKind(_appliedStartDate ?? DateTime.UnixEpoch, DateTimeKind.Utc);
                var endUtc = DateTime.SpecifyKind((_appliedEndDate?.AddDays(1).AddSeconds(-1)) ?? DateTime.UtcNow, DateTimeKind.Utc);
                var webStartUtc = DateTime.SpecifyKind(DateTime.UnixEpoch, DateTimeKind.Utc);
                var webEndUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

                var summary = await _usageMetricsService.GetUsageSummaryAsync(startUtc, endUtc);
                TotalApplications = summary.TotalApplications;
                TotalWebsites = summary.TotalWebsites;
                TotalHours = summary.TotalHours;

                await LoadAppLaboratoryOptionsAsync(startUtc, endUtc);
                await LoadWebLaboratoryOptionsAsync(webStartUtc, webEndUtc);
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
                _loadDataSemaphore.Release();
            }
        }

        private async Task LoadAppPageAsync(int pageNumber)
        {
            if (!_isActive)
            {
                return;
            }

            if (!await _appPageSemaphore.WaitAsync(0))
            {
                return;
            }

            try
            {
                if (!_isActive)
                {
                    return;
                }

                var startUtc = DateTime.SpecifyKind(_appliedStartDate ?? DateTime.UnixEpoch, DateTimeKind.Utc);
                var endUtc = DateTime.SpecifyKind((_appliedEndDate?.AddDays(1).AddSeconds(-1)) ?? DateTime.UtcNow, DateTimeKind.Utc);

                var result = await _usageMetricsService.GetApplicationUsageDetailsPaginatedAsync(
                    startUtc,
                    endUtc,
                    pageNumber,
                    _pageSize,
                    _appliedAppSearchText,
                    _appliedAppLaboratory == "All Laboratories" ? null : _appliedAppLaboratory);

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
                AppTotalCount = result.TotalCount;
            }
            catch { }
            finally
            {
                _appPageSemaphore.Release();
            }
        }

        private async Task AppPreviousPageAsync()
        {
            if (AppCurrentPage > 1)
            {
                await LoadAppPageAsync(AppCurrentPage - 1);
            }
        }

        private async Task AppNextPageAsync()
        {
            if (AppCurrentPage < AppTotalPages)
            {
                await LoadAppPageAsync(AppCurrentPage + 1);
            }
        }

        private async Task LoadWebPageAsync(int pageNumber)
        {
            if (!_isActive)
            {
                return;
            }

            if (!await _webPageSemaphore.WaitAsync(0))
            {
                return;
            }

            try
            {
                if (!_isActive)
                {
                    return;
                }

                var startUtc = DateTime.SpecifyKind(DateTime.UnixEpoch, DateTimeKind.Utc);
                var endUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

                var result = await _usageMetricsService.GetWebsiteUsageDetailsPaginatedAsync(
                    startUtc,
                    endUtc,
                    pageNumber,
                    _pageSize,
                    _appliedWebSearchText,
                    _appliedWebLaboratory == "All Laboratories" ? null : _appliedWebLaboratory);

                FilteredWebsiteUsage.Clear();
                foreach (var item in result.Items)
                {
                    FilteredWebsiteUsage.Add(new WebUsageRow
                    {
                        Domain = item.Domain,
                        Browser = item.Browser,
                        PCName = item.PCName,
                        RoomNumber = item.RoomNumber,
                        VisitTime = item.VisitTime,
                        VisitCount = item.VisitCount
                    });
                }

                WebCurrentPage = result.PageNumber;
                WebTotalPages = result.TotalPages;
                WebTotalCount = result.TotalCount;
            }
            catch { }
            finally
            {
                _webPageSemaphore.Release();
            }
        }

        private async Task LoadAppLaboratoryOptionsAsync(DateTime startUtc, DateTime endUtc)
        {
            var selectedBeforeReload = SelectedAppLaboratory;
            var labs = await _usageMetricsService.GetApplicationUsageLaboratoriesAsync(startUtc, endUtc);

            AppLaboratoryOptions.Clear();
            AppLaboratoryOptions.Add("All Laboratories");

            foreach (var lab in labs)
            {
                AppLaboratoryOptions.Add(lab);
            }

            SelectedAppLaboratory = AppLaboratoryOptions.Contains(selectedBeforeReload)
                ? selectedBeforeReload
                : "All Laboratories";

            if (!AppLaboratoryOptions.Contains(_appliedAppLaboratory))
            {
                _appliedAppLaboratory = "All Laboratories";
            }
        }

        private async Task LoadWebLaboratoryOptionsAsync(DateTime startUtc, DateTime endUtc)
        {
            var selectedBeforeReload = SelectedWebLaboratory;
            var labs = await _usageMetricsService.GetWebsiteUsageLaboratoriesAsync(startUtc, endUtc);

            WebLaboratoryOptions.Clear();
            WebLaboratoryOptions.Add("All Laboratories");

            foreach (var lab in labs)
            {
                WebLaboratoryOptions.Add(lab);
            }

            SelectedWebLaboratory = WebLaboratoryOptions.Contains(selectedBeforeReload)
                ? selectedBeforeReload
                : "All Laboratories";

            if (!WebLaboratoryOptions.Contains(_appliedWebLaboratory))
            {
                _appliedWebLaboratory = "All Laboratories";
            }
        }

        public void OnNavigatedFrom()
        {
            _isActive = false;
        }

        private async Task WebPreviousPageAsync()
        {
            if (WebCurrentPage > 1)
            {
                await LoadWebPageAsync(WebCurrentPage - 1);
            }
        }

        private async Task WebNextPageAsync()
        {
            if (WebCurrentPage < WebTotalPages)
            {
                await LoadWebPageAsync(WebCurrentPage + 1);
            }
        }

        private async Task ExportUsageMetricsAsync()
        {
            try
            {
                var startUtc = DateTime.SpecifyKind(_appliedStartDate ?? DateTime.UnixEpoch, DateTimeKind.Utc);
                var endUtc = DateTime.SpecifyKind((_appliedEndDate?.AddDays(1).AddSeconds(-1)) ?? DateTime.UtcNow, DateTimeKind.Utc);

                var bytes = await _usageMetricsService.ExportUsageMetricsToExcelAsync(
                    startUtc,
                    endUtc,
                    _appliedAppSearchText,
                    _appliedWebSearchText,
                    _appliedAppLaboratory == "All Laboratories" ? null : _appliedAppLaboratory,
                    _appliedWebLaboratory == "All Laboratories" ? null : _appliedWebLaboratory);

                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                    DefaultExt = "xlsx",
                    FileName = $"UsageMetrics_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
                };

                if (saveFileDialog.ShowDialog() != true)
                {
                    return;
                }

                await File.WriteAllBytesAsync(saveFileDialog.FileName, bytes);
                MessageBox.Show(
                    "Usage metrics for both Application and Website tabs were exported to an Excel file.",
                    "Export Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export usage metrics: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
        public string Domain { get; set; } = string.Empty;
        public string Browser { get; set; } = string.Empty;
        public string PCName { get; set; } = string.Empty;
        public string RoomNumber { get; set; } = string.Empty;
        public DateTime VisitTime { get; set; }
        public int VisitCount { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}