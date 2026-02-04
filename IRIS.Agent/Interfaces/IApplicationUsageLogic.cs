namespace IRIS.Agent.Interfaces;

public interface IApplicationUsageLogic
{
    Task StartMonitoringAsync();
    Task StopMonitoringAsync();
}
