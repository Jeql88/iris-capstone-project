using System.Threading.Tasks;
using Serilog;
using IRIS.Agent.Services.Contracts;

namespace IRIS.Agent.Controllers
{
    public class PCController
    {
        private readonly IPCService _pcLogic;

        public PCController(IPCService pcLogic)
        {
            _pcLogic = pcLogic;
        }

        public async Task RegisterPCAsync()
        {
            Log.Information("Starting PC registration process.");
            await _pcLogic.RegisterOrUpdatePCAsync();
            Log.Information("PC registration process completed.");
        }
    }
}