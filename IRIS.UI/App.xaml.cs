using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

namespace IRIS.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private IServiceProvider? _serviceProvider;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Add global exception handlers
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            _serviceProvider = serviceCollection.BuildServiceProvider();

            // Show login window only
            var loginWindow = _serviceProvider.GetRequiredService<LoginWindow>();
            loginWindow.Show();
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

            // Database
            services.AddDbContext<IRISDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("IRISDatabase")));

            // Services (using Contracts namespace)
            services.AddScoped<IAuthenticationService, AuthenticationService>();
            services.AddScoped<IUserManagementService, UserManagementService>();
            services.AddScoped<IAccessLogsService, AccessLogsService>();
            services.AddScoped<IMonitoringService, MonitoringService>();
            services.AddScoped<IPolicyService, PolicyService>();
            services.AddScoped<IUsageMetricsService, UsageMetricsService>();
            services.AddScoped<IApplicationUsageService, ApplicationUsageService>();
            services.AddSingleton<INavigationService, NavigationService>();

            // ViewModels
            services.AddTransient<LoginViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<MonitorViewModel>();
            services.AddTransient<ViewScreenViewModel>();
            services.AddTransient<SoftwareManagementViewModel>();
            services.AddTransient<PolicyEnforcementViewModel>();
            services.AddTransient<UsageMetricsViewModel>();
            services.AddTransient<UserManagementViewModel>();
            services.AddTransient<AccessLogsViewModel>();
            services.AddTransient<SettingsViewModel>();

            // Views - Shared
            services.AddTransient<LoginWindow>();
            services.AddTransient<MainWindow>();
            
            // Views - Common
            services.AddTransient<DashboardView>();
            services.AddTransient(sp => new AccessLogsView(sp.GetRequiredService<AccessLogsViewModel>()));
            services.AddTransient(sp => new UsageMetricsView(sp.GetRequiredService<UsageMetricsViewModel>()));
            services.AddTransient(sp => new SettingsView(sp.GetRequiredService<SettingsViewModel>(), sp.GetRequiredService<IAuthenticationService>(), sp.GetRequiredService<INavigationService>()));
            
            // Views - Admin
            services.AddTransient(sp => new UserManagementView(sp.GetRequiredService<UserManagementViewModel>(), sp.GetRequiredService<IUserManagementService>()));
            services.AddTransient(sp => new PolicyEnforcementView(sp.GetRequiredService<PolicyEnforcementViewModel>()));
            
            // Views - Personnel
            services.AddTransient(sp => new MonitorView(sp.GetRequiredService<MonitorViewModel>()));
            services.AddTransient(sp => new SoftwareManagementView(sp.GetRequiredService<SoftwareManagementViewModel>()));
            
            // Views - Faculty
            services.AddTransient(sp => new ViewScreenPage(sp.GetRequiredService<ViewScreenViewModel>()));
        }
    }
}

