using System.Diagnostics;
using Serilog;

namespace IRIS.Agent.Helper
{
    /// <summary>
    /// Performs privileged operations on behalf of user-mode agents. Runs in the helper
    /// process which is started by Task Scheduler as LocalSystem, so every call here has
    /// administrative rights unconditionally.
    /// </summary>
    internal static class AdminOperations
    {
        public static HelperResponse SetSleepTimeouts(int acMinutes, int dcMinutes)
        {
            try
            {
                var acResult = RunCommand("powercfg", $"-change standby-timeout-ac {acMinutes}");
                var dcResult = RunCommand("powercfg", $"-change standby-timeout-dc {dcMinutes}");

                if (acResult.ExitCode != 0 || dcResult.ExitCode != 0)
                {
                    var msg = $"powercfg failed. AC exit={acResult.ExitCode}: {acResult.Output.Trim()}; DC exit={dcResult.ExitCode}: {dcResult.Output.Trim()}";
                    Log.Warning(msg);
                    return new HelperResponse(false, msg);
                }

                Log.Information("Sleep timeouts set (AC={AcMinutes}min, DC={DcMinutes}min).", acMinutes, dcMinutes);
                return new HelperResponse(true);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "SetSleepTimeouts failed.");
                return new HelperResponse(false, ex.Message);
            }
        }

        public static HelperResponse ForceShutdown(int delaySeconds)
        {
            try
            {
                var result = RunCommand("shutdown", $"/s /f /t {Math.Max(0, delaySeconds)}");
                if (result.ExitCode != 0)
                {
                    Log.Warning("shutdown /s /f failed. Exit={ExitCode}: {Output}", result.ExitCode, result.Output.Trim());
                    return new HelperResponse(false, result.Output.Trim());
                }

                Log.Information("Forced shutdown scheduled in {Delay}s.", delaySeconds);
                return new HelperResponse(true);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ForceShutdown failed.");
                return new HelperResponse(false, ex.Message);
            }
        }

        public static HelperResponse ForceRestart(int delaySeconds)
        {
            try
            {
                var result = RunCommand("shutdown", $"/r /f /t {Math.Max(0, delaySeconds)}");
                if (result.ExitCode != 0)
                {
                    Log.Warning("shutdown /r /f failed. Exit={ExitCode}: {Output}", result.ExitCode, result.Output.Trim());
                    return new HelperResponse(false, result.Output.Trim());
                }

                Log.Information("Forced restart scheduled in {Delay}s.", delaySeconds);
                return new HelperResponse(true);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ForceRestart failed.");
                return new HelperResponse(false, ex.Message);
            }
        }

        private static CommandResult RunCommand(string fileName, string arguments)
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.Start();
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(10_000);

            var combined = string.IsNullOrWhiteSpace(stderr)
                ? stdout
                : $"{stdout}{Environment.NewLine}{stderr}";

            return new CommandResult(process.ExitCode, combined);
        }

        private readonly record struct CommandResult(int ExitCode, string Output);
    }
}
