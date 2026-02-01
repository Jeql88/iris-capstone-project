using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using IRIS.UI.Helpers;

namespace IRIS.UI.ViewModels
{
    public class UsageMetricsViewModel : INotifyPropertyChanged
    {
        private string _selectedTimeRange = "Last 7 Days";

        public UsageMetricsViewModel()
        {
            RefreshCommand = new RelayCommand(async () => await LoadDataAsync(), () => true);
            
            // Mock data for design
            LoadMockData();
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
            await Task.CompletedTask;
            // Backend implementation will go here
        }

        private void LoadMockData()
        {
            Applications.Clear();
            Applications.Add(new ApplicationUsageModel { Name = "Adobe Photoshop", UsageCount = 245, Percentage = 28.5 });
            Applications.Add(new ApplicationUsageModel { Name = "Google Chrome", UsageCount = 198, Percentage = 23.0 });
            Applications.Add(new ApplicationUsageModel { Name = "Microsoft Word", UsageCount = 156, Percentage = 18.1 });
            Applications.Add(new ApplicationUsageModel { Name = "AutoCAD", UsageCount = 134, Percentage = 15.6 });
            Applications.Add(new ApplicationUsageModel { Name = "Visual Studio", UsageCount = 89, Percentage = 10.3 });
            Applications.Add(new ApplicationUsageModel { Name = "Figma", UsageCount = 67, Percentage = 7.8 });
            Applications.Add(new ApplicationUsageModel { Name = "Blender", UsageCount = 45, Percentage = 5.2 });
            Applications.Add(new ApplicationUsageModel { Name = "Notepad++", UsageCount = 32, Percentage = 3.7 });

            Websites.Clear();
            Websites.Add(new WebsiteUsageModel { Domain = "youtube.com", VisitCount = 1234, Percentage = 32.1 });
            Websites.Add(new WebsiteUsageModel { Domain = "facebook.com", VisitCount = 987, Percentage = 25.7 });
            Websites.Add(new WebsiteUsageModel { Domain = "google.com", VisitCount = 756, Percentage = 19.7 });
            Websites.Add(new WebsiteUsageModel { Domain = "github.com", VisitCount = 543, Percentage = 14.1 });
            Websites.Add(new WebsiteUsageModel { Domain = "stackoverflow.com", VisitCount = 321, Percentage = 8.4 });
            Websites.Add(new WebsiteUsageModel { Domain = "behance.net", VisitCount = 234, Percentage = 6.1 });
            Websites.Add(new WebsiteUsageModel { Domain = "dribbble.com", VisitCount = 187, Percentage = 4.9 });
            Websites.Add(new WebsiteUsageModel { Domain = "pinterest.com", VisitCount = 145, Percentage = 3.8 });
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