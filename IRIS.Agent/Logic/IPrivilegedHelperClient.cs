namespace IRIS.Agent.Logic
{
    /// <summary>
    /// Sends admin-operation requests to the SYSTEM-level helper process via named pipe.
    /// </summary>
    public interface IPrivilegedHelperClient
    {
        Task SetSleepTimeoutsAsync(int acMinutes, int dcMinutes);
        Task ForceShutdownAsync(int delaySeconds = 0);
        Task ForceRestartAsync(int delaySeconds = 0);
        Task PingAsync();
    }
}
