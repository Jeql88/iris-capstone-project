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
