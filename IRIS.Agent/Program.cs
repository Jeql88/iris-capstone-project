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
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger();

            try
            {
                var builder = Host.CreateApplicationBuilder(args);

                builder.Configuration
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

                // UseWindowsService() auto-detects: runs as Windows Service when started
                // by SCM, runs as normal console app when started interactively.
                builder.Services.AddWindowsService(options =>
                {
                    options.ServiceName = "IRISAgent";
                });

                // Disable QuickEdit to prevent console-click freeze (only in interactive/console mode)
                if (Environment.UserInteractive)
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
                    catch { /* Non-critical: ignore if no console handle (service mode) */ }
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
