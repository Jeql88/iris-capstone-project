using System.Runtime.Versioning;
using IRIS.Core.Data;
using IRIS.Core.DTOs;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace IRIS.Agent.Logic;

[SupportedOSPlatform("windows")]
public class ApplicationUsageLogic : IDisposable
{
    private readonly IRISDbContext _context;
    private readonly string _macAddress;
    private readonly ProcessMonitor _processMonitor;
    private System.Threading.Timer? _scanTimer;
    private System.Threading.Timer? _sendTimer;
    private int? _pcId;

    public ApplicationUsageLogic(IRISDbContext context, string macAddress)
    {
        _context = context;
        _macAddress = macAddress;
        _processMonitor = new ProcessMonitor();
    }

    public async Task StartMonitoringAsync()
    {
        _pcId = await GetPCIdAsync();
        if (_pcId == null)
        {
            Log.Warning("PC not registered. Cannot start application usage monitoring.");
            return;
        }

        // Scan processes every 10 seconds
        _scanTimer = new System.Threading.Timer(_ => _processMonitor.ScanProcesses(), null, TimeSpan.Zero, TimeSpan.FromSeconds(10));

        // Send data to server every 1 minute
        _sendTimer = new System.Threading.Timer(async _ => await SendUsageDataAsync(), null, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1));

        Log.Information("Application usage monitoring started");
    }

    public Task StopMonitoringAsync()
    {
        _scanTimer?.Dispose();
        _sendTimer?.Dispose();
        Log.Information("Application usage monitoring stopped");
        return Task.CompletedTask;
    }

    private async Task<int?> GetPCIdAsync()
    {
        var pc = await _context.PCs.FirstOrDefaultAsync(p => p.MacAddress == _macAddress);
        return pc?.Id;
    }

    private async Task SendUsageDataAsync()
    {
        if (_pcId == null) return;

        try
        {
            var records = _processMonitor.GetCompletedRecords();
            if (records.Count == 0) return;

            var usages = records.Select(r => new ApplicationUsageCreateDto
            {
                PCId = _pcId.Value,
                ApplicationName = r.ApplicationName,
                StartTime = r.StartTime,
                EndTime = r.EndTime,
                Duration = r.Duration
            }).ToList();

            await SaveUsageBatchAsync(usages);
            Log.Information("Sent {Count} application usage records to database", usages.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to send application usage data");
        }
    }

    private async Task SaveUsageBatchAsync(List<ApplicationUsageCreateDto> dtos)
    {
        var usages = dtos.Select(dto => new Core.Models.SoftwareUsageHistory
        {
            PCId = dto.PCId,
            ApplicationName = dto.ApplicationName,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            Duration = dto.Duration,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        _context.SoftwareUsageHistory.AddRange(usages);
        await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _scanTimer?.Dispose();
        _sendTimer?.Dispose();
    }
}
