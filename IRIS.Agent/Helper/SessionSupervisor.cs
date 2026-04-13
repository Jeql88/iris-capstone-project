using System.Runtime.InteropServices;
using Serilog;

namespace IRIS.Agent.Helper
{
    /// <summary>
    /// Polls Windows Terminal Services for active interactive sessions and launches
    /// a user-mode IRIS.Agent.exe --background process in each one via CreateProcessAsUser.
    /// Runs inside the SYSTEM helper process.
    /// </summary>
    internal sealed class SessionSupervisor : IAsyncDisposable
    {
        private readonly string _agentExePath;
        private readonly CancellationTokenSource _cts = new();
        private Task? _pollLoop;

        /// <summary>SessionId → tracked child info.</summary>
        private readonly Dictionary<uint, TrackedAgent> _agents = new();

        private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);
        private readonly TimeSpan _relaunchBackoff = TimeSpan.FromSeconds(3);

        public SessionSupervisor(string agentExePath)
        {
            _agentExePath = agentExePath;
        }

        public void Start()
        {
            _pollLoop = Task.Run(() => PollLoopAsync(_cts.Token));
            Log.Information("SessionSupervisor started. Polling every {Seconds}s.", _pollInterval.TotalSeconds);
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            if (_pollLoop != null)
            {
                try { await _pollLoop; } catch { /* ignore shutdown exceptions */ }
            }
            _cts.Dispose();
        }

        private async Task PollLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    PollOnce();
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "SessionSupervisor poll error.");
                }

                try { await Task.Delay(_pollInterval, ct); }
                catch (OperationCanceledException) { break; }
            }
        }

        private void PollOnce()
        {
            // Reap exited children first.
            ReapExited();

            // Enumerate active sessions.
            var sessions = GetActiveSessions();

            foreach (var sessionId in sessions)
            {
                if (_agents.TryGetValue(sessionId, out var tracked))
                {
                    // Already running or within backoff window.
                    if (!tracked.HasExited)
                        continue;

                    if (DateTime.UtcNow - tracked.ExitedAt < _relaunchBackoff)
                        continue;

                    // Backoff elapsed — remove stale entry so we relaunch below.
                    _agents.Remove(sessionId);
                }

                // Launch a new agent for this session.
                try
                {
                    var pid = LaunchAgentInSession(sessionId);
                    if (pid != 0)
                    {
                        _agents[sessionId] = new TrackedAgent(pid);
                        Log.Information("Launched agent PID {Pid} in session {SessionId}.", pid, sessionId);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to launch agent in session {SessionId}.", sessionId);
                }
            }
        }

        private void ReapExited()
        {
            foreach (var kvp in _agents.ToList())
            {
                if (kvp.Value.HasExited)
                    continue;

                try
                {
                    using var proc = System.Diagnostics.Process.GetProcessById((int)kvp.Value.ProcessId);
                    if (proc.HasExited)
                    {
                        _agents[kvp.Key] = kvp.Value with { HasExited = true, ExitedAt = DateTime.UtcNow };
                        Log.Information("Agent PID {Pid} in session {SessionId} has exited.", kvp.Value.ProcessId, kvp.Key);
                    }
                }
                catch (ArgumentException)
                {
                    // Process no longer exists.
                    _agents[kvp.Key] = kvp.Value with { HasExited = true, ExitedAt = DateTime.UtcNow };
                    Log.Information("Agent PID {Pid} in session {SessionId} no longer exists.", kvp.Value.ProcessId, kvp.Key);
                }
            }
        }

        private static List<uint> GetActiveSessions()
        {
            var result = new List<uint>();

            if (!HelperNativeMethods.WTSEnumerateSessions(
                    HelperNativeMethods.WTS_CURRENT_SERVER_HANDLE, 0, 1,
                    out var pSessionInfo, out var count))
            {
                Log.Warning("WTSEnumerateSessions failed (error {Error}).", Marshal.GetLastWin32Error());
                return result;
            }

            try
            {
                var dataSize = Marshal.SizeOf<HelperNativeMethods.WTS_SESSION_INFO>();
                var current = pSessionInfo;

                for (var i = 0; i < count; i++)
                {
                    var info = Marshal.PtrToStructure<HelperNativeMethods.WTS_SESSION_INFO>(current);
                    current = IntPtr.Add(current, dataSize);

                    // Only care about active interactive sessions (not services session 0).
                    if (info.State == HelperNativeMethods.WTS_CONNECTSTATE_CLASS.WTSActive && info.SessionId > 0)
                    {
                        result.Add(info.SessionId);
                    }
                }
            }
            finally
            {
                HelperNativeMethods.WTSFreeMemory(pSessionInfo);
            }

            return result;
        }

        private uint LaunchAgentInSession(uint sessionId)
        {
            IntPtr userToken = IntPtr.Zero;
            IntPtr dupToken = IntPtr.Zero;
            IntPtr envBlock = IntPtr.Zero;

            try
            {
                if (!HelperNativeMethods.WTSQueryUserToken(sessionId, out userToken))
                {
                    var err = Marshal.GetLastWin32Error();
                    Log.Warning("WTSQueryUserToken failed for session {SessionId} (error {Error}).", sessionId, err);
                    return 0;
                }

                if (!HelperNativeMethods.DuplicateTokenEx(
                        userToken,
                        HelperNativeMethods.MAXIMUM_ALLOWED,
                        IntPtr.Zero,
                        HelperNativeMethods.SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation,
                        HelperNativeMethods.TOKEN_TYPE.TokenPrimary,
                        out dupToken))
                {
                    var err = Marshal.GetLastWin32Error();
                    Log.Warning("DuplicateTokenEx failed for session {SessionId} (error {Error}).", sessionId, err);
                    return 0;
                }

                if (!HelperNativeMethods.CreateEnvironmentBlock(out envBlock, dupToken, false))
                {
                    var err = Marshal.GetLastWin32Error();
                    Log.Warning("CreateEnvironmentBlock failed for session {SessionId} (error {Error}).", sessionId, err);
                    return 0;
                }

                var si = new HelperNativeMethods.STARTUPINFO();
                si.cb = Marshal.SizeOf(si);
                si.lpDesktop = @"winsta0\default";

                var workingDir = Path.GetDirectoryName(_agentExePath) ?? @"C:\";
                var commandLine = $"\"{_agentExePath}\" --background";

                var creationFlags = HelperNativeMethods.CREATE_UNICODE_ENVIRONMENT
                                  | HelperNativeMethods.CREATE_NO_WINDOW;

                if (!HelperNativeMethods.CreateProcessAsUser(
                        dupToken,
                        null,
                        commandLine,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        false,
                        creationFlags,
                        envBlock,
                        workingDir,
                        ref si,
                        out var pi))
                {
                    var err = Marshal.GetLastWin32Error();
                    Log.Warning("CreateProcessAsUser failed for session {SessionId} (error {Error}).", sessionId, err);
                    return 0;
                }

                // Close the process and thread handles — we only need the PID.
                HelperNativeMethods.CloseHandle(pi.hProcess);
                HelperNativeMethods.CloseHandle(pi.hThread);

                return pi.dwProcessId;
            }
            finally
            {
                if (envBlock != IntPtr.Zero)
                    HelperNativeMethods.DestroyEnvironmentBlock(envBlock);
                if (dupToken != IntPtr.Zero)
                    HelperNativeMethods.CloseHandle(dupToken);
                if (userToken != IntPtr.Zero)
                    HelperNativeMethods.CloseHandle(userToken);
            }
        }

        private record struct TrackedAgent(uint ProcessId, bool HasExited = false, DateTime ExitedAt = default);
    }
}
