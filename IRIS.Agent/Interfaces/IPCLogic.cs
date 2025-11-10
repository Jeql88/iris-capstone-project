using System.Threading.Tasks;

namespace IRIS.Agent.Interfaces
{
    public interface IPCLogic
    {
        Task RegisterOrUpdatePCAsync();
    }
}