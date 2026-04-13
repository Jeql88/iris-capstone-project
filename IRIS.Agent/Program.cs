using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using IRIS.Agent.Logic;

namespace IRIS.Agent
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            // --- System helper mode: runs as SYSTEM, spawns per-session agents, hosts admin pipe ---
            if (args.Contains("--system-helper", StringComparer.OrdinalIgnoreCase))
            {
                return await Helper.SystemHelperEntryPoint.RunAsync(args);
            }

            // --- User-mode agent: runs in the student's session ---

            // No-args or --background both mean background mode.
            var isBackground = args.Length == 0
                || args.Contains("--background", StringComparer.OrdinalIgnoreCase);

            // Session-scoped single-instance guard.
            var sessionId = Process.GetCurrentProcess().SessionId;
            using var mutex = new Mutex(true, $@"Local\IRIS.Agent.UserAgent.{sessionId}", out var createdNew);
            if (!createdNew)
            {
                Console.Error.WriteLine("Another IRIS.Agent instance is already running in this session.");
                return 1;
            }

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger();

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
