using System.Diagnostics;
using System.Security.Principal;
using IRIS.UI.Services.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IRIS.UI.Services
{
    public sealed class HostFirewallBootstrapService : IHostFirewallBootstrapService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<HostFirewallBootstrapService> _logger;

        public HostFirewallBootstrapService(
            IConfiguration configuration,
            ILogger<HostFirewallBootstrapService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task EnsurePowerCommandRuleAsync(CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            cancellationToken.ThrowIfCancellationRequested();

            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            var ensureRule = bool.TryParse(_configuration["PowerCommandServer:EnsureFirewallRuleOnStartup"], out var enabled)
                ? enabled
                : true;

            if (!ensureRule)
            {
                _logger.LogInformation("Power command host firewall bootstrap disabled by PowerCommandServer:EnsureFirewallRuleOnStartup.");
                return;
            }

            var port = int.TryParse(_configuration["PowerCommandServer:Port"], out var configuredPort)
                ? configuredPort
                : 5091;
            var ruleName = (_configuration["PowerCommandServer:FirewallRuleName"] ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(ruleName))
            {
                ruleName = $"IRIS UI Power Command TCP {port}";
            }

            if (!IsRunningAsAdministrator())
            {
                _logger.LogWarning(
                    "Cannot ensure host firewall rule '{RuleName}' for TCP {Port}: IRIS.UI is not running as Administrator.",
                    ruleName,
                    port);
                return;
            }

            if (FirewallRuleExists(ruleName))
            {
                _logger.LogInformation("Host firewall rule already present: {RuleName}", ruleName);
                return;
            }

            var addRuleResult = RunCommand(
                "netsh",
                $"advfirewall firewall add rule name=\"{ruleName}\" dir=in action=allow protocol=TCP localport={port} profile=private,domain remoteip=localsubnet");

            if (addRuleResult.ExitCode == 0)
            {
                _logger.LogInformation("Host firewall rule ensured: {RuleName} (TCP {Port})", ruleName, port);
                return;
            }

            _logger.LogWarning(
                "Failed to add host firewall rule {RuleName} (TCP {Port}). Output: {Output}",
                ruleName,
                port,
                addRuleResult.Output.Trim());
        }

        private static bool IsRunningAsAdministrator()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                if (identity == null)
                {
                    return false;
                }

                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
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