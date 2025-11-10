using System.Threading.Tasks;

namespace IRIS.Agent.Interfaces
{
    public interface IShutdownLogic
    {
        Task HandleShutdownAsync();
    }
}