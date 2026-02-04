using System.Threading.Tasks;

namespace IRIS.Agent.Services.Contracts
{
    public interface IApplicationUsageService
    {
        Task StartMonitoringAsync();
        Task StopMonitoringAsync();
    }
}
