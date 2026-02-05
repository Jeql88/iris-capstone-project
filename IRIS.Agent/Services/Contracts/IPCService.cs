using System.Threading.Tasks;

namespace IRIS.Agent.Services.Contracts
{
    public interface IPCService
    {
        Task RegisterOrUpdatePCAsync();
    }
}
