using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace IRIS.Agent
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var isBackground = args.Contains("--background", StringComparer.OrdinalIgnoreCase);

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

                builder.Services.AddHostedService<AgentWorker>();

                var host = builder.Build();
                await host.RunAsync();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "IRIS Agent terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
