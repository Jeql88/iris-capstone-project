using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serilog;
using IRIS.Core.Data;
using IRIS.Core.Models;
using IRIS.Agent.Services.Contracts;

namespace IRIS.Agent.Logic
{
    public class ShutdownLogic : IShutdownService
    {
        private readonly IRISDbContext _context;
        private readonly string _macAddress;

        public ShutdownLogic(IRISDbContext context, string macAddress)
        {
            _context = context;
            _macAddress = macAddress;
        }

        public async Task HandleShutdownAsync()
        {
            try
            {
                var pc = await _context.PCs.FirstOrDefaultAsync(p => p.MacAddress == _macAddress);
                if (pc != null)
                {
                    pc.Status = PCStatus.Offline;
                    pc.LastSeen = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    Log.Information("Shutdown handled: PC {MacAddress} marked as Offline", _macAddress);
                }
                else
                {
                    Log.Warning("PC with MAC {MacAddress} not found during shutdown", _macAddress);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to handle shutdown for PC {MacAddress}", _macAddress);
                // Don't re-throw; shutdown should complete gracefully
            }
        }
    }
}