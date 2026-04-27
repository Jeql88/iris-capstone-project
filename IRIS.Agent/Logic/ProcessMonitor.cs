using System.Diagnostics;
using System.Runtime.Versioning;
using Serilog;

namespace IRIS.Agent.Logic;

[SupportedOSPlatform("windows")]
public class ProcessMonitor
{
    private readonly Dictionary<int, ProcessInfo> _activeProcesses = new();
    private readonly List<ProcessUsageRecord> _completedRecords = new();
    private readonly Dictionary<string, DateTime> _mainProcessStartTimes = new();
    private readonly HashSet<string> _ignoredProcesses = new()
    {
        "svchost", "System", "Registry", "smss", "csrss", "wininit", "services",
        "lsass", "winlogon", "dwm", "runtimebroker", "SearchIndexer", "conhost",
        "taskhostw", "sihost", "ctfmon", "fontdrvhost", "dllhost",
        "updater", "crashpad_handler", "elevation_service", "wmic", "wmiapsrv",
        "backgroundtaskhost", "MoUsoCoreWorker", "SecurityHealthService",
        "SgrmBroker", "spoolsv", "WmiPrvSE", "audiodg", "dasHost",
        "TextInputHost", "StartMenuExperienceHost", "SearchHost", "ShellExperienceHost",
        "ApplicationFrameHost", "SystemSettings", "LockApp", "UserOOBEBroker",
        "MusNotification", "MusNotifyIcon", "WUDFHost", "CompPkgSrv", "explorer",
        // Don't record the agent itself as a tracked application — it's
        // operational noise. The dashboard (IRIS.UI) is intentionally NOT
        // listed here, since admins want to see UI usage in metrics.
        "iris.agent"
    };
    private readonly HashSet<string> _multiProcessApps = new() { "brave", "chrome", "msedge", "firefox", "msedgewebview2" };

    public List<ProcessUsageRecord> GetCompletedRecords()
    {
        lock (_completedRecords)
        {
            var records = new List<ProcessUsageRecord>(_completedRecords);
            _completedRecords.Clear();
            return records;
        }
    }

    public void ScanProcesses()
    {
        try
        {
            var currentProcessIds = new HashSet<int>();
            var currentMultiProcessApps = new HashSet<string>();
            var processes = Process.GetProcesses();

            foreach (var process in processes)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(process.ProcessName) || 
                        _ignoredProcesses.Contains(process.ProcessName.ToLower()))
                    {
                        continue;
                    }

                    // Skip processes without main windows (background services)
                    if (string.IsNullOrEmpty(process.MainWindowTitle))
                    {
                        continue;
                    }

                    var processNameLower = process.ProcessName.ToLower();

                    // Track multi-process apps as single entity by name only
                    if (_multiProcessApps.Contains(processNameLower))
                    {
                        var friendlyName = GetFriendlyName(process);
                        currentMultiProcessApps.Add(friendlyName);
                        if (!_mainProcessStartTimes.ContainsKey(friendlyName))
                        {
                            _mainProcessStartTimes[friendlyName] = DateTime.UtcNow;
                            Log.Debug("Detected multi-process app: {ProcessName}", friendlyName);
                        }
                        continue; // Skip PID tracking for multi-process apps
                    }

                    // Track regular processes by PID
                    currentProcessIds.Add(process.Id);
                    if (!_activeProcesses.ContainsKey(process.Id))
                    {
                        var friendlyName = GetFriendlyName(process);
                        _activeProcesses[process.Id] = new ProcessInfo
                        {
                            ProcessId = process.Id,
                            ProcessName = friendlyName,
                            StartTime = DateTime.UtcNow
                        };
                        Log.Debug("Detected new process: {ProcessName} (PID: {ProcessId})", 
                            friendlyName, process.Id);
                    }
                }
                catch
                {
                    // Skip processes we can't access
                }
                finally
                {
                    process.Dispose();
                }
            }

            // Detect closed multi-process apps
            var closedMultiProcessApps = _mainProcessStartTimes.Keys.Except(currentMultiProcessApps).ToList();
            foreach (var appName in closedMultiProcessApps)
            {
                var startTime = _mainProcessStartTimes[appName];
                var endTime = DateTime.UtcNow;
                var duration = endTime - startTime;

                lock (_completedRecords)
                {
                    _completedRecords.Add(new ProcessUsageRecord
                    {
                        ApplicationName = appName,
                        StartTime = startTime,
                        EndTime = endTime,
                        Duration = duration
                    });
                }

                _mainProcessStartTimes.Remove(appName);
                Log.Debug("Multi-process app closed: {ProcessName} (Duration: {Duration})", appName, duration);
            }

            // Detect closed regular processes
            var closedProcessIds = _activeProcesses.Keys.Except(currentProcessIds).ToList();
            foreach (var processId in closedProcessIds)
            {
                var processInfo = _activeProcesses[processId];
                var endTime = DateTime.UtcNow;
                var duration = endTime - processInfo.StartTime;

                lock (_completedRecords)
                {
                    _completedRecords.Add(new ProcessUsageRecord
                    {
                        ApplicationName = processInfo.ProcessName,
                        StartTime = processInfo.StartTime,
                        EndTime = endTime,
                        Duration = duration
                    });
                }

                _activeProcesses.Remove(processId);
                Log.Debug("Process closed: {ProcessName} (Duration: {Duration})", 
                    processInfo.ProcessName, duration);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error scanning processes");
        }
    }

    private string GetFriendlyName(Process process)
    {
        try
        {
            var fileDescription = process.MainModule?.FileVersionInfo?.FileDescription;
            if (!string.IsNullOrEmpty(fileDescription))
            {
                return fileDescription;
            }
        }
        catch
        {
            // Access denied - use fallback
        }

        // Fallback: Map common process names
        return process.ProcessName.ToLower() switch
        {
            "chrome" => "Google Chrome",
            "msedge" => "Microsoft Edge",
            "brave" => "Brave Browser",
            "firefox" => "Mozilla Firefox",
            "explorer" => "File Explorer",
            "teams" => "Microsoft Teams",
            "msedgewebview2" => "Microsoft Edge",   
            _ => process.ProcessName
        };
    }
}

public class ProcessInfo
{
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
}

public class ProcessUsageRecord
{
    public string ApplicationName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
}
