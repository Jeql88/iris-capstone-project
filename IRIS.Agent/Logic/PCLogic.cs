using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serilog;
using IRIS.Core.Data;
using IRIS.Core.Models;
using IRIS.Core.DTOs;
using IRIS.Agent.Services.Contracts;

namespace IRIS.Agent.Logic
{
    [SupportedOSPlatform("windows")]
    public class PCLogic : IPCService
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
                var networkInfo = GetNetworkInfo();
                var os = GetOperatingSystem();

                Log.Information("Detected PC: Name={PcName}, IP={IpAddress}, MAC={MacAddress}, OS={OS}", pcName, networkInfo.IpAddress, networkInfo.MacAddress, os);

                // Query existing PC by MAC address
                var existingPC = await _context.PCs
                    .Include(p => p.HardwareConfigs)
                    .FirstOrDefaultAsync(p => p.MacAddress == networkInfo.MacAddress);

                if (existingPC == null)
                {
                    existingPC = await _context.PCs
                        .Include(p => p.HardwareConfigs)
                        .Where(p => p.Hostname == pcName)
                        .OrderByDescending(p => p.LastSeen)
                        .FirstOrDefaultAsync();
                }

                // Ensure a default room exists and get its Id
                var defaultRoomId = await EnsureDefaultRoomAsync();

                if (existingPC != null)
                {
                    // Update existing record
                    existingPC.Hostname = pcName;
                    existingPC.IpAddress = networkInfo.IpAddress;
                    existingPC.SubnetMask = networkInfo.SubnetMask;
                    existingPC.DefaultGateway = networkInfo.DefaultGateway;
                    existingPC.OperatingSystem = os;
                    existingPC.Status = PCStatus.Online;
                    existingPC.LastSeen = DateTime.UtcNow;

                    Log.Information("Updated existing PC record for MAC {MacAddress}", networkInfo.MacAddress);
                    
                    // Update hardware config if changed
                    await UpdateHardwareConfigAsync(existingPC.Id);
                }
                else
                {
                    // Create new record
                    var newPC = new PC
                    {
                        Hostname = pcName,
                        IpAddress = networkInfo.IpAddress,
                        MacAddress = networkInfo.MacAddress,
                        SubnetMask = networkInfo.SubnetMask,
                        DefaultGateway = networkInfo.DefaultGateway,
                        OperatingSystem = os,
                        Status = PCStatus.Online,
                        LastSeen = DateTime.UtcNow,
                        RoomId = defaultRoomId // Default room, can be configured later
                    };

                    _context.PCs.Add(newPC);
                    await _context.SaveChangesAsync();
                    
                    Log.Information("Created new PC record for MAC {MacAddress}", networkInfo.MacAddress);
                    
                    // Create hardware config for new PC
                    await CreateHardwareConfigAsync(newPC.Id);
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

        private async Task<int> EnsureDefaultRoomAsync()
        {
            try
            {
                // Try to find an existing default room
                var existingRoom = await _context.Rooms
                    .FirstOrDefaultAsync(r => r.RoomNumber == "DEFAULT");

                if (existingRoom != null)
                {
                    return existingRoom.Id;
                }

                // Create a default room if it doesn't exist
                var room = new Room
                {
                    RoomNumber = "DEFAULT",
                    Description = "Default room for unassigned PCs",
                    Capacity = 0,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Rooms.Add(room);
                await _context.SaveChangesAsync();

                return room.Id;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to ensure default room exists");
                throw;
            }
        }

        private Task CreateHardwareConfigAsync(int pcId)
        {
            try
            {
                var hardwareInfo = GetHardwareInfo();
                
                var config = new PCHardwareConfig
                {
                    PCId = pcId,
                    Processor = hardwareInfo.Processor,
                    GraphicsCard = hardwareInfo.GraphicsCard,
                    Motherboard = hardwareInfo.Motherboard,
                    RamCapacity = hardwareInfo.RamCapacity,
                    StorageCapacity = hardwareInfo.StorageCapacity,
                    StorageType = hardwareInfo.StorageType,
                    AppliedAt = DateTime.UtcNow,
                    IsActive = true
                };

                _context.PCHardwareConfigs.Add(config);
                Log.Information("Created hardware config for PC {PCId}", pcId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create hardware config for PC {PCId}", pcId);
            }
            return Task.CompletedTask;
        }

        private async Task UpdateHardwareConfigAsync(int pcId)
        {
            try
            {
                var existingConfig = await _context.PCHardwareConfigs
                    .Where(c => c.PCId == pcId && c.IsActive)
                    .FirstOrDefaultAsync();

                var hardwareInfo = GetHardwareInfo();

                // Check if hardware changed
                if (existingConfig == null || HasHardwareChanged(existingConfig, hardwareInfo))
                {
                    // Deactivate old config
                    if (existingConfig != null)
                    {
                        existingConfig.IsActive = false;
                    }

                    // Create new config
                    await CreateHardwareConfigAsync(pcId);
                    Log.Information("Hardware config updated for PC {PCId}", pcId);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to update hardware config for PC {PCId}", pcId);
            }
        }

        private bool HasHardwareChanged(PCHardwareConfig existing, PCHardwareConfigCreateDto newInfo)
        {
            return existing.Processor != newInfo.Processor ||
                   existing.GraphicsCard != newInfo.GraphicsCard ||
                   existing.Motherboard != newInfo.Motherboard ||
                   existing.RamCapacity != newInfo.RamCapacity ||
                   existing.StorageCapacity != newInfo.StorageCapacity ||
                   existing.StorageType != newInfo.StorageType;
        }

        private PCHardwareConfigCreateDto GetHardwareInfo()
        {
            return new PCHardwareConfigCreateDto(
                PCId: 0, // Will be set later
                Processor: GetProcessorInfo(),
                GraphicsCard: GetGraphicsCardInfo(),
                Motherboard: GetMotherboardInfo(),
                RamCapacity: GetRamCapacity(),
                StorageCapacity: GetStorageCapacity(),
                StorageType: GetStorageType()
            );
        }

        [SupportedOSPlatform("windows")]
        private string GetProcessorInfo()
        {
            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
                foreach (var obj in searcher.Get())
                {
                    return obj["Name"]?.ToString() ?? "Unknown";
                }
            }
            catch { }
            return "Unknown";
        }

        [SupportedOSPlatform("windows")]
        private string GetGraphicsCardInfo()
        {
            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
                foreach (var obj in searcher.Get())
                {
                    return obj["Name"]?.ToString() ?? "Unknown";
                }
            }
            catch { }
            return "Unknown";
        }

        [SupportedOSPlatform("windows")]
        private string GetMotherboardInfo()
        {
            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher("SELECT Product, Manufacturer FROM Win32_BaseBoard");
                foreach (var obj in searcher.Get())
                {
                    var manufacturer = obj["Manufacturer"]?.ToString();
                    var product = obj["Product"]?.ToString();
                    return $"{manufacturer} {product}".Trim();
                }
            }
            catch { }
            return "Unknown";
        }

        [SupportedOSPlatform("windows")]
        private long GetRamCapacity()
        {
            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher("SELECT Capacity FROM Win32_PhysicalMemory");
                long totalRam = 0;
                foreach (var obj in searcher.Get())
                {
                    totalRam += Convert.ToInt64(obj["Capacity"]);
                }
                return totalRam;
            }
            catch { }
            return 0;
        }

        private long GetStorageCapacity()
        {
            try
            {
                var drives = DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed);
                return drives.Sum(d => d.TotalSize);
            }
            catch { }
            return 0;
        }

        [SupportedOSPlatform("windows")]
        private string GetStorageType()
        {
            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher(
                    "SELECT Model, MediaType, InterfaceType FROM Win32_DiskDrive");
                
                var storageTypes = new List<string>();
                
                foreach (var obj in searcher.Get())
                {
                    var model = obj["Model"]?.ToString()?.ToUpper() ?? "";
                    var mediaType = obj["MediaType"]?.ToString()?.ToUpper() ?? "";
                    var interfaceType = obj["InterfaceType"]?.ToString()?.ToUpper() ?? "";
                    
                    // Check model name for SSD indicators
                    if (model.Contains("SSD") || model.Contains("NVME") || model.Contains("SOLID STATE"))
                    {
                        storageTypes.Add("SSD");
                    }
                    // Check interface (NVMe is always SSD)
                    else if (interfaceType.Contains("NVME"))
                    {
                        storageTypes.Add("SSD");
                    }
                    // Check media type
                    else if (mediaType.Contains("SSD") || mediaType.Contains("SOLID"))
                    {
                        storageTypes.Add("SSD");
                    }
                    else
                    {
                        storageTypes.Add("HDD");
                    }
                }
                
                // Return mixed if both types found
                if (storageTypes.Contains("SSD") && storageTypes.Contains("HDD"))
                    return "SSD + HDD";
                if (storageTypes.Contains("SSD"))
                    return "SSD";
                if (storageTypes.Contains("HDD"))
                    return "HDD";
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to detect storage type");
            }
            return "Unknown";
        }

        public static NetworkInfoDto GetNetworkInfo()
        {
            var candidates = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up)
                .Where(ni => ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet || ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                .Select(ni =>
                {
                    var ipProps = ni.GetIPProperties();
                    var ipv4 = ipProps.UnicastAddresses
                        .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a.Address));

                    if (ipv4 == null)
                    {
                        return null;
                    }

                    var gateway = ipProps.GatewayAddresses
                        .FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.Any.Equals(g.Address))
                        ?.Address;

                    var ip = ipv4.Address;
                    var score = 0;

                    // Strongly prefer adapters with a valid IPv4 default gateway.
                    if (gateway != null)
                    {
                        score += 100;
                    }

                    // Prefer private LAN ranges commonly used in labs.
                    if (IsPrivateIPv4(ip))
                    {
                        score += 20;
                    }

                    // De-prioritize APIPA/self-assigned addresses.
                    if (ip.ToString().StartsWith("169.254.", StringComparison.Ordinal))
                    {
                        score -= 100;
                    }

                    return new
                    {
                        Interface = ni,
                        IPv4 = ipv4,
                        Gateway = gateway,
                        Score = score
                    };
                })
                .Where(x => x != null)
                .OrderByDescending(x => x!.Score)
                .ToList();

            var selected = candidates.FirstOrDefault();
            if (selected != null)
            {
                var ipAddress = selected.IPv4.Address.ToString();
                var macAddress = selected.Interface.GetPhysicalAddress().ToString();
                var subnetMask = selected.IPv4.IPv4Mask?.ToString() ?? "255.255.255.0";
                var defaultGateway = selected.Gateway?.ToString() ?? "N/A";

                Log.Information("Selected network adapter for registration: {Adapter} | IP={IpAddress} | Gateway={Gateway}", selected.Interface.Name, ipAddress, defaultGateway);

                return new NetworkInfoDto(ipAddress, macAddress, subnetMask, defaultGateway);
            }

            throw new Exception("No active network interface found for IP and MAC detection.");
        }

        private static bool IsPrivateIPv4(IPAddress ip)
        {
            var bytes = ip.GetAddressBytes();
            return bytes.Length == 4 && (
                bytes[0] == 10 ||
                (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                (bytes[0] == 192 && bytes[1] == 168));
        }

        private static string GetOperatingSystem()
        {
            return $"{Environment.OSVersion.Platform} {Environment.OSVersion.Version}";
        }
    }
}