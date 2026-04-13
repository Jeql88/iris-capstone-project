using Microsoft.Extensions.Configuration;
using Serilog;

namespace IRIS.Agent.Helper
{
    /// <summary>
    /// Entry point for the SYSTEM-level helper mode (--system-helper).
    /// Runs as LocalSystem via a scheduled task created by the MSI installer.
    /// Responsibilities:
    ///   1) Launch user-mode IRIS.Agent.exe --background in each active interactive session.
    ///   2) Host a named-pipe server for admin operations (powercfg, shutdown/restart).
    /// </summary>
    internal static class SystemHelperEntryPoint
    {
        private const string MutexName = @"Global\IRIS.Agent.SystemHelper";

        public static async Task<int> RunAsync(string[] args)
        {
            // Single-instance guard.
            using var mutex = new Mutex(true, MutexName, out var createdNew);
            if (!createdNew)
            {
                Console.Error.WriteLine("Another IRIS.Agent --system-helper instance is already running.");
                return 1;
            }

            // Load configuration from the same appsettings.json.
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // Configure Serilog for the helper.
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger();

            Log.Information("IRIS Agent System Helper starting.");

            try
            {
                var pipeName = configuration["HelperSettings:PipeName"] ?? "IRIS.Agent.Helper";
                var pipeToken = configuration["HelperSettings:PipeToken"] ?? string.Empty;

                if (string.IsNullOrWhiteSpace(pipeToken) || pipeToken == "CHANGE-ME-INSTALLER-SETS-THIS")
                {
                    Log.Warning("HelperSettings:PipeToken is not configured. Admin operations will reject all requests.");
                }

                var agentExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "IRIS.Agent.exe");

                // Start both subsystems.
                await using var pipeServer = new AdminOperationPipeServer(pipeName, pipeToken);
                await using var sessionSupervisor = new SessionSupervisor(agentExePath);

                pipeServer.Start();
                sessionSupervisor.Start();

                // Wait until the process is asked to stop (SIGTERM / service stop / Ctrl+C).
                using var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (_, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                };

                try
                {
                    await Task.Delay(Timeout.Infinite, cts.Token);
                }
                catch (OperationCanceledException) { /* expected */ }

                Log.Information("IRIS Agent System Helper shutting down.");
                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "IRIS Agent System Helper terminated unexpectedly.");
                return 2;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
