using System;
using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using IRIS.Core.Data;
using IRIS.Core.Services;
using IRIS.UI.Views;
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

            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            _serviceProvider = serviceCollection.BuildServiceProvider();

            // Show login window only
            var loginWindow = _serviceProvider.GetRequiredService<LoginWindow>();
            loginWindow.Show();
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

            // Services
            services.AddScoped<IAuthenticationService, AuthenticationService>();
            services.AddScoped<IUserManagementService, UserManagementService>();
            services.AddScoped<IMonitoringService, MonitoringService>();
            services.AddSingleton<INavigationService, NavigationService>();

            // ViewModels
            services.AddTransient<LoginViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<MonitorViewModel>();
            services.AddTransient<ViewScreenViewModel>();
            services.AddTransient<SoftwareManagementViewModel>();
            services.AddTransient<UserManagementViewModel>();

            // Views
            services.AddTransient<LoginWindow>();
            services.AddTransient<MainWindow>();
            services.AddTransient<DashboardView>();
            services.AddTransient(sp => new MonitorView(sp.GetRequiredService<MonitorViewModel>()));
            services.AddTransient(sp => new ViewScreenPage(sp.GetRequiredService<ViewScreenViewModel>()));
            services.AddTransient(sp => new SoftwareManagementView(sp.GetRequiredService<SoftwareManagementViewModel>()));
            services.AddTransient(sp => new UserManagementView(sp.GetRequiredService<UserManagementViewModel>(), sp.GetRequiredService<IUserManagementService>()));

        }
    }
}

