using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using LibreHardwareMonitor.Hardware;
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
        private readonly string _commandServerHost;
        private readonly int _commandServerPort;
        private readonly string _machineName;
        private readonly SemaphoreSlim _contextLock = new(1, 1);

        private long _lastBytesSent = -1;
        private long _lastBytesReceived = -1;
        private DateTime _lastNetworkSample = DateTime.MinValue;
        private readonly Computer? _hardwareComputer;

        public MonitoringLogic(
            IRISDbContext context,
            string macAddress,
            string pingHost,
            int pingTimeoutMs,
            string commandServerHost,
            int commandServerPort)
        {
            _context = context;
            _macAddress = macAddress;
            _pingHost = pingHost;
            _pingTimeoutMs = pingTimeoutMs;
            _commandServerHost = commandServerHost;
            _commandServerPort = commandServerPort;
            _machineName = Environment.MachineName;

            try
            {
                _hardwareComputer = new Computer
                {
                    IsCpuEnabled = true,
                    IsGpuEnabled = true
                };
                _hardwareComputer.Open();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Hardware sensors are not available; temperatures and GPU usage may be missing");
            }
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

                Log.Information("Network metrics captured for PC {MacAddress}: Up={Up:F2} Mbps, Down={Down:F2} Mbps, Latency={Lat:F1} ms, Loss={Loss:F1}%",
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
                using var client = new TcpClient();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));

                await client.ConnectAsync(_commandServerHost, _commandServerPort, timeout.Token);

                using var stream = client.GetStream();
                using var writer = new StreamWriter(stream) { AutoFlush = true };
                using var reader = new StreamReader(stream);

                await writer.WriteLineAsync(_macAddress);
                var response = await reader.ReadLineAsync();

                if (string.IsNullOrWhiteSpace(response) || response.Equals("NONE", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (response.Equals("Shutdown", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Warning("Executing immediate shutdown from server command for PC {MacAddress}", _macAddress);
                    Process.Start("shutdown", "/s /t 0 /f /c \"Shutdown requested from IRIS monitor\"");
                    return;
                }

                if (response.Equals("Restart", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Warning("Executing immediate restart from server command for PC {MacAddress}", _macAddress);
                    Process.Start("shutdown", "/r /t 0 /f /c \"Restart requested from IRIS monitor\"");
                    return;
                }

                if (response.Equals("RefreshMetrics", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Information("Executing immediate metrics refresh from server command for PC {MacAddress}", _macAddress);
                    await SendHeartbeatAsync();
                    await CaptureHardwareMetricsAsync();
                    await CaptureNetworkMetricsAsync();
                }
            }
            catch (OperationCanceledException)
            {
                // Server timeout/unreachable; retry on next poll
            }
            catch (SocketException)
            {
                // Server unavailable; retry on next poll
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to process pending power command for PC {MacAddress}", _macAddress);
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
            if (_hardwareComputer == null)
            {
                var wmiTemp = TryGetWmiCpuTemperature();
                return (wmiTemp, null, null);
            }

            try
            {
                double? cpuTemperature = null;
                double? gpuTemperature = null;
                double? gpuUsage = null;

                foreach (var hardware in _hardwareComputer.Hardware)
                {
                    UpdateHardwareRecursive(hardware);

                    if (hardware.HardwareType == HardwareType.Cpu)
                    {
                        var temp = GetCpuTemperatureFromHardware(hardware);
                        if (temp.HasValue)
                        {
                            cpuTemperature = temp;
                        }
                        continue;
                    }

                    if (hardware.HardwareType == HardwareType.GpuAmd ||
                        hardware.HardwareType == HardwareType.GpuNvidia ||
                        hardware.HardwareType == HardwareType.GpuIntel)
                    {
                        gpuTemperature = GetSensorValue(hardware, SensorType.Temperature, "Core")
                            ?? GetFirstSensorValue(hardware, SensorType.Temperature)
                            ?? gpuTemperature;

                        gpuUsage = GetSensorValue(hardware, SensorType.Load, "Core")
                            ?? GetSensorValue(hardware, SensorType.Load, "D3D")
                            ?? GetFirstSensorValue(hardware, SensorType.Load)
                            ?? gpuUsage;
                    }
                }

                if (!cpuTemperature.HasValue)
                {
                    cpuTemperature = TryGetWmiCpuTemperature();
                }

                return (cpuTemperature, gpuTemperature, gpuUsage);
            }
            catch
            {
                var wmiTemp = TryGetWmiCpuTemperature();
                return (wmiTemp, null, null);
            }
        }

        private static void UpdateHardwareRecursive(IHardware hardware)
        {
            hardware.Update();
            foreach (var subHardware in hardware.SubHardware)
            {
                UpdateHardwareRecursive(subHardware);
            }
        }

        private static double? GetCpuTemperatureFromHardware(IHardware hardware)
        {
            var temperatures = CollectSensorValues(hardware, SensorType.Temperature)
                .Where(v => v >= 10 && v <= 120)
                .ToList();

            if (!temperatures.Any())
            {
                return null;
            }

            return temperatures.Max();
        }

        private static double? GetSensorValue(IHardware hardware, SensorType sensorType, string nameContains)
        {
            var sensor = CollectSensors(hardware)
                .FirstOrDefault(s => s.SensorType == sensorType &&
                                     s.Value.HasValue &&
                                     s.Name.Contains(nameContains, StringComparison.OrdinalIgnoreCase));

            return sensor?.Value;
        }

        private static double? GetFirstSensorValue(IHardware hardware, SensorType sensorType)
        {
            var sensor = CollectSensors(hardware)
                .FirstOrDefault(s => s.SensorType == sensorType && s.Value.HasValue);

            return sensor?.Value;
        }

        private static IEnumerable<ISensor> CollectSensors(IHardware hardware)
        {
            foreach (var sensor in hardware.Sensors)
            {
                yield return sensor;
            }

            foreach (var subHardware in hardware.SubHardware)
            {
                foreach (var sensor in CollectSensors(subHardware))
                {
                    yield return sensor;
                }
            }
        }

        private static IEnumerable<double> CollectSensorValues(IHardware hardware, SensorType sensorType)
        {
            return CollectSensors(hardware)
                .Where(s => s.SensorType == sensorType && s.Value.HasValue)
                .Select(s => (double)s.Value!.Value);
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