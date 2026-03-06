using System.Threading.Tasks;

namespace IRIS.Agent.Services.Contracts
{
    public interface IShutdownService
    {
        Task HandleShutdownAsync();
    }
}
