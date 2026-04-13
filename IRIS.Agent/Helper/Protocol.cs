namespace IRIS.Agent.Helper
{
    /// <summary>
    /// Operations the SYSTEM-level helper process exposes to the user-mode agent via named pipe.
    /// </summary>
    public enum HelperOp
    {
        Ping = 0,
        SetSleepTimeouts = 1,
        ForceShutdown = 2,
        ForceRestart = 3
    }

    /// <summary>
    /// JSON-serialized request sent from the user-mode agent to the SYSTEM helper.
    /// </summary>
    public sealed record HelperRequest(
        HelperOp Op,
        string Token,
        int? AcMinutes = null,
        int? DcMinutes = null,
        int? DelaySeconds = null);

    /// <summary>
    /// JSON-serialized response returned by the SYSTEM helper.
    /// </summary>
    public sealed record HelperResponse(bool Ok, string? Error = null);
}
