using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.IO;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using IRIS.Core.Data;
using IRIS.Core.DTOs;
using IRIS.Agent.Controllers;
using IRIS.Agent.Logic;

namespace IRIS.Agent
{
    /// <summary>
    /// Core agent logic wrapped as a BackgroundService so it works both
    /// as an interactive console app and as a Windows Service.
    /// </summary>
    internal sealed class AgentWorker : BackgroundService
    {
        private readonly IConfiguration _configuration;

        private static bool _sleepDisabledForIdleShutdown;

        // Disposables managed by this worker
        private IRISDbContext? _context;
        private IRISDbContext? _appUsageContext;
        private IRISDbContext? _websiteUsageContext;
        private ApplicationUsageLogic? _appUsageLogic;
        private WebsiteUsageLogic? _websiteUsageLogic;
        private ScreenSnapshotServer? _snapshotServer;
        private AgentFileManagementServer? _fileManagementServer;
        private MonitoringController? _monitoringController;
        private ShutdownLogic? _shutdownLogic;
        private System.Threading.Timer? _policyTimer;

        public AgentWorker(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Log.Information("IRIS Agent starting...");

            var startupConfigurator = new AgentStartupConfigurator(_configuration);
            await startupConfigurator.EnsureInitialConfigurationAsync();

            // Build DbContext options
            var options = new DbContextOptionsBuilder<IRISDbContext>()
                .UseNpgsql(_configuration.GetConnectionString("IRISDatabase"))
                .Options;

            // Initialize dependencies
            _context = new IRISDbContext(options);
            var pcLogic = new PCLogic(_context);
            var pcController = new PCController(pcLogic);

            // Get MAC address for monitoring
            var networkInfo = PCLogic.GetNetworkInfo();

            // Initialize monitoring components
            var pingHost = _configuration["AgentSettings:PingHost"] ?? "8.8.8.8";
            var pingTimeout = int.TryParse(_configuration["AgentSettings:PingTimeoutMs"], out var pto) ? pto : 1000;
            var commandServerHost = ResolveCommandServerHost(_configuration);
            var commandServerPort = int.TryParse(_configuration["AgentSettings:CommandServerPort"], out var csp) ? csp : 5091;
            var freezeAutoUnfreezeMinutes = int.TryParse(_configuration["AgentSettings:FreezeAutoUnfreezeMinutes"], out var fum) ? fum : 10;

            Log.Information("Power command polling endpoint configured as {CommandServerHost}:{CommandServerPort}", commandServerHost, commandServerPort);

            var monitoringLogic = new MonitoringLogic(
                _context,
                networkInfo.MacAddress,
                pingHost,
                pingTimeout,
                commandServerHost,
                commandServerPort,
                freezeAutoUnfreezeMinutes);
            _monitoringController = new MonitoringController(monitoringLogic, _configuration);
            var screenStreamPort = int.TryParse(_configuration["AgentSettings:ScreenStreamPort"], out var ssp) ? ssp : 5057;
            var snapshotMaxWidth = int.TryParse(_configuration["AgentSettings:SnapshotMaxWidth"], out var smw) ? smw : 1280;
            snapshotMaxWidth = Math.Clamp(snapshotMaxWidth, 640, 1920);
            var snapshotJpegQuality = int.TryParse(_configuration["AgentSettings:SnapshotJpegQuality"], out var sjq) ? sjq : 75;
            snapshotJpegQuality = Math.Clamp(snapshotJpegQuality, 30, 90);
            var streamToken = _configuration["AgentSettings:ScreenStreamToken"];
            var allowedSourceIpEntries = (_configuration["AgentSettings:AllowedSnapshotSourceIps"] ?? string.Empty)
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

            _snapshotServer = new ScreenSnapshotServer(
                screenStreamPort,
                snapshotMaxWidth,
                snapshotJpegQuality,
                streamToken,
                allowedSourceIps,
                autoAllowLocalSubnet,
                controllerDiscoveryMode);

            var fileApiPort = int.TryParse(_configuration["AgentSettings:FileApiPort"], out var fap) ? fap : 5065;
            var fileApiToken = _configuration["AgentSettings:FileApiToken"] ?? string.Empty;
            var configuredManagedRootPath = _configuration["AgentSettings:ManagedRootPath"];
            var managedRootPath = ResolveManagedRootPath(configuredManagedRootPath);

            Log.Information("File management root path: {ManagedRootPath}", managedRootPath);
            _fileManagementServer = new AgentFileManagementServer(fileApiPort, managedRootPath, fileApiToken);

            try
            {
                await _snapshotServer.StartAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Screen snapshot server failed to start. Agent will continue without snapshot streaming.");
            }

            try
            {
                await _fileManagementServer.StartAsync();
            }
            catch (System.Net.HttpListenerException ex) when (ex.ErrorCode == 5)
            {
                var currentUser = Environment.UserDomainName + "\\" + Environment.UserName;
                Log.Error(ex,
                    "File management API failed to start due to URL ACL permissions on port {Port}. " +
                    "Reserve URL ACL with: netsh http add urlacl url=http://+:{UrlAclPort}/ user={CurrentUser}",
                    fileApiPort,
                    fileApiPort,
                    currentUser);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "File management API failed to start. Agent will continue without file API.");
            }

            var databaseStartupReady = true;

            try
            {
                // Execute startup logic: Register PC
                await pcController.RegisterPCAsync();

                // Initialize wallpaper policy enforcer
                var wallpaperEnforcer = new WallpaperPolicyEnforcer(_context, networkInfo.MacAddress, _configuration);

                // Enforce wallpaper policy on startup
                await wallpaperEnforcer.EnforceWallpaperPolicyAsync();

                // Initialize application usage tracking with separate context
                var appUsageOptions = new DbContextOptionsBuilder<IRISDbContext>()
                    .UseNpgsql(_configuration.GetConnectionString("IRISDatabase"))
                    .Options;
                _appUsageContext = new IRISDbContext(appUsageOptions);
                _appUsageLogic = new ApplicationUsageLogic(_appUsageContext, networkInfo.MacAddress);
                await _appUsageLogic.StartMonitoringAsync();

                var websiteUsageCollectInterval = int.TryParse(_configuration["AgentSettings:WebsiteCollectIntervalSeconds"], out var wcis) ? wcis : 120;
                var websiteUsageSyncInterval = int.TryParse(_configuration["AgentSettings:WebsiteSyncIntervalSeconds"], out var wsis) ? wsis : 60;
                var websiteUsageBucketMinutes = int.TryParse(_configuration["AgentSettings:WebsiteBucketMinutes"], out var wbm) ? wbm : 5;

                var websiteUsageOptions = new DbContextOptionsBuilder<IRISDbContext>()
                    .UseNpgsql(_configuration.GetConnectionString("IRISDatabase"))
                    .Options;
                _websiteUsageContext = new IRISDbContext(websiteUsageOptions);
                _websiteUsageLogic = new WebsiteUsageLogic(
                    _websiteUsageContext,
                    networkInfo.MacAddress,
                    websiteUsageCollectInterval,
                    websiteUsageSyncInterval,
                    websiteUsageBucketMinutes);
                Log.Information("Initializing website usage monitoring...");
                try
                {
                    await _websiteUsageLogic.StartMonitoringAsync();
                    Log.Information("Website usage monitoring initialization completed.");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Website usage monitoring failed to initialize");
                }

                // Start policy enforcement
                _policyTimer = new System.Threading.Timer(async _ => await CheckPoliciesAsync(_context, networkInfo.MacAddress), null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
                _shutdownLogic = new ShutdownLogic(_context, networkInfo.MacAddress);
            }
            catch (Exception ex)
            {
                databaseStartupReady = false;
                Log.Error(ex, "Database-dependent startup failed. Agent will continue running snapshot/file endpoints and command polling in degraded mode.");
            }

            // Start monitoring loop
            await _monitoringController.StartMonitoringAsync();

            if (databaseStartupReady)
            {
                Log.Information("Agent initialized successfully. Monitoring loop started.");
            }
            else
            {
                Log.Warning("Agent initialized in degraded mode (database unreachable). Snapshot and file endpoints may still be available.");
            }

            // Wait until the host signals shutdown
            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when the service is stopping
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            Log.Information("Agent stopping...");
            _policyTimer?.Dispose();

            if (_monitoringController != null)
                await _monitoringController.StopMonitoringAsync();

            if (_snapshotServer != null)
                await _snapshotServer.StopAsync();

            if (_fileManagementServer != null)
                await _fileManagementServer.StopAsync();

            if (_appUsageLogic != null)
                await _appUsageLogic.StopMonitoringAsync();

            if (_websiteUsageLogic != null)
                await _websiteUsageLogic.StopMonitoringAsync();

            if (_shutdownLogic != null)
                await _shutdownLogic.HandleShutdownAsync();

            _websiteUsageLogic?.Dispose();
            _websiteUsageContext?.Dispose();
            _appUsageLogic?.Dispose();
            _appUsageContext?.Dispose();
            _snapshotServer?.Dispose();
            _fileManagementServer?.Dispose();
            _context?.Dispose();

            Log.CloseAndFlush();
            await base.StopAsync(cancellationToken);
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
                    var hasIdleShutdownPolicy = false;

                    foreach (var policy in pc.Room.Policies.Where(p => p.IsActive))
                    {
                        // Check auto-shutdown policy
                        if (policy.AutoShutdownIdleMinutes.HasValue)
                        {
                            hasIdleShutdownPolicy = true;
                            await CheckIdleShutdownAsync(policy.AutoShutdownIdleMinutes.Value);
                        }
                    }

                    // Disable sleep when idle shutdown is active so the PC stays awake long enough
                    if (hasIdleShutdownPolicy && !_sleepDisabledForIdleShutdown)
                    {
                        RunPowercfg("-change standby-timeout-ac 0");
                        RunPowercfg("-change standby-timeout-dc 0");
                        _sleepDisabledForIdleShutdown = true;
                        Log.Information("Disabled sleep (set to Never) due to active idle shutdown policy.");
                    }
                    else if (!hasIdleShutdownPolicy && _sleepDisabledForIdleShutdown)
                    {
                        RunPowercfg("-change standby-timeout-ac 30");
                        RunPowercfg("-change standby-timeout-dc 15");
                        _sleepDisabledForIdleShutdown = false;
                        Log.Information("Restored default sleep settings (idle shutdown policy no longer active).");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to check policies: {ex.Message}");
            }
        }

        private static async Task CheckIdleShutdownAsync(int idleMinutes)
        {
            var idleTime = GetIdleTime();
            Log.Information($"Idle time: {idleTime.TotalMinutes:F1} minutes, threshold: {idleMinutes} minutes");

            if (idleTime.TotalMinutes >= idleMinutes)
            {
                Log.Warning($"PC has been idle for {idleTime.TotalMinutes:F1} minutes. Showing shutdown warning...");
                var wasCancelled = await ShutdownWarningDialog.ShowCancelOnlyWarningAsync(
                    "Auto-Shutdown Warning",
                    "This PC will shut down due to idle time policy in 15 seconds.\n\nClick Cancel to prevent shutdown.",
                    15000);

                if (!wasCancelled)
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = Path.Combine(Environment.SystemDirectory, "shutdown.exe"),
                        Arguments = "/s /t 0",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    Process.Start(psi);
                }
            }
        }

        private static TimeSpan GetIdleTime()
        {
            var lastInputInfo = new NativeMethods.LASTINPUTINFO();
            lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);

            if (NativeMethods.GetLastInputInfo(ref lastInputInfo))
            {
                var idleTime = Environment.TickCount - lastInputInfo.dwTime;
                return TimeSpan.FromMilliseconds(idleTime);
            }

            return TimeSpan.Zero;
        }

        private static void RunPowercfg(string arguments)
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (process != null && process.WaitForExit(5000) && process.ExitCode != 0)
                {
                    Log.Warning("powercfg {Arguments} exited with code {ExitCode}", arguments, process.ExitCode);
                }
            }
            catch (Exception ex)
            {
                Log.Warning("Failed to run powercfg {Arguments}: {Message}", arguments, ex.Message);
            }
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

        private static string ResolveManagedRootPath(string? configuredManagedRootPath)
        {
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var fallbackPath = @"C:\IRIS\Managed";

            if (string.IsNullOrWhiteSpace(configuredManagedRootPath))
            {
                return string.IsNullOrWhiteSpace(desktopPath) ? fallbackPath : desktopPath;
            }

            var replacedDesktopToken = configuredManagedRootPath
                .Replace("%DESKTOP%", desktopPath, StringComparison.OrdinalIgnoreCase)
                .Replace("{DESKTOP}", desktopPath, StringComparison.OrdinalIgnoreCase);

            var expanded = Environment.ExpandEnvironmentVariables(replacedDesktopToken).Trim();
            if (string.IsNullOrWhiteSpace(expanded))
            {
                return string.IsNullOrWhiteSpace(desktopPath) ? fallbackPath : desktopPath;
            }

            var fullExpanded = Path.GetFullPath(expanded);
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(userProfile) && !string.IsNullOrWhiteSpace(desktopPath))
            {
                var profileDesktop = Path.GetFullPath(Path.Combine(userProfile, "Desktop"));
                if (string.Equals(fullExpanded, profileDesktop, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(fullExpanded, desktopPath, StringComparison.OrdinalIgnoreCase))
                {
                    Log.Warning("ManagedRootPath resolved to profile Desktop ({ProfileDesktop}) but known Desktop is {KnownDesktop}. Using known Desktop.", profileDesktop, desktopPath);
                    return desktopPath;
                }
            }

            return fullExpanded;
        }

        private static string ResolveCommandServerHost(IConfiguration configuration)
        {
            var configuredHost = (configuration["AgentSettings:CommandServerHost"] ?? string.Empty).Trim();

            // Explicit non-loopback host wins.
            if (!string.IsNullOrWhiteSpace(configuredHost) &&
                !configuredHost.Equals("auto", StringComparison.OrdinalIgnoreCase) &&
                !IsLoopbackHost(configuredHost))
            {
                return configuredHost;
            }

            var dbConnectionString = configuration.GetConnectionString("IRISDatabase");
            if (!string.IsNullOrWhiteSpace(dbConnectionString))
            {
                try
                {
                    var builder = new NpgsqlConnectionStringBuilder(dbConnectionString);
                    var dbHost = (builder.Host ?? string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(dbHost))
                    {
                        var firstHost = dbHost.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .FirstOrDefault();

                        if (!string.IsNullOrWhiteSpace(firstHost) && !IsLoopbackHost(firstHost))
                        {
                            Log.Information("Resolved command server host from IRIS database host: {ResolvedHost}", firstHost);
                            return firstHost;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to parse IRIS database connection string for command server host resolution");
                }
            }

            if (!string.IsNullOrWhiteSpace(configuredHost) && !configuredHost.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                return configuredHost;
            }

            return "127.0.0.1";
        }

        private static bool IsLoopbackHost(string host)
        {
            return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
                || host.Equals("::1", StringComparison.OrdinalIgnoreCase);
        }
    }
}
