using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using IRIS.Core.Data;
using IRIS.Core.Services;
using IRIS.Core.Services.Contracts;
using IRIS.UI.Views.Shared;
using IRIS.UI.Views.Admin;
using IRIS.UI.Views.Personnel;
using IRIS.UI.Views.Faculty;
using IRIS.UI.Views.Common;
using IRIS.UI.ViewModels;
using IRIS.UI.Services;
using IRIS.UI.Services.Contracts;

namespace IRIS.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private IServiceProvider? _serviceProvider;
        private IWallpaperFileServer? _wallpaperFileServer;
        private DataRetentionBackgroundService? _dataRetentionService;
        private MonitoringBackgroundService? _monitoringService;
        private AutoShutdownEnforcementService? _autoShutdownService;
        private CancellationTokenSource? _appCts;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Add global exception handlers
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            _serviceProvider = serviceCollection.BuildServiceProvider();

            _wallpaperFileServer = _serviceProvider.GetRequiredService<IWallpaperFileServer>();
            _wallpaperFileServer.Start();

            // Start the data retention background cleanup service
            _appCts = new CancellationTokenSource();
            _dataRetentionService = _serviceProvider.GetRequiredService<DataRetentionBackgroundService>();
            _ = _dataRetentionService.StartAsync(_appCts.Token);

            _monitoringService = _serviceProvider.GetRequiredService<MonitoringBackgroundService>();
            _ = _monitoringService.StartAsync(_appCts.Token);

            _autoShutdownService = _serviceProvider.GetRequiredService<AutoShutdownEnforcementService>();
            _ = _autoShutdownService.StartAsync(_appCts.Token);

            // Show login window only
            var loginWindow = _serviceProvider.GetRequiredService<LoginWindow>();
            MainWindow = loginWindow;
            loginWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                _appCts?.Cancel();
                _monitoringService?.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
                _autoShutdownService?.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
                _dataRetentionService?.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
            }
            catch { /* Ignore shutdown errors from background services */ }

            try
            {
                _wallpaperFileServer?.StopAsync().GetAwaiter().GetResult();
            }
            catch
            {
                // Ignore shutdown errors from wallpaper file server.
            }

            _appCts?.Dispose();
            base.OnExit(e);
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            var logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
            var errorText = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] UNHANDLED DISPATCHER EXCEPTION\n" +
                            $"  Type: {e.Exception.GetType().FullName}\n" +
                            $"  Message: {e.Exception.Message}\n" +
                            $"  Inner: {e.Exception.InnerException?.Message}\n" +
                            $"  InnerInner: {e.Exception.InnerException?.InnerException?.Message}\n" +
                            $"  StackTrace:\n{e.Exception.StackTrace}\n" +
                            $"  InnerStackTrace:\n{e.Exception.InnerException?.StackTrace}\n\n";
            System.IO.File.AppendAllText(logPath, errorText);
            MessageBox.Show($"An unexpected error occurred:\n\n{e.Exception.Message}\n\nInner: {e.Exception.InnerException?.Message}\n\nLogged to: {logPath}", 
                "Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                MessageBox.Show($"A critical error occurred:\n\n{ex.Message}\n\n{ex.StackTrace}", 
                    "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        public IServiceProvider GetServiceProvider() => _serviceProvider!;

        private void ConfigureServices(IServiceCollection services)
        {
            // Configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            services.AddSingleton<IConfiguration>(configuration);

            // Logging
            services.AddLogging();

            // Database
            services.AddDbContext<IRISDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("IRISDatabase")));

            // Services (using Contracts namespace)
            services.AddScoped<IAuthenticationService, AuthenticationService>();
            services.AddScoped<IUserManagementService, UserManagementService>();
            services.AddScoped<IAccessLogsService, AccessLogsService>();
            services.AddScoped<IMonitoringService, MonitoringService>();
            services.AddScoped<IRoomService, RoomService>();
            services.AddScoped<IPCAdminService, PCAdminService>();
            services.AddScoped<IPolicyService, PolicyService>();
            services.AddScoped<IUsageMetricsService, UsageMetricsService>();
            services.AddScoped<IApplicationUsageService, ApplicationUsageService>();
            services.AddScoped<IDeploymentDataService, DeploymentDataService>();
            services.AddScoped<IDataRetentionService, DataRetentionService>();
            services.AddSingleton<IPowerCommandQueueService, PowerCommandQueueService>();
            services.AddSingleton<IWallpaperFileServer, WallpaperFileServer>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<IPCDataCacheService, PCDataCacheService>();
            services.AddSingleton<DataRetentionBackgroundService>();
            services.AddSingleton<MonitoringBackgroundService>();
            services.AddSingleton<IWakeOnLanService, WakeOnLanService>();
            services.AddSingleton<ILocalMachineIdentityService, LocalMachineIdentityService>();
            services.AddSingleton<AutoShutdownEnforcementService>();

            // ViewModels
            services.AddTransient<LoginViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<MonitorViewModel>();
            services.AddTransient<ViewScreenViewModel>();
            services.AddTransient<FileManagementViewModel>();
            services.AddTransient<PolicyEnforcementViewModel>();
            services.AddTransient<LabsViewModel>();
            services.AddTransient<UsageMetricsViewModel>();
            services.AddTransient<AlertsViewModel>();
            services.AddTransient<UserManagementViewModel>();
            services.AddTransient<AccessLogsViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<NetworkAnalyticsViewModel>();

            // Views - Shared
            services.AddTransient<LoginWindow>();
            services.AddTransient<MainWindow>();
            
            // Views - Common
            services.AddTransient<DashboardView>();
            services.AddTransient(sp => new AccessLogsView(sp.GetRequiredService<AccessLogsViewModel>()));
            services.AddTransient(sp => new UsageMetricsView(sp.GetRequiredService<UsageMetricsViewModel>()));
            services.AddTransient(sp => new AlertsView(sp.GetRequiredService<AlertsViewModel>()));
            services.AddTransient(sp => new SettingsView(sp.GetRequiredService<SettingsViewModel>(), sp.GetRequiredService<IAuthenticationService>()));
            services.AddTransient(sp => new NetworkAnalyticsView(sp.GetRequiredService<NetworkAnalyticsViewModel>()));
            
            // Views - Admin
            services.AddTransient(sp => new UserManagementView(sp.GetRequiredService<UserManagementViewModel>(), sp.GetRequiredService<IUserManagementService>()));
            services.AddTransient(sp => new PolicyEnforcementView(sp.GetRequiredService<PolicyEnforcementViewModel>()));
            services.AddTransient(sp => new LabsView(sp.GetRequiredService<LabsViewModel>()));
            
            // Views - Personnel
            services.AddTransient(sp => new MonitorView(sp.GetRequiredService<MonitorViewModel>()));
            services.AddTransient(sp => new FileManagementView(sp.GetRequiredService<FileManagementViewModel>()));
            services.AddTransient<PersonnelDashboardView>();
            services.AddTransient<PersonnelMainWindow>();
            
            // Views - Faculty
            services.AddTransient(sp => new ViewScreenPage(sp.GetRequiredService<ViewScreenViewModel>()));
            services.AddTransient<FacultyDashboardView>();
            services.AddTransient<FacultyMainWindow>();
        }
    }
}

