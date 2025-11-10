using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serilog;
using IRIS.Core.Data;
using IRIS.Core.Models;
using IRIS.Agent.Interfaces;

namespace IRIS.Agent.Logic
{
    public class PCLogic : IPCLogic
    {
        private readonly IRISDbContext _context;

        public PCLogic(IRISDbContext context)
        {
            _context = context;
        }

        public async Task RegisterOrUpdatePCAsync()
        {
            try
            {
                // Detect PC details
                var pcName = Environment.MachineName;
                var (ipAddress, macAddress) = GetNetworkInfo();

                Log.Information("Detected PC: Name={PcName}, IP={IpAddress}, MAC={MacAddress}", pcName, ipAddress, macAddress);

                // Query existing PC by MAC address
                var existingPC = await _context.PCs.FirstOrDefaultAsync(p => p.MacAddress == macAddress);

                if (existingPC != null)
                {
                    // Update existing record
                    existingPC.Hostname = pcName;
                    existingPC.IpAddress = ipAddress;
                    existingPC.Status = PCStatus.Online;
                    existingPC.LastSeen = DateTime.UtcNow;

                    Log.Information("Updated existing PC record for MAC {MacAddress}", macAddress);
                }
                else
                {
                    // Create new record
                    var newPC = new PC
                    {
                        Hostname = pcName,
                        IpAddress = ipAddress,
                        MacAddress = macAddress,
                        Status = PCStatus.Online,
                        LastSeen = DateTime.UtcNow,
                        RoomId = 1 // Default room, can be configured later
                    };

                    _context.PCs.Add(newPC);
                    Log.Information("Created new PC record for MAC {MacAddress}", macAddress);
                }

                await _context.SaveChangesAsync();
                Log.Information("PC registration/update completed successfully.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to register or update PC record");
                throw; // Re-throw to let caller handle
            }
        }

        public static (string IpAddress, string MacAddress) GetNetworkInfo()
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus == OperationalStatus.Up &&
                    (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                     ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211))
                {
                    var ipProps = ni.GetIPProperties();
                    var ipv4 = ipProps.UnicastAddresses
                        .FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

                    if (ipv4 != null)
                    {
                        return (ipv4.Address.ToString(), ni.GetPhysicalAddress().ToString());
                    }
                }
            }

            throw new Exception("No active network interface found for IP and MAC detection.");
        }
    }
}