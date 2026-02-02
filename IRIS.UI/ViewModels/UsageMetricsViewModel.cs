using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using IRIS.Core.Services;
using IRIS.UI.Helpers;

namespace IRIS.UI.ViewModels
{
    public class UsageMetricsViewModel : INotifyPropertyChanged
    {
        private readonly IUsageMetricsService _usageMetricsService;
        private string _selectedTimeRange = "Last 7 Days";

        public UsageMetricsViewModel(IUsageMetricsService usageMetricsService)
        {
            _usageMetricsService = usageMetricsService;
            RefreshCommand = new RelayCommand(async () => await LoadDataAsync(), () => true);
            _ = LoadDataAsync();
        }

        public ObservableCollection<ApplicationUsageModel> Applications { get; } = new();
        public ObservableCollection<WebsiteUsageModel> Websites { get; } = new();

        public string SelectedTimeRange
        {
            get => _selectedTimeRange;
            set { _selectedTimeRange = value; OnPropertyChanged(); _ = LoadDataAsync(); }
        }

        public ICommand RefreshCommand { get; }

        private async Task LoadDataAsync()
        {
            var days = SelectedTimeRange switch
            {
                "Last 24 Hours" => 1,
                "Last 7 Days" => 7,
                "Last 30 Days" => 30,
                "Last 90 Days" => 90,
                _ => 7
            };

            var apps = await _usageMetricsService.GetMostUsedApplicationsAsync(days);
            var websites = await _usageMetricsService.GetMostVisitedWebsitesAsync(days);

            Applications.Clear();
            foreach (var app in apps)
            {
                Applications.Add(new ApplicationUsageModel
                {
                    Name = app.ApplicationName,
                    UsageCount = app.UsageCount,
                    Percentage = app.Percentage
                });
            }

            Websites.Clear();
            foreach (var website in websites)
            {
                Websites.Add(new WebsiteUsageModel
                {
                    Domain = website.Domain,
                    VisitCount = website.VisitCount,
                    Percentage = website.Percentage
                });
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class ApplicationUsageModel : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private int _usageCount;
        private double _percentage;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public int UsageCount
        {
            get => _usageCount;
            set { _usageCount = value; OnPropertyChanged(); }
        }

        public double Percentage
        {
            get => _percentage;
            set { _percentage = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class WebsiteUsageModel : INotifyPropertyChanged
    {
        private string _domain = string.Empty;
        private int _visitCount;
        private double _percentage;

        public string Domain
        {
            get => _domain;
            set { _domain = value; OnPropertyChanged(); }
        }

        public int VisitCount
        {
            get => _visitCount;
            set { _visitCount = value; OnPropertyChanged(); }
        }

        public double Percentage
        {
            get => _percentage;
            set { _percentage = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}