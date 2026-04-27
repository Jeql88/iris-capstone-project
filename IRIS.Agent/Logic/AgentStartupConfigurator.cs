using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace IRIS.Agent.Logic
{
    /// <summary>
    /// Passive startup check. All host configuration (firewall rules, URL ACLs, scheduled tasks,
    /// Remote Desktop setup) is performed by the MSI installer. At runtime we only verify the
    /// expected state and log a single warning if something is missing — we never attempt to
    /// mutate firewall / ACL / registry state from the running agent process.
    /// </summary>
    public sealed class AgentStartupConfigurator
    {
        private readonly IConfiguration _configuration;

        public AgentStartupConfigurator(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Task EnsureInitialConfigurationAsync()
        {
            if (!OperatingSystem.IsWindows())
            {
                return Task.CompletedTask;
            }

            var enabled = bool.TryParse(_configuration["AgentSettings:StartupConfigurationEnabled"], out var parsed)
                ? parsed
                : true;

            if (!enabled)
            {
                return Task.CompletedTask;
            }

            var screenPort = int.TryParse(_configuration["AgentSettings:ScreenStreamPort"], out var ssp) ? ssp : 5057;
            var fileApiPort = int.TryParse(_configuration["AgentSettings:FileApiPort"], out var fap) ? fap : 5065;

            var missing = new List<string>();

            // Snapshot server uses a raw TcpListener (RawHttpServer) and does
            // NOT require a URL ACL. Only check the file-API URL ACL, which
            // still rides http.sys via HttpListener.
            if (!UrlAclExists($"http://+:{fileApiPort}/"))
            {
                missing.Add($"URL ACL http://+:{fileApiPort}/");
            }

            if (!FirewallRuleExists($"IRIS Agent Snapshot TCP {screenPort}"))
            {
                missing.Add($"Firewall rule 'IRIS Agent Snapshot TCP {screenPort}'");
            }

            if (!FirewallRuleExists($"IRIS Agent File API TCP {fileApiPort}"))
            {
                missing.Add($"Firewall rule 'IRIS Agent File API TCP {fileApiPort}'");
            }

            if (missing.Count > 0)
            {
                Log.Warning(
                    "IRIS Agent host configuration incomplete. Missing: {Missing}. " +
                    "Re-install via the MSI as Administrator to provision these resources.",
                    string.Join("; ", missing));
            }
            else
            {
                Log.Information("Startup configuration check: already configured.");
            }

            return Task.CompletedTask;
        }

        private static bool UrlAclExists(string url)
        {
            var result = RunCommand("netsh", $"http show urlacl url={url}");
            if (result.ExitCode != 0)
            {
                return false;
            }

            // netsh exits 0 and returns an empty "URL Reservations:" block even for missing ACLs,
            // so the authoritative signal is whether the URL itself appears in the output.
            return result.Output.Contains(url, StringComparison.OrdinalIgnoreCase);
        }

        private static bool FirewallRuleExists(string ruleName)
        {
            var result = RunCommand("netsh", $"advfirewall firewall show rule name=\"{ruleName}\"");
            return result.ExitCode == 0 &&
                   !result.Output.Contains("No rules match", StringComparison.OrdinalIgnoreCase);
        }

        private static CommandResult RunCommand(string fileName, string arguments)
        {
            try
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
                var standardOutput = process.StandardOutput.ReadToEnd();
                var standardError = process.StandardError.ReadToEnd();
                process.WaitForExit();

                var combined = string.IsNullOrWhiteSpace(standardError)
                    ? standardOutput
                    : $"{standardOutput}{Environment.NewLine}{standardError}";

                return new CommandResult(process.ExitCode, combined);
            }
            catch (Exception ex)
            {
                return new CommandResult(-1, ex.Message);
            }
        }

        private readonly record struct CommandResult(int ExitCode, string Output);
    }
}
