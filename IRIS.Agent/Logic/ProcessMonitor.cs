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
        "lsass", "winlogon", "dwm", "RuntimeBroker", "SearchIndexer", "conhost",
        "taskhostw", "explorer", "sihost", "ctfmon", "fontdrvhost", "dllhost",
        "updater", "crashpad_handler", "elevation_service"
    };
    private readonly HashSet<string> _multiProcessApps = new() { "brave", "chrome", "msedge", "firefox" };

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

                    currentProcessIds.Add(process.Id);
                    var processNameLower = process.ProcessName.ToLower();

                    // Track multi-process apps as single entity
                    if (_multiProcessApps.Contains(processNameLower))
                    {
                        currentMultiProcessApps.Add(processNameLower);
                        if (!_mainProcessStartTimes.ContainsKey(processNameLower))
                        {
                            _mainProcessStartTimes[processNameLower] = DateTime.UtcNow;
                            Log.Debug("Detected multi-process app: {ProcessName}", process.ProcessName);
                        }
                        continue;
                    }

                    // Track regular processes
                    if (!_activeProcesses.ContainsKey(process.Id))
                    {
                        _activeProcesses[process.Id] = new ProcessInfo
                        {
                            ProcessId = process.Id,
                            ProcessName = process.ProcessName,
                            StartTime = process.StartTime
                        };
                        Log.Debug("Detected new process: {ProcessName} (PID: {ProcessId})", 
                            process.ProcessName, process.Id);
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
                var duration = endTime - processInfo.StartTime.ToUniversalTime();

                lock (_completedRecords)
                {
                    _completedRecords.Add(new ProcessUsageRecord
                    {
                        ApplicationName = processInfo.ProcessName,
                        StartTime = processInfo.StartTime.ToUniversalTime(),
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
