using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serilog;
using IRIS.Core.Data;
using IRIS.Core.DTOs;
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
                .MinimumLevel.Debug()
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
            var networkInfo = PCLogic.GetNetworkInfo();

            // Initialize monitoring components
            var pingHost = configuration["AgentSettings:PingHost"] ?? "8.8.8.8";
            var pingTimeout = int.TryParse(configuration["AgentSettings:PingTimeoutMs"], out var pto) ? pto : 1000;
            var commandServerHost = configuration["AgentSettings:CommandServerHost"] ?? "127.0.0.1";
            var commandServerPort = int.TryParse(configuration["AgentSettings:CommandServerPort"], out var csp) ? csp : 5091;

            var monitoringLogic = new MonitoringLogic(context, networkInfo.MacAddress, pingHost, pingTimeout, commandServerHost, commandServerPort);
            var monitoringController = new MonitoringController(monitoringLogic, configuration);
            var screenStreamPort = int.TryParse(configuration["AgentSettings:ScreenStreamPort"], out var ssp) ? ssp : 5057;
            var snapshotMaxWidth = int.TryParse(configuration["AgentSettings:SnapshotMaxWidth"], out var smw) ? smw : 1280;
            snapshotMaxWidth = Math.Clamp(snapshotMaxWidth, 640, 1920);
            var snapshotJpegQuality = int.TryParse(configuration["AgentSettings:SnapshotJpegQuality"], out var sjq) ? sjq : 75;
            snapshotJpegQuality = Math.Clamp(snapshotJpegQuality, 30, 90);
            var streamToken = configuration["AgentSettings:ScreenStreamToken"];
            var allowedSourceIpEntries = (configuration["AgentSettings:AllowedSnapshotSourceIps"] ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
            var autoAllowLocalSubnet = allowedSourceIpEntries.Any(x => x.Equals("auto", StringComparison.OrdinalIgnoreCase));
            var selfOnlyMode = allowedSourceIpEntries.Any(x => x.Equals("self", StringComparison.OrdinalIgnoreCase));
            var controllerDiscoveryMode = allowedSourceIpEntries.Any(x =>
                x.Equals("core", StringComparison.OrdinalIgnoreCase) ||
                x.Equals("controller", StringComparison.OrdinalIgnoreCase));

            var allowedSourceIps = allowedSourceIpEntries
                .Where(x => !x.Equals("auto", StringComparison.OrdinalIgnoreCase))
                .Where(x => !x.Equals("self", StringComparison.OrdinalIgnoreCase))
                .Where(x => !x.Equals("core", StringComparison.OrdinalIgnoreCase))
                .Where(x => !x.Equals("controller", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (selfOnlyMode)
            {
                allowedSourceIps = allowedSourceIps
                    .Concat(GetLocalHostIpv4Addresses())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            using var snapshotServer = new ScreenSnapshotServer(
                screenStreamPort,
                snapshotMaxWidth,
                snapshotJpegQuality,
                streamToken,
                allowedSourceIps,
                autoAllowLocalSubnet,
                controllerDiscoveryMode);

            // Execute startup logic: Register PC
            await pcController.RegisterPCAsync();

            // Initialize wallpaper policy enforcer
            var wallpaperEnforcer = new WallpaperPolicyEnforcer(context, networkInfo.MacAddress);

            // Enforce wallpaper policy on startup
            await wallpaperEnforcer.EnforceWallpaperPolicyAsync();

            // Initialize application usage tracking with separate context
            var appUsageOptions = new DbContextOptionsBuilder<IRISDbContext>()
                .UseNpgsql(configuration.GetConnectionString("IRISDatabase"))
                .Options;
            var appUsageContext = new IRISDbContext(appUsageOptions);
            var appUsageLogic = new ApplicationUsageLogic(appUsageContext, networkInfo.MacAddress);
            await appUsageLogic.StartMonitoringAsync();

            var websiteUsageCollectInterval = int.TryParse(configuration["AgentSettings:WebsiteCollectIntervalSeconds"], out var wcis) ? wcis : 120;
            var websiteUsageSyncInterval = int.TryParse(configuration["AgentSettings:WebsiteSyncIntervalSeconds"], out var wsis) ? wsis : 60;
            var websiteUsageBucketMinutes = int.TryParse(configuration["AgentSettings:WebsiteBucketMinutes"], out var wbm) ? wbm : 5;

            var websiteUsageOptions = new DbContextOptionsBuilder<IRISDbContext>()
                .UseNpgsql(configuration.GetConnectionString("IRISDatabase"))
                .Options;
            var websiteUsageContext = new IRISDbContext(websiteUsageOptions);
            var websiteUsageLogic = new WebsiteUsageLogic(
                websiteUsageContext,
                networkInfo.MacAddress,
                websiteUsageCollectInterval,
                websiteUsageSyncInterval,
                websiteUsageBucketMinutes);
            Log.Information("Initializing website usage monitoring...");
            try
            {
                await websiteUsageLogic.StartMonitoringAsync();
                Log.Information("Website usage monitoring initialization completed.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Website usage monitoring failed to initialize");
            }

            // Start monitoring loop
            await monitoringController.StartMonitoringAsync();
            try
            {
                await snapshotServer.StartAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Screen snapshot server failed to start. Agent will continue without snapshot streaming.");
            }

            // Start policy enforcement
            var policyTimer = new System.Threading.Timer(async _ => await CheckPoliciesAsync(context, networkInfo.MacAddress), null, TimeSpan.Zero, TimeSpan.FromSeconds(30));

            Log.Information("Agent initialized successfully. Monitoring loop started.");

            // Set up shutdown handling
            var shutdownLogic = new ShutdownLogic(context, networkInfo.MacAddress);
            AppDomain.CurrentDomain.ProcessExit += async (sender, e) =>
            {
                Log.Information("Shutdown detected. Handling final update...");
                await websiteUsageLogic.StopMonitoringAsync();
                await shutdownLogic.HandleShutdownAsync();
                websiteUsageLogic.Dispose();
                websiteUsageContext.Dispose();
                Log.CloseAndFlush();
            };

            Console.CancelKeyPress += async (sender, e) =>
            {
                e.Cancel = true; // Prevent immediate exit
                Log.Information("Ctrl+C detected. Handling shutdown...");
                await monitoringController.StopMonitoringAsync();
                await snapshotServer.StopAsync();
                await appUsageLogic.StopMonitoringAsync();
                await websiteUsageLogic.StopMonitoringAsync();
                await shutdownLogic.HandleShutdownAsync();
                appUsageLogic.Dispose();
                appUsageContext.Dispose();
                websiteUsageLogic.Dispose();
                websiteUsageContext.Dispose();
                Log.CloseAndFlush();
                Environment.Exit(0);
            };

            // Keep the application running
            await Task.Delay(Timeout.Infinite);
        }

        [DllImport("user32.dll")]
        static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [StructLayout(LayoutKind.Sequential)]
        struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        private static async Task CheckPoliciesAsync(IRISDbContext context, string macAddress)
        {
            try
            {
                var optionsBuilder = new DbContextOptionsBuilder<IRISDbContext>()
                    .UseNpgsql(context.Database.GetConnectionString())
                    .Options;

                using var policyContext = new IRISDbContext(optionsBuilder);
                var pc = await policyContext.PCs
                    .Include(p => p.Room)
                    .ThenInclude(r => r.Policies)
                    .FirstOrDefaultAsync(p => p.MacAddress == macAddress);

                if (pc?.Room?.Policies != null)
                {
                    // Initialize wallpaper enforcer for periodic checks - COMMENTED OUT: Only enforce on startup
                    // var wallpaperEnforcer = new WallpaperPolicyEnforcer(policyContext, macAddress);

                    foreach (var policy in pc.Room.Policies.Where(p => p.IsActive))
                    {
                        // Check wallpaper compliance - COMMENTED OUT: Only enforce on startup
                        // if (policy.ResetWallpaperOnStartup)
                        // {
                        //     await wallpaperEnforcer.CheckAndEnforceWallpaperComplianceAsync();
                        // }

                        // Check auto-shutdown policy
                        if (policy.AutoShutdownIdleMinutes.HasValue)
                        {
                            await CheckIdleShutdownAsync(policy.AutoShutdownIdleMinutes.Value);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to check policies: {ex.Message}");
            }
        }

        private static Task CheckIdleShutdownAsync(int idleMinutes)
        {
            var idleTime = GetIdleTime();
            Log.Information($"Idle time: {idleTime.TotalMinutes:F1} minutes, threshold: {idleMinutes} minutes");

            if (idleTime.TotalMinutes >= idleMinutes)
            {
                Log.Warning($"PC has been idle for {idleTime.TotalMinutes:F1} minutes. Shutting down...");
                Process.Start("shutdown", "/s /t 10 /c \"Auto-shutdown due to idle time policy\"");
            }
            return Task.CompletedTask;
        }

        private static TimeSpan GetIdleTime()
        {
            var lastInputInfo = new LASTINPUTINFO();
            lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);

            if (GetLastInputInfo(ref lastInputInfo))
            {
                var idleTime = Environment.TickCount - lastInputInfo.dwTime;
                return TimeSpan.FromMilliseconds(idleTime);
            }

            return TimeSpan.Zero;
        }

        private static IEnumerable<string> GetLocalHostIpv4Addresses()
        {
            var ips = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "127.0.0.1"
            };

            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                var properties = nic.GetIPProperties();
                foreach (var unicast in properties.UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        ips.Add(unicast.Address.ToString());
                    }
                }
            }

            return ips;
        }
    }
}
