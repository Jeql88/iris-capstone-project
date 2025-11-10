using System.Threading.Tasks;

namespace IRIS.Agent.Interfaces
{
    public interface IMonitoringLogic
    {
        Task SendHeartbeatAsync();
        Task CaptureHardwareMetricsAsync();
    }
}