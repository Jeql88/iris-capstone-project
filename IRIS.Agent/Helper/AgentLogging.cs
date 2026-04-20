using Serilog;
using Serilog.Events;

namespace IRIS.Agent.Helper
{
    internal static class AgentLogging
    {
        public static void Configure(string role)
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "IRIS", "Agent");

            try { Directory.CreateDirectory(logDir); }
            catch { /* fall back to console-only if path is unwritable */ }

            var sessionId = System.Diagnostics.Process.GetCurrentProcess().SessionId;
            var filePath = Path.Combine(logDir, $"{role}-.log");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.WithProperty("Role", role)
                .Enrich.WithProperty("SessionId", sessionId)
                .WriteTo.Console()
                .WriteTo.File(
                    filePath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    shared: true,
                    restrictedToMinimumLevel: LogEventLevel.Debug,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [S{SessionId} {Role}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
        }
    }
}
