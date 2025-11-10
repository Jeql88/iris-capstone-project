using System.Threading.Tasks;
using Serilog;
using IRIS.Agent.Interfaces;

namespace IRIS.Agent.Controllers
{
    public class PCController
    {
        private readonly IPCLogic _pcLogic;

        public PCController(IPCLogic pcLogic)
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