using System.Threading.Tasks;

namespace IRIS.Agent.Services.Contracts
{
    public interface IMonitoringService
    {
        Task SendHeartbeatAsync();
        Task CaptureHardwareMetricsAsync();
    }
}
