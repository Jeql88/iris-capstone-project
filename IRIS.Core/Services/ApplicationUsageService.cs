using IRIS.Core.Data;
using IRIS.Core.DTOs;
using IRIS.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace IRIS.Core.Services;

public class ApplicationUsageService : IApplicationUsageService
{
    private readonly IRISDbContext _context;

    public ApplicationUsageService(IRISDbContext context)
    {
        _context = context;
    }

    public async Task RecordApplicationUsageAsync(ApplicationUsageCreateDto dto)
    {
        var usage = new SoftwareUsageHistory
        {
            PCId = dto.PCId,
            ApplicationName = dto.ApplicationName,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            Duration = dto.Duration,
            CreatedAt = DateTime.UtcNow
        };

        _context.SoftwareUsageHistory.Add(usage);
        await _context.SaveChangesAsync();
    }

    public async Task RecordApplicationUsageBatchAsync(List<ApplicationUsageCreateDto> dtos)
    {
        var usages = dtos.Select(dto => new SoftwareUsageHistory
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
}
