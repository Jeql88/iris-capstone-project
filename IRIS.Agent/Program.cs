using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serilog;
using IRIS.Core.Data;
using IRIS.Agent.Controllers;
using IRIS.Agent.Logic;

namespace IRIS.Agent
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Load configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // Configure Serilog
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();

            Log.Information("IRIS Agent starting...");

            // Build DbContext options
            var options = new DbContextOptionsBuilder<IRISDbContext>()
                .UseNpgsql(configuration.GetConnectionString("IRISDatabase"))
                .Options;

            // Initialize dependencies
            using var context = new IRISDbContext(options);
            var pcLogic = new PCLogic(context);
            var pcController = new PCController(pcLogic);

            // Get MAC address for monitoring
            var (_, macAddress) = PCLogic.GetNetworkInfo();

            // Initialize monitoring components
            var monitoringLogic = new MonitoringLogic(context, macAddress);
            var monitoringController = new MonitoringController(monitoringLogic, configuration);

            // Execute startup logic: Register PC
            await pcController.RegisterPCAsync();

            // Start monitoring loop
            await monitoringController.StartMonitoringAsync();

            Log.Information("Agent initialized successfully. Monitoring loop started.");

            // Set up shutdown handling
            var shutdownLogic = new ShutdownLogic(context, macAddress);
            AppDomain.CurrentDomain.ProcessExit += async (sender, e) =>
            {
                Log.Information("Shutdown detected. Handling final update...");
                await shutdownLogic.HandleShutdownAsync();
                Log.CloseAndFlush();
            };

            Console.CancelKeyPress += async (sender, e) =>
            {
                e.Cancel = true; // Prevent immediate exit
                Log.Information("Ctrl+C detected. Handling shutdown...");
                await monitoringController.StopMonitoringAsync();
                await shutdownLogic.HandleShutdownAsync();
                Log.CloseAndFlush();
                Environment.Exit(0);
            };

            // Keep the application running
            await Task.Delay(Timeout.Infinite);
        }
    }
}
