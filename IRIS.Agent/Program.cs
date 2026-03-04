using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
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
            var monitoringLogic = new MonitoringLogic(context, networkInfo.MacAddress);
            var monitoringController = new MonitoringController(monitoringLogic, configuration);

            // Execute startup logic: Register PC
            await pcController.RegisterPCAsync();

            // Initialize wallpaper policy enforcer
            var wallpaperEnforcer = new WallpaperPolicyEnforcer(context, networkInfo.MacAddress);
            
            // Enforce wallpaper policy on startup
            await wallpaperEnforcer.EnforceWallpaperPolicyAsync();

            // Start monitoring loop
            await monitoringController.StartMonitoringAsync();

            // Start policy enforcement
            var policyTimer = new Timer(async _ => await CheckPoliciesAsync(context, networkInfo.MacAddress), null, TimeSpan.Zero, TimeSpan.FromSeconds(30));

            Log.Information("Agent initialized successfully. Monitoring loop started.");

            // Set up shutdown handling
            var shutdownLogic = new ShutdownLogic(context, networkInfo.MacAddress);
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

        [DllImport("user32.dll", SetLastError = true)]
        static extern int MessageBoxTimeout(IntPtr hWnd, string lpText, string lpCaption, uint uType, ushort wLanguageId, uint dwMilliseconds);

        private const uint MB_OKCANCEL = 1;
        private const uint MB_ICONWARNING = 0x30;
        private const int IDCANCEL = 2;

        private static Task CheckIdleShutdownAsync(int idleMinutes)
        {
            var idleTime = GetIdleTime();
            Log.Information($"Idle time: {idleTime.TotalMinutes:F1} minutes, threshold: {idleMinutes} minutes");
            
            if (idleTime.TotalMinutes >= idleMinutes)
            {
                Log.Warning($"PC has been idle for {idleTime.TotalMinutes:F1} minutes. Showing shutdown warning...");
                Task.Run(() =>
                {
                    var result = MessageBoxTimeout(IntPtr.Zero, "You're about to be signed out\n\nAuto-shutdown due to idle time policy\n\nClick Cancel to prevent shutdown", "Auto-Shutdown Warning", MB_OKCANCEL | MB_ICONWARNING, 0, 15000);
                    if (result != IDCANCEL)
                    {
                        Process.Start("shutdown", "/s /t 0");
                    }
                });
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
    }
}
