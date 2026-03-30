using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Windows.Forms;

namespace IRIS.Agent.Logic
{
    public sealed class AgentStartupConfigurator
    {
        private readonly IConfiguration _configuration;

        public AgentStartupConfigurator(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task EnsureInitialConfigurationAsync()
        {
            await Task.CompletedTask;

            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            var startupConfigEnabled = bool.TryParse(_configuration["AgentSettings:StartupConfigurationEnabled"], out var enabled)
                ? enabled
                : true;

            if (!startupConfigEnabled)
            {
                Log.Information("Startup configuration is disabled by AgentSettings:StartupConfigurationEnabled.");
                return;
            }

            var screenPort = int.TryParse(_configuration["AgentSettings:ScreenStreamPort"], out var ssp) ? ssp : 5057;
            var fileApiPort = int.TryParse(_configuration["AgentSettings:FileApiPort"], out var fap) ? fap : 5065;
            var enableRemoteDesktop = bool.TryParse(_configuration["AgentSettings:EnableRemoteDesktopSetup"], out var ers)
                ? ers
                : true;
            var remoteDesktopPort = int.TryParse(_configuration["AgentSettings:RemoteDesktopPort"], out var rdp) ? rdp : 3389;
            var autoApprove = bool.TryParse(_configuration["AgentSettings:AutoApproveInitialConfiguration"], out var aac)
                ? aac
                : false;

            var requiredFirewallRules = new List<FirewallRulePlan>
            {
                new($"IRIS Agent Snapshot TCP {screenPort}", screenPort),
                new($"IRIS Agent File API TCP {fileApiPort}", fileApiPort)
            };

            if (enableRemoteDesktop && remoteDesktopPort != 3389)
            {
                requiredFirewallRules.Add(new($"IRIS Agent RDP TCP {remoteDesktopPort}", remoteDesktopPort));
            }

            var missingFirewallRules = requiredFirewallRules
                .Where(rule => !FirewallRuleExists(rule.Name))
                .ToList();

            var snapshotUrlAcl = $"http://+:{screenPort}/";
            var fileApiUrlAcl = $"http://+:{fileApiPort}/api/";
            var missingUrlAcls = new List<string>();

            if (!UrlAclExists(snapshotUrlAcl))
            {
                missingUrlAcls.Add(snapshotUrlAcl);
            }

            if (!UrlAclExists(fileApiUrlAcl))
            {
                missingUrlAcls.Add(fileApiUrlAcl);
            }

            var needsRemoteDesktopSetup = enableRemoteDesktop && !IsRemoteDesktopReady(remoteDesktopPort);

            if (!missingFirewallRules.Any() && !missingUrlAcls.Any() && !needsRemoteDesktopSetup)
            {
                Log.Information("Startup configuration check: already configured.");
                EnsureScheduledTask();
                return;
            }

            var summary = BuildSummary(missingFirewallRules, missingUrlAcls, needsRemoteDesktopSetup);
            var allowSetup = autoApprove || PromptUserToConfigure(summary);
            if (!allowSetup)
            {
                Log.Warning("Startup configuration skipped by user choice.");
                return;
            }

            if (!IsRunningAsAdministrator())
            {
                Log.Warning("Startup configuration requires Administrator privileges. Please run the agent as Administrator once to apply firewall, URL ACL, and Remote Desktop setup.");
                return;
            }

            foreach (var rule in missingFirewallRules)
            {
                EnsureFirewallRule(rule.Name, rule.Port);
            }

            foreach (var urlAcl in missingUrlAcls)
            {
                EnsureUrlAcl(urlAcl);
            }

            if (needsRemoteDesktopSetup)
            {
                EnsureRemoteDesktopEnabled(remoteDesktopPort);
            }

            Log.Information("Startup configuration completed.");

            EnsureScheduledTask();
        }

        private static void EnsureScheduledTask()
        {
            const string taskName = "IRISAgent";

            try
            {
                var queryResult = RunCommand("schtasks", $"/Query /TN \"{taskName}\"");
                if (queryResult.ExitCode == 0)
                {
                    Log.Information("Scheduled task '{TaskName}' already exists.", taskName);
                    return;
                }

                var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(exePath))
                {
                    Log.Warning("Could not determine agent executable path. Skipping scheduled task creation.");
                    return;
                }

                var createResult = RunCommand("schtasks",
                    $"/Create /TN \"{taskName}\" /TR \"\\\"{exePath}\\\"\" /SC ONSTART /RU SYSTEM /RL HIGHEST /F");

                if (createResult.ExitCode == 0)
                {
                    Log.Information("Scheduled task '{TaskName}' created to run agent on startup.", taskName);
                }
                else
                {
                    Log.Warning("Failed to create scheduled task '{TaskName}'. Output: {Output}", taskName, createResult.Output.Trim());
                }
            }
            catch (Exception ex)
            {
                Log.Warning("Error ensuring scheduled task: {Message}", ex.Message);
            }
        }

        private static string BuildSummary(
            IReadOnlyCollection<FirewallRulePlan> missingFirewallRules,
            IReadOnlyCollection<string> missingUrlAcls,
            bool needsRemoteDesktopSetup)
        {
            var lines = new List<string>
            {
                "IRIS Agent requires initial local configuration."
            };

            if (missingFirewallRules.Any())
            {
                lines.Add("Firewall rules:");
                foreach (var rule in missingFirewallRules)
                {
                    lines.Add($"  • TCP {rule.Port} ({rule.Name})");
                }
            }

            if (missingUrlAcls.Any())
            {
                lines.Add("HTTP URL ACL:");
                foreach (var urlAcl in missingUrlAcls)
                {
                    lines.Add($"  • {urlAcl}");
                }
            }

            if (needsRemoteDesktopSetup)
            {
                lines.Add("Remote Desktop setup:");
                lines.Add("  • Enable incoming RDP connections");
                lines.Add("  • Enable Remote Desktop firewall group");
                lines.Add("  • Ensure Remote Desktop Service is running");
            }

            lines.Add(string.Empty);
            lines.Add("Allow IRIS Agent to apply these changes now?");

            return string.Join(Environment.NewLine, lines);
        }

        private static bool PromptUserToConfigure(string message)
        {
            try
            {
                var result = MessageBox.Show(
                    message,
                    "IRIS Agent Initial Configuration",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button1);

                return result == DialogResult.Yes;
            }
            catch
            {
                Console.WriteLine(message);
                Console.Write("Type Y to allow setup now: ");
                var input = Console.ReadLine();
                return string.Equals(input?.Trim(), "Y", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(input?.Trim(), "YES", StringComparison.OrdinalIgnoreCase);
            }
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

        private static void EnsureFirewallRule(string ruleName, int port)
        {
            var command = $"advfirewall firewall add rule name=\"{ruleName}\" dir=in action=allow protocol=TCP localport={port}";
            var result = RunCommand("netsh", command);

            if (result.ExitCode == 0)
            {
                Log.Information("Firewall rule ensured: {RuleName}", ruleName);
            }
            else
            {
                Log.Warning("Failed to add firewall rule {RuleName}. Output: {Output}", ruleName, result.Output.Trim());
            }
        }

        private static bool UrlAclExists(string url)
        {
            var result = RunCommand("netsh", $"http show urlacl url={url}");
            return result.ExitCode == 0 &&
                   !result.Output.Contains("Error", StringComparison.OrdinalIgnoreCase);
        }

        private static void EnsureUrlAcl(string url)
        {
            var user = $"{Environment.UserDomainName}\\{Environment.UserName}";
            var result = RunCommand("netsh", $"http add urlacl url={url} user=\"{user}\"");
            if (result.ExitCode == 0)
            {
                Log.Information("URL ACL ensured: {Url}", url);
                return;
            }

            Log.Warning("Failed to add URL ACL for {Url}. Output: {Output}", url, result.Output.Trim());
        }

        private static bool IsRemoteDesktopReady(int desiredPort)
        {
            var checkResult = RunCommand("reg", "query \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\Terminal Server\" /v fDenyTSConnections");
            if (checkResult.ExitCode != 0 || !checkResult.Output.Contains("0x0", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Check if RDP port matches the desired port
            var portResult = RunCommand("reg", "query \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\Terminal Server\\WinStations\\RDP-Tcp\" /v PortNumber");
            if (portResult.ExitCode == 0)
            {
                var hexPort = $"0x{desiredPort:x}";
                if (!portResult.Output.Contains(hexPort, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private static void EnsureRemoteDesktopEnabled(int rdpPort)
        {
            var regResult = RunCommand("reg", "add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\Terminal Server\" /v fDenyTSConnections /t REG_DWORD /d 0 /f");
            if (regResult.ExitCode != 0)
            {
                Log.Warning("Failed to enable Remote Desktop registry setting. Output: {Output}", regResult.Output.Trim());
            }

            // Set custom RDP listening port
            var portResult = RunCommand("reg", $"add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\Terminal Server\\WinStations\\RDP-Tcp\" /v PortNumber /t REG_DWORD /d {rdpPort} /f");
            if (portResult.ExitCode == 0)
            {
                Log.Information("RDP listening port set to {RdpPort}", rdpPort);
            }
            else
            {
                Log.Warning("Failed to set RDP port to {RdpPort}. Output: {Output}", rdpPort, portResult.Output.Trim());
            }

            // Try enabling the built-in Remote Desktop firewall group (may fail on non-English Windows)
            var firewallGroupResult = RunCommand("netsh", "advfirewall firewall set rule group=\"remote desktop\" new enable=Yes");
            if (firewallGroupResult.ExitCode != 0)
            {
                Log.Debug("Built-in Remote Desktop firewall group not found (may be localized). Custom firewall rule for port {RdpPort} was added separately.", rdpPort);
            }

            var serviceAutoStartResult = RunCommand("sc", "config TermService start= auto");
            if (serviceAutoStartResult.ExitCode != 0)
            {
                Log.Warning("Failed to set TermService startup mode. Output: {Output}", serviceAutoStartResult.Output.Trim());
            }

            // Restart TermService so the new port takes effect
            var stopResult = RunCommand("sc", "stop TermService");
            if (stopResult.ExitCode == 0)
            {
                // Wait for service to fully stop before restarting
                System.Threading.Thread.Sleep(3000);
            }

            var startServiceResult = RunCommand("sc", "start TermService");
            if (startServiceResult.ExitCode != 0 &&
                !startServiceResult.Output.Contains("already been started", StringComparison.OrdinalIgnoreCase) &&
                !startServiceResult.Output.Contains("1056", StringComparison.OrdinalIgnoreCase))
            {
                Log.Warning("Failed to start TermService. Output: {Output}", startServiceResult.Output.Trim());
            }

            Log.Information("Remote Desktop setup completed on port {RdpPort}.", rdpPort);
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

        private readonly record struct FirewallRulePlan(string Name, int Port);
        private readonly record struct CommandResult(int ExitCode, string Output);
    }
}
