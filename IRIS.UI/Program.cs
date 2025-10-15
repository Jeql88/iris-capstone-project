using System;
using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using IRIS.Core.Data;
using IRIS.Core.Services;

namespace IRIS.UI
{
    public partial class App : Application
    {
        private IServiceProvider? _serviceProvider;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            _serviceProvider = serviceCollection.BuildServiceProvider();

            // Show login window
            var loginWindow = _serviceProvider.GetRequiredService<Views.LoginWindow>();
            loginWindow.Show();
        }

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

            // Views
            services.AddTransient<Views.LoginWindow>();
        }
    }

    // Design-time DbContext factory for EF migrations
    public class IRISDbContextFactory : IDesignTimeDbContextFactory<IRISDbContext>
    {
        public IRISDbContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var optionsBuilder = new DbContextOptionsBuilder<IRISDbContext>();
            optionsBuilder.UseNpgsql(configuration.GetConnectionString("IRISDatabase"));

            return new IRISDbContext(optionsBuilder.Options);
        }
    }
}