using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IRIS.Core.Services.Contracts;
using IRIS.UI.ViewModels;
using IRIS.UI.Services;
using IRIS.UI.Views.Shared;
using Microsoft.Extensions.DependencyInjection;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Wpf;

namespace IRIS.UI.Views.Common
{
    public partial class DashboardView : UserControl
    {
        private readonly DashboardViewModel _viewModel;
        private INavigationService? _navigationService;
        private ScrollViewer? dashboardContent;
        private static readonly SolidColorBrush ActiveBrush = new(Color.FromRgb(180, 40, 40));
        private static readonly SolidColorBrush DefaultForeground = Brushes.White;
        private static readonly SolidColorBrush ActiveForeground = Brushes.White;

        public DashboardView() : this(((App)Application.Current).GetServiceProvider().GetRequiredService<DashboardViewModel>())
        {
        }

        public DashboardView(DashboardViewModel viewModel)
        {
            _viewModel = viewModel;
            InitializeComponent();
            DataContext = _viewModel;
            Loaded += OnLoaded;
        }

        public void SetNavigationService(INavigationService navigationService)
        {
            _navigationService = navigationService;
            UserHeader.SetNavigationService(navigationService);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            dashboardContent = MainContent.Content as ScrollViewer;
        }

        /// <summary>
        /// Resets all sidebar buttons to transparent, then highlights the active one.
        /// </summary>
        private void SetActiveButton(Button activeButton)
        {
            var allButtons = new[]
            {
                DashboardBtn, MonitorBtn, SoftwareManagementBtn,
                PolicyBtn, LabsBtn, AccessLogsBtn, UserManagementBtn,
                UsageMetricsBtn, AlertsBtn
            };

            foreach (var btn in allButtons)
            {
                btn.Background = Brushes.Transparent;
                btn.Foreground = DefaultForeground;
            }

            activeButton.Background = ActiveBrush;
            activeButton.Foreground = ActiveForeground;
        }

        private void ClearActiveButton()
        {
            var allButtons = new[]
            {
                DashboardBtn, MonitorBtn, SoftwareManagementBtn,
                PolicyBtn, LabsBtn, AccessLogsBtn, UserManagementBtn,
                UsageMetricsBtn, AlertsBtn
            };

            foreach (var btn in allButtons)
            {
                btn.Background = Brushes.Transparent;
                btn.Foreground = DefaultForeground;
            }
        }

        public void ClearButtonsAndPanel()
        {
            ClearActiveButton();
            CollapseRightPanel();
        }

        private void CollapseRightPanel()
        {
            if (MainGrid.ColumnDefinitions.Count > 2)
            {
                MainGrid.ColumnDefinitions[2].Width = new GridLength(0);
            }
        }

        private void ShowRightPanel()
        {
            if (MainGrid.ColumnDefinitions.Count > 2)
            {
                MainGrid.ColumnDefinitions[2].Width = new GridLength(320);
            }
        }

        private void DashboardBtn_Click(object sender, RoutedEventArgs e)
        {
            UserHeader.SetVisibility(true);
            SetActiveButton(DashboardBtn);
            ShowRightPanel();
            MainContent.Content = dashboardContent;
        }

        private void MonitorBtn_Click(object sender, RoutedEventArgs e)
        {
            UserHeader.SetVisibility(true);
            UserHeader.CloseDropdown();
            SetActiveButton(MonitorBtn);
            CollapseRightPanel();
            _navigationService?.NavigateTo("Monitor");
        }

        private void PolicyBtn_Click(object sender, RoutedEventArgs e)
        {
            UserHeader.SetVisibility(true);
            UserHeader.CloseDropdown();
            SetActiveButton(PolicyBtn);
            CollapseRightPanel();
            _navigationService?.NavigateTo("PolicyEnforcement");
        }

        private void LabsBtn_Click(object sender, RoutedEventArgs e)
        {
            UserHeader.SetVisibility(true);
            UserHeader.CloseDropdown();
            SetActiveButton(LabsBtn);
            CollapseRightPanel();
            _navigationService?.NavigateTo("Labs");
        }

        private void SoftwareManagementBtn_Click(object sender, RoutedEventArgs e)
        {
            UserHeader.SetVisibility(true);
            UserHeader.CloseDropdown();
            SetActiveButton(SoftwareManagementBtn);
            CollapseRightPanel();
            _navigationService?.NavigateTo("SoftwareManagement");
        }

        private void UserManagementBtn_Click(object sender, RoutedEventArgs e)
        {
            UserHeader.SetVisibility(true);
            UserHeader.CloseDropdown();
            SetActiveButton(UserManagementBtn);
            CollapseRightPanel();
            _navigationService?.NavigateTo("UserManagement");
        }

        private void UsageMetricsBtn_Click(object sender, RoutedEventArgs e)
        {
            UserHeader.SetVisibility(true);
            UserHeader.CloseDropdown();
            SetActiveButton(UsageMetricsBtn);
            CollapseRightPanel();
            _navigationService?.NavigateTo("UsageMetrics");
        }

        private void AccessLogsBtn_Click(object sender, RoutedEventArgs e)
        {
            UserHeader.SetVisibility(true);
            UserHeader.CloseDropdown();
            SetActiveButton(AccessLogsBtn);
            CollapseRightPanel();
            _navigationService?.NavigateTo("AccessLogs");
        }

        private void AlertsBtn_Click(object sender, RoutedEventArgs e)
        {
            UserHeader.SetVisibility(true);
            UserHeader.CloseDropdown();
            SetActiveButton(AlertsBtn);
            CollapseRightPanel();
            _navigationService?.NavigateTo("Alerts");
        }

        private async void LogoutBtn_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to logout?", "Confirm Logout",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var serviceProvider = ((App)Application.Current).GetServiceProvider();
                var authService = (IAuthenticationService)serviceProvider.GetService(typeof(IAuthenticationService))!;
                await authService.LogoutAsync();

                var loginWindow = new LoginWindow(authService);
                loginWindow.Show();

                Window.GetWindow(this)?.Close();
            }
        }
        private static void OpenPlotInWindow(string title, PlotModel? model)
        {
            if (model == null)
            {
                return;
            }

            var detailedModel = CreateDetailedPlotModel(title, model);
            var zoomWindow = new Window
            {
                Title = title,
                Width = 1220,
                Height = 760,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new PlotView
                {
                    Model = detailedModel,
                    Margin = new Thickness(10)
                }
            };

            zoomWindow.ShowDialog();
        }

        private static PlotModel CreateDetailedPlotModel(string title, PlotModel source)
        {
            var detailed = new PlotModel
            {
                Title = title,
                Subtitle = "Detailed view • scroll to zoom • drag to pan • hover points for exact values"
            };

            foreach (var axis in source.Axes)
            {
                if (axis is DateTimeAxis dateTimeAxis)
                {
                    detailed.Axes.Add(new DateTimeAxis
                    {
                        Position = dateTimeAxis.Position,
                        StringFormat = "yyyy-MM-dd HH:mm",
                        IntervalType = DateTimeIntervalType.Auto,
                        MinorIntervalType = DateTimeIntervalType.Auto,
                        IsZoomEnabled = true,
                        IsPanEnabled = true,
                        FontSize = 12,
                        Title = dateTimeAxis.Title
                    });
                    continue;
                }

                if (axis is LinearAxis linearAxis)
                {
                    detailed.Axes.Add(new LinearAxis
                    {
                        Position = linearAxis.Position,
                        Minimum = linearAxis.ActualMinimum,
                        Maximum = linearAxis.ActualMaximum,
                        LabelFormatter = linearAxis.LabelFormatter,
                        IsZoomEnabled = true,
                        IsPanEnabled = true,
                        FontSize = 12,
                        Title = linearAxis.Title,
                        MinimumPadding = 0.1,
                        MaximumPadding = 0.1
                    });
                    continue;
                }

                detailed.Axes.Add(axis);
            }

            foreach (var line in source.Series.OfType<LineSeries>())
            {
                var detailedSeries = new LineSeries
                {
                    Title = string.IsNullOrWhiteSpace(line.Title) ? title : line.Title,
                    Color = line.Color,
                    StrokeThickness = Math.Max(2, line.StrokeThickness),
                    MarkerType = MarkerType.Circle,
                    MarkerSize = 3,
                    MarkerStroke = line.Color,
                    MarkerFill = OxyColors.White,
                    CanTrackerInterpolatePoints = false,
                    TrackerFormatString = "{0}\nTime: {2:yyyy-MM-dd HH:mm:ss}\nValue: {4:0.###}"
                };

                foreach (var point in line.Points)
                {
                    detailedSeries.Points.Add(point);
                }

                detailed.Series.Add(detailedSeries);
            }

            return detailed;
        }

        private void LatencyChartBorder_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => OpenPlotInWindow($"Latency - Detailed View ({_viewModel.ActiveRoomDescription} • {_viewModel.ActiveRangeDescription})", _viewModel.LatencyPlot);

        private void BandwidthChartBorder_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => OpenPlotInWindow($"Bandwidth - Detailed View ({_viewModel.ActiveRoomDescription} • {_viewModel.ActiveRangeDescription})", _viewModel.BandwidthPlot);

        private void PacketLossChartBorder_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => OpenPlotInWindow($"Packet Loss - Detailed View ({_viewModel.ActiveRoomDescription} • {_viewModel.ActiveRangeDescription})", _viewModel.PacketLossPlot);
    }
}
