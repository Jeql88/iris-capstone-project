using System;
using System.Runtime.InteropServices;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Win32;
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
        private readonly IPrivilegedHelperClient _helperClient;

        private static readonly SemaphoreSlim _idleCheckLock = new(1, 1);
        private static DateTime _lastResumeTimestamp = DateTime.MinValue;
        private static readonly TimeSpan _resumeDebounceWindow = TimeSpan.FromSeconds(10);
        private string? _macAddress;

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

        public AgentWorker(IConfiguration configuration, IPrivilegedHelperClient helperClient)
        {
            _configuration = configuration;
            _helperClient = helperClient;
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
            var freezeAutoUnfreezeMinutes = int.TryParse(_configuration["AgentSettings:FreezeAutoUnfreezeMinutes"], out var fum) ? fum : 10;

            var monitoringLogic = new MonitoringLogic(
                _context,
                networkInfo.MacAddress,
                pingHost,
                pingTimeout,
                options,
                freezeAutoUnfreezeMinutes,
                _helperClient);
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
                var wallpaperEnforcer = new WallpaperPolicyEnforcer(_context, networkInfo.MacAddress);

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
                _policyTimer = new System.Threading.Timer(async _ => await CheckPoliciesAsync(_context, networkInfo.MacAddress, _helperClient), null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
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

            // Store MAC for use in resume handler and restore sleep settings
            _macAddress = networkInfo.MacAddress;
            await RestoreSleepSettingsAsync();
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            Log.Information("Subscribed to system PowerModeChanged events for idle enforcement on resume.");

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
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
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

        private static async Task CheckPoliciesAsync(IRISDbContext context, string macAddress, IPrivilegedHelperClient helperClient)
        {
            if (!await _idleCheckLock.WaitAsync(0))
                return; // Another check is already in progress

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
                    foreach (var policy in pc.Room.Policies.Where(p => p.IsActive))
                    {
                        if (policy.AutoShutdownIdleMinutes.HasValue)
                        {
                            await CheckIdleShutdownAsync(policy.AutoShutdownIdleMinutes.Value, helperClient);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Failed to check policies: {Message}", ex.Message);
            }
            finally
            {
                _idleCheckLock.Release();
            }
        }

        private static async Task CheckIdleShutdownAsync(int idleMinutes, IPrivilegedHelperClient helperClient)
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
                    try
                    {
                        await helperClient.ForceShutdownAsync(0);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to execute idle-policy shutdown via helper.");
                    }
                }
            }
        }

        private static TimeSpan GetIdleTime()
        {
            var lastInputInfo = new NativeMethods.LASTINPUTINFO();
            lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);

            if (NativeMethods.GetLastInputInfo(ref lastInputInfo))
            {
                var idleTime = unchecked((uint)Environment.TickCount - lastInputInfo.dwTime);
                return TimeSpan.FromMilliseconds(idleTime);
            }

            return TimeSpan.Zero;
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume)
            {
                Log.Information("System resumed from sleep/hibernate. Scheduling idle policy check.");

                var now = DateTime.UtcNow;
                if ((now - _lastResumeTimestamp) < _resumeDebounceWindow)
                {
                    Log.Information("Resume event debounced (within {Seconds}s of last resume).", _resumeDebounceWindow.TotalSeconds);
                    return;
                }
                _lastResumeTimestamp = now;

                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(5));
                    await RunIdleCheckOnResumeAsync();
                });
            }
            else if (e.Mode == PowerModes.Suspend)
            {
                Log.Information("System entering sleep/hibernate.");
            }
        }

        private async Task RunIdleCheckOnResumeAsync()
        {
            if (string.IsNullOrEmpty(_macAddress) || _context == null)
                return;

            try
            {
                await CheckPoliciesAsync(_context, _macAddress, _helperClient);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to run idle check on system resume.");
            }
        }

        private async Task RestoreSleepSettingsAsync()
        {
            try
            {
                await _helperClient.SetSleepTimeoutsAsync(30, 15);
                Log.Information("Restored default sleep timeouts (AC=30min, DC=15min) via helper.");
            }
            catch (HelperUnavailableException)
            {
                Log.Warning("Helper unavailable — could not restore sleep settings. Will retry on next resume.");
            }
            catch (Exception ex)
            {
                Log.Warning("Failed to restore sleep settings: {Message}", ex.Message);
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

    }
}
