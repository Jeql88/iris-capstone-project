using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using IRIS.Agent.Logic;

namespace IRIS.Agent
{
    class Program
    {
        [DllImport("kernel32.dll")]
        private static extern uint WTSGetActiveConsoleSessionId();

        static async Task<int> Main(string[] args)
        {
            // Must run before any WinForms/GDI type loads so Screen.Bounds and
            // CopyFromScreen agree on physical pixels.
            try { NativeMethods.SetProcessDpiAwarenessContext(NativeMethods.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2); }
            catch { /* Pre-1703 Windows: leave default DPI unawareness */ }

            // --- System helper mode: runs as SYSTEM, spawns per-session agents, hosts admin pipe ---
            if (args.Contains("--system-helper", StringComparer.OrdinalIgnoreCase))
            {
                return await Helper.SystemHelperEntryPoint.RunAsync(args);
            }

            // --- User-mode agent: runs in the student's session ---

            // No-args or --background both mean background mode.
            var isBackground = args.Length == 0
                || args.Contains("--background", StringComparer.OrdinalIgnoreCase);

            var currentProcess = Process.GetCurrentProcess();
            var sessionId = currentProcess.SessionId;

            // Session-scoped single-instance guard. Uses Local\ namespace (session-scoped
            // by Windows) plus an explicit session id suffix for defence in depth.
            using var mutex = new Mutex(true, $@"Local\IRIS.Agent.UserAgent.{sessionId}", out var createdNew);
            if (!createdNew)
            {
                Console.Error.WriteLine($"Another IRIS.Agent instance is already running in session {sessionId}.");
                return 1;
            }

            Helper.AgentLogging.Configure("user");

            Log.Information(
                "IRIS.Agent starting: PID={Pid}, SessionId={SessionId}, WTSActiveSession={WtsSession}, UserInteractive={Interactive}, ProcessPath={Path}",
                currentProcess.Id,
                sessionId,
                WTSGetActiveConsoleSessionId(),
                Environment.UserInteractive,
                currentProcess.MainModule?.FileName ?? "<unknown>");

            // Diagnostic: enumerate other IRIS.Agent processes in this session so
            // orphaned/duplicated installs are visible in the log.
            try
            {
                var peers = Process.GetProcessesByName("IRIS.Agent")
                    .Where(p => p.Id != currentProcess.Id && p.SessionId == sessionId)
                    .ToList();
                if (peers.Count > 0)
                {
                    Log.Warning(
                        "Found {Count} other IRIS.Agent process(es) in session {SessionId}: {Pids}",
                        peers.Count,
                        sessionId,
                        string.Join(",", peers.Select(p => p.Id)));
                }
                foreach (var p in peers) p.Dispose();
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Could not enumerate peer IRIS.Agent processes");
            }

            try
            {
                // Hide console window when launched in background mode (e.g. by scheduled task)
                if (isBackground)
                {
                    try
                    {
                        var hwnd = NativeMethods.GetConsoleWindow();
                        if (hwnd != IntPtr.Zero)
                            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_HIDE);
                    }
                    catch { /* Non-critical: ignore if no console window */ }
                }

                var builder = Host.CreateApplicationBuilder(args);

                builder.Configuration
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

                // Disable QuickEdit to prevent console-click freeze (only in interactive/console mode)
                if (Environment.UserInteractive && !isBackground)
                {
                    try
                    {
                        var handle = NativeMethods.GetStdHandle(NativeMethods.STD_INPUT_HANDLE);
                        if (handle != IntPtr.Zero && NativeMethods.GetConsoleMode(handle, out var mode))
                        {
                            mode &= ~NativeMethods.ENABLE_QUICK_EDIT_MODE;
                            mode |= NativeMethods.ENABLE_EXTENDED_FLAGS;
                            NativeMethods.SetConsoleMode(handle, mode);
                        }
                    }
                    catch { /* Non-critical: ignore if no console handle */ }
                }

                // Register services
                builder.Services.AddSingleton<IPrivilegedHelperClient, PrivilegedHelperClient>();
                builder.Services.AddHostedService<AgentWorker>();

                var host = builder.Build();
                await host.RunAsync();
                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "IRIS Agent terminated unexpectedly");
                return 2;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
