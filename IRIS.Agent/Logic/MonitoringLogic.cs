using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serilog;
using IRIS.Core.Data;
using IRIS.Core.DTOs;
using IRIS.Core.Models;
using IRIS.Agent.Services.Contracts;

namespace IRIS.Agent.Logic
{
    public class MonitoringLogic : IMonitoringService
    {
        private readonly IRISDbContext _context;
        private readonly string _macAddress;
        private readonly string _pingHost;
        private readonly int _pingTimeoutMs;
        private readonly DbContextOptions<IRISDbContext> _dbOptions;
        private readonly int _freezeAutoUnfreezeMinutes;
        private readonly string _machineName;
        private readonly IPrivilegedHelperClient _helperClient;
        private readonly SemaphoreSlim _contextLock = new(1, 1);
        private readonly FreezeOverlayController _freezeOverlayController = new();

        private long _lastBytesSent = -1;
        private long _lastBytesReceived = -1;
        private DateTime _lastNetworkSample = DateTime.MinValue;

        public MonitoringLogic(
            IRISDbContext context,
            string macAddress,
            string pingHost,
            int pingTimeoutMs,
            DbContextOptions<IRISDbContext> dbOptions,
            int freezeAutoUnfreezeMinutes,
            IPrivilegedHelperClient helperClient)
        {
            _context = context;
            _macAddress = macAddress;
            _pingHost = pingHost;
            _pingTimeoutMs = pingTimeoutMs;
            _dbOptions = dbOptions;
            _freezeAutoUnfreezeMinutes = Math.Clamp(freezeAutoUnfreezeMinutes, 1, 120);
            _machineName = Environment.MachineName;
            _helperClient = helperClient;
        }

        public async Task SendHeartbeatAsync()
        {
            await _contextLock.WaitAsync();
            try
            {
                var pc = await ResolveCurrentPcAsync();
                if (pc == null)
                {
                    pc = await CreatePcFromHeartbeatAsync();
                }

                if (pc != null)
                {
                    var networkInfo = TryGetNetworkInfo();
                    if (networkInfo != null)
                    {
                        pc.IpAddress = networkInfo.IpAddress;
                        pc.SubnetMask = networkInfo.SubnetMask;
                        pc.DefaultGateway = networkInfo.DefaultGateway;
                        pc.Hostname = _machineName;

                        if (!string.Equals(pc.MacAddress, networkInfo.MacAddress, StringComparison.OrdinalIgnoreCase))
                        {
                            var hasConflict = await _context.PCs.AnyAsync(p =>
                                p.Id != pc.Id &&
                                p.MacAddress == networkInfo.MacAddress);

                            if (!hasConflict)
                            {
                                pc.MacAddress = networkInfo.MacAddress;
                            }
                            else
                            {
                                Log.Warning("Skipping MAC update for {MachineName}; target MAC {Mac} belongs to another record", _machineName, networkInfo.MacAddress);
                            }
                        }
                    }

                    pc.Status = PCStatus.Online;
                    pc.LastSeen = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    Log.Information("Heartbeat sent for PC {MacAddress} ({MachineName})", pc.MacAddress, _machineName);
                }
                else
                {
                    Log.Warning("PC not found for heartbeat (MAC {MacAddress}, Hostname {MachineName})", _macAddress, _machineName);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to send heartbeat for PC {MacAddress}", _macAddress);
                throw;
            }
            finally
            {
                _contextLock.Release();
            }
        }

        public async Task CaptureHardwareMetricsAsync()
        {
            await _contextLock.WaitAsync();
            try
            {
                var pc = await ResolveCurrentPcAsync();
                if (pc == null)
                {
                    Log.Warning("PC not found for hardware metrics capture (MAC {MacAddress}, Hostname {MachineName})", _macAddress, _machineName);
                    return;
                }

                // Capture CPU usage
                var cpuUsage = GetCpuUsage();

                // Capture RAM usage
                var ramUsage = GetRamUsage();

                // Capture Disk usage
                var diskUsage = GetDiskUsage();

                // Capture temperature and GPU load
                var (cpuTemperature, gpuTemperature, gpuUsage) = GetTemperatureAndGpuMetrics();

                var metric = new HardwareMetric
                {
                    PCId = pc.Id,
                    CpuUsage = cpuUsage,
                    MemoryUsage = ramUsage,
                    DiskUsage = diskUsage,
                    CpuTemperature = cpuTemperature,
                    GpuTemperature = gpuTemperature,
                    GpuUsage = gpuUsage,
                    Timestamp = DateTime.UtcNow
                };

                _context.HardwareMetrics.Add(metric);
                await _context.SaveChangesAsync();

                Log.Information(
                    "Hardware metrics captured for PC {MacAddress}: CPU={Cpu:F1}%, RAM={Ram:F1}%, Disk={Disk:F1}%, CPU Temp={CpuTemp}, GPU Temp={GpuTemp}, GPU={Gpu:F1}%",
                    _macAddress,
                    cpuUsage,
                    ramUsage,
                    diskUsage,
                    cpuTemperature?.ToString("F1") ?? "N/A",
                    gpuTemperature?.ToString("F1") ?? "N/A",
                    gpuUsage ?? 0);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to capture hardware metrics for PC {MacAddress}", _macAddress);
                throw;
            }
            finally
            {
                _contextLock.Release();
            }
        }

        public async Task CaptureNetworkMetricsAsync()
        {
            await _contextLock.WaitAsync();
            try
            {
                var pc = await ResolveCurrentPcAsync();
                if (pc == null)
                {
                    Log.Warning("PC not found for network metrics capture (MAC {MacAddress}, Hostname {MachineName})", _macAddress, _machineName);
                    return;
                }

                var now = DateTime.UtcNow;

                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(nic => nic.OperationalStatus == OperationalStatus.Up &&
                                  nic.NetworkInterfaceType != NetworkInterfaceType.Loopback);

                var primaryNic = interfaces.FirstOrDefault();
                if (primaryNic == null)
                {
                    Log.Warning("No active network interface found for {MacAddress}", _macAddress);
                    return;
                }

                long bytesSent = 0;
                long bytesReceived = 0;
                foreach (var nic in interfaces)
                {
                    var stats = nic.GetIPv4Statistics();
                    bytesSent += stats.BytesSent;
                    bytesReceived += stats.BytesReceived;
                }

                double? uploadMbps = null;
                double? downloadMbps = null;
                if (_lastNetworkSample != DateTime.MinValue && _lastBytesSent >= 0 && _lastBytesReceived >= 0)
                {
                    var seconds = (now - _lastNetworkSample).TotalSeconds;
                    if (seconds > 0)
                    {
                        uploadMbps = (bytesSent - _lastBytesSent) * 8.0 / (seconds * 1_000_000.0);
                        downloadMbps = (bytesReceived - _lastBytesReceived) * 8.0 / (seconds * 1_000_000.0);
                    }
                }

                // Simple latency/packet loss check via ping
                var (latency, packetLoss) = PingHost(_pingHost, 3, _pingTimeoutMs);

                var metric = new NetworkMetric
                {
                    PCId = pc.Id,
                    Timestamp = now,
                    UploadSpeed = uploadMbps,
                    DownloadSpeed = downloadMbps,
                    BytesSent = bytesSent,
                    BytesReceived = bytesReceived,
                    Latency = latency,
                    PacketLoss = packetLoss,
                    IsConnected = true,
                    NetworkInterface = primaryNic.Description
                };

                _context.NetworkMetrics.Add(metric);
                await _context.SaveChangesAsync();

                _lastBytesSent = bytesSent;
                _lastBytesReceived = bytesReceived;
                _lastNetworkSample = now;

                Log.Information("Network metrics captured for PC {MacAddress}: Up={Up:F2} Mbps, Down={Down:F2} Mbps, Latency={Lat:F0} ms, Loss={Loss:F1}%",
                    pc.MacAddress,
                    uploadMbps ?? 0,
                    downloadMbps ?? 0,
                    latency ?? 0,
                    packetLoss ?? 0);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to capture network metrics for PC {MacAddress}", _macAddress);
                throw;
            }
            finally
            {
                _contextLock.Release();
            }
        }

        public async Task ProcessPendingPowerCommandAsync()
        {
            try
            {
                var normalizedMac = NormalizeMacAddress(_macAddress);

                using var commandContext = new IRISDbContext(_dbOptions);
                var now = DateTime.UtcNow;
                var pending = await commandContext.PendingCommands
                    .Where(c => c.MacAddress == normalizedMac
                                && c.Status == PendingCommandStatus.Pending
                                && c.ExpiresAtUtc > now)
                    .OrderBy(c => c.CreatedAtUtc)
                    .FirstOrDefaultAsync();

                if (pending == null)
                    return;

                pending.Status = PendingCommandStatus.Consumed;
                await commandContext.SaveChangesAsync();

                var response = string.IsNullOrWhiteSpace(pending.Payload)
                    ? pending.CommandType
                    : $"{pending.CommandType}::{pending.Payload}";

                if (response.Equals("Shutdown", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Warning("Received remote shutdown command for PC {MacAddress}; showing user warning dialog", _macAddress);
                    var wasCancelled = await ShutdownWarningDialog.ShowCancelOnlyWarningAsync(
                        "Remote Shutdown Warning",
                        "This PC will shut down due to a remote command in 15 seconds.\n\nClick Cancel to prevent shutdown.",
                        15000);

                    if (!wasCancelled)
                    {
                        try
                        {
                            await _helperClient.ForceShutdownAsync(0);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Failed to execute remote shutdown via helper.");
                        }
                    }

                    return;
                }

                if (response.Equals("Restart", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Warning("Received remote restart command for PC {MacAddress}; showing user warning dialog", _macAddress);
                    var wasCancelled = await ShutdownWarningDialog.ShowCancelOnlyWarningAsync(
                        "Remote Restart Warning",
                        "This PC will restart due to a remote command in 15 seconds.\n\nClick Cancel to prevent restart.",
                        15000);

                    if (!wasCancelled)
                    {
                        try
                        {
                            await _helperClient.ForceRestartAsync(0);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Failed to execute remote restart via helper.");
                        }
                    }

                    return;
                }

                if (response.Equals("RefreshMetrics", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Information("Executing immediate metrics refresh from server command for PC {MacAddress}", _macAddress);
                    await SendHeartbeatAsync();
                    await CaptureHardwareMetricsAsync();
                    await CaptureNetworkMetricsAsync();
                    return;
                }

                if (response.Equals("FreezeOn", StringComparison.OrdinalIgnoreCase) ||
                    response.StartsWith("FreezeOn::", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Warning("Executing freeze-on command for PC {MacAddress}", _macAddress);
                    _freezeOverlayController.Freeze(_freezeAutoUnfreezeMinutes, ExtractFreezeMessage(response));
                    return;
                }

                if (response.Equals("FreezeOff", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Information("Executing freeze-off command for PC {MacAddress}", _macAddress);
                    _freezeOverlayController.Unfreeze();
                    return;
                }

                if (response.StartsWith("Message::", StringComparison.OrdinalIgnoreCase))
                {
                    var decodedMessage = ExtractMessagePayload(response);
                    if (!string.IsNullOrWhiteSpace(decodedMessage))
                    {
                        Log.Information("Displaying remote message dialog for PC {MacAddress}", _macAddress);
                        await RemoteMessageDialog.ShowInfoAsync("Message from IRIS", decodedMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to process pending power command for PC {MacAddress}", _macAddress);
            }
        }

        private static string NormalizeMacAddress(string macAddress)
        {
            var normalized = new string(macAddress
                .Where(char.IsLetterOrDigit)
                .ToArray());
            return normalized.ToUpperInvariant();
        }

        private static string? ExtractFreezeMessage(string command)
        {
            var delimiterIndex = command.IndexOf("::", StringComparison.Ordinal);
            if (delimiterIndex < 0 || delimiterIndex + 2 >= command.Length)
            {
                return null;
            }

            var payload = command[(delimiterIndex + 2)..];
            if (string.IsNullOrWhiteSpace(payload))
            {
                return null;
            }

            try
            {
                var bytes = Convert.FromBase64String(payload);
                var decoded = System.Text.Encoding.UTF8.GetString(bytes);
                return string.IsNullOrWhiteSpace(decoded) ? null : decoded.Trim();
            }
            catch
            {
                return null;
            }
        }

        private static string? ExtractMessagePayload(string command)
        {
            var delimiterIndex = command.IndexOf("::", StringComparison.Ordinal);
            if (delimiterIndex < 0 || delimiterIndex + 2 >= command.Length)
            {
                return null;
            }

            var payload = command[(delimiterIndex + 2)..];
            if (string.IsNullOrWhiteSpace(payload))
            {
                return null;
            }

            try
            {
                var bytes = Convert.FromBase64String(payload);
                var decoded = System.Text.Encoding.UTF8.GetString(bytes);
                return string.IsNullOrWhiteSpace(decoded) ? null : decoded.Trim();
            }
            catch
            {
                return null;
            }
        }

        private static (double? latencyMs, double? packetLossPercent) PingHost(string host, int attempts, int timeoutMs)
        {
            try
            {
                var ping = new Ping();
                int success = 0;
                double total = 0;

                for (int i = 0; i < attempts; i++)
                {
                    var reply = ping.Send(host, timeoutMs);
                    if (reply.Status == IPStatus.Success)
                    {
                        success++;
                        total += reply.RoundtripTime;
                    }
                }

                double? latency = success > 0 ? total / success : null;
                double? loss = attempts > 0 ? (1.0 - (double)success / attempts) * 100.0 : null;
                return (latency, loss);
            }
            catch
            {
                return (null, null);
            }
        }

        private async Task<PC?> ResolveCurrentPcAsync()
        {
            var byMac = await _context.PCs.FirstOrDefaultAsync(p => p.MacAddress == _macAddress);
            if (byMac != null)
            {
                return byMac;
            }

            return await _context.PCs
                .Where(p => p.Hostname == _machineName)
                .OrderByDescending(p => p.LastSeen)
                .FirstOrDefaultAsync();
        }

        private async Task<PC?> CreatePcFromHeartbeatAsync()
        {
            var networkInfo = TryGetNetworkInfo();
            var candidateMac = networkInfo?.MacAddress;

            if (!string.IsNullOrWhiteSpace(candidateMac))
            {
                var existingByCurrentMac = await _context.PCs.FirstOrDefaultAsync(p => p.MacAddress == candidateMac);
                if (existingByCurrentMac != null)
                {
                    return existingByCurrentMac;
                }
            }

            var defaultRoomId = await EnsureDefaultRoomAsync();

            var pc = new PC
            {
                Hostname = _machineName,
                IpAddress = networkInfo?.IpAddress,
                MacAddress = !string.IsNullOrWhiteSpace(candidateMac) ? candidateMac : _macAddress,
                SubnetMask = networkInfo?.SubnetMask,
                DefaultGateway = networkInfo?.DefaultGateway,
                OperatingSystem = Environment.OSVersion.VersionString,
                Status = PCStatus.Online,
                LastSeen = DateTime.UtcNow,
                RoomId = defaultRoomId
            };

            _context.PCs.Add(pc);
            await _context.SaveChangesAsync();
            Log.Information("Created PC from heartbeat: {MachineName} ({MacAddress})", _machineName, pc.MacAddress);
            return pc;
        }

        private async Task<int> EnsureDefaultRoomAsync()
        {
            var existingRoom = await _context.Rooms.FirstOrDefaultAsync(r => r.RoomNumber == "DEFAULT");
            if (existingRoom != null)
            {
                return existingRoom.Id;
            }

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

        private static NetworkInfoDto? TryGetNetworkInfo()
        {
            try
            {
                return PCLogic.GetNetworkInfo();
            }
            catch
            {
                return null;
            }
        }

        private float GetCpuUsage()
        {
            try
            {
                using var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                cpuCounter.NextValue();
                System.Threading.Thread.Sleep(1000); // Wait for accurate reading
                return cpuCounter.NextValue();
            }
            catch
            {
                return 0; // Fallback
            }
        }

        private float GetRamUsage()
        {
            try
            {
                using var ramCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use");
                return ramCounter.NextValue();
            }
            catch
            {
                return 0; // Fallback
            }
        }

        private float GetDiskUsage()
        {
            try
            {
                var drives = DriveInfo.GetDrives().Where(d => d.IsReady);
                if (!drives.Any()) return 0;

                var totalUsed = drives.Sum(d => d.TotalSize - d.AvailableFreeSpace);
                var totalSize = drives.Sum(d => d.TotalSize);
                return totalSize > 0 ? (float)(totalUsed * 100.0 / totalSize) : 0;
            }
            catch
            {
                return 0; // Fallback
            }
        }

        private (double? cpuTemperature, double? gpuTemperature, double? gpuUsage) GetTemperatureAndGpuMetrics()
        {
            var cpuTemperature = TryGetWmiCpuTemperature();
            var (gpuTemperature, gpuUsage) = TryGetNvidiaSmiGpuMetrics();
            return (cpuTemperature, gpuTemperature, gpuUsage);
        }

        private static (double? gpuTemperature, double? gpuUsage) TryGetNvidiaSmiGpuMetrics()
        {
            try
            {
                var nvidiaSmiPath = Path.Combine(Environment.SystemDirectory, "nvidia-smi.exe");
                if (!File.Exists(nvidiaSmiPath))
                {
                    Log.Debug("nvidia-smi.exe not found at {Path}; GPU metrics unavailable", nvidiaSmiPath);
                    return (null, null);
                }

                var psi = new ProcessStartInfo
                {
                    FileName = nvidiaSmiPath,
                    Arguments = "--query-gpu=temperature.gpu,utilization.gpu --format=csv,noheader,nounits",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(psi);
                if (process == null) return (null, null);

                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(5000);

                if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                    return (null, null);

                // Output format: "65, 42" (temp, utilization)
                var parts = output.Trim().Split(',', StringSplitOptions.TrimEntries);
                if (parts.Length < 2) return (null, null);

                double? gpuTemp = double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var t) && t >= 0 && t <= 150 ? t : null;
                double? gpuUsage = double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var u) && u >= 0 && u <= 100 ? u : null;

                return (gpuTemp, gpuUsage);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to get GPU metrics from nvidia-smi");
                return (null, null);
            }
        }

        private static double? TryGetWmiCpuTemperature()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM MSAcpi_ThermalZoneTemperature");
                var values = searcher.Get()
                    .Cast<ManagementObject>()
                    .Select(mo => mo["CurrentTemperature"])
                    .OfType<uint>()
                    .Select(v => (v / 10.0) - 273.15)
                    .Where(v => v >= 10 && v <= 120)
                    .ToList();

                return values.Any() ? values.Max() : null;
            }
            catch
            {
                return null;
            }
        }

    }
}