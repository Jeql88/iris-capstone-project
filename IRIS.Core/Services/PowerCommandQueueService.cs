using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using IRIS.Core.Data;
using IRIS.Core.Models;
using IRIS.Core.Services.Contracts;

namespace IRIS.Core.Services
{
    public class PowerCommandQueueService : IPowerCommandQueueService
    {
        private static readonly TimeSpan CommandTtl = TimeSpan.FromMinutes(3);

        private readonly IServiceScopeFactory _scopeFactory;

        public PowerCommandQueueService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        public async Task<bool> QueueCommandAsync(string macAddress, string commandType)
        {
            if (string.IsNullOrWhiteSpace(macAddress) || string.IsNullOrWhiteSpace(commandType))
                return false;

            var normalizedCommand = commandType.Trim();

            // Parse command type and optional payload (e.g., "FreezeOn::base64msg")
            string parsedCommandType;
            string? payload = null;

            if (normalizedCommand.StartsWith("FreezeOn::", StringComparison.OrdinalIgnoreCase))
            {
                parsedCommandType = "FreezeOn";
                payload = normalizedCommand.Substring("FreezeOn::".Length);
            }
            else if (normalizedCommand.StartsWith("Message::", StringComparison.OrdinalIgnoreCase))
            {
                parsedCommandType = "Message";
                payload = normalizedCommand.Substring("Message::".Length);
            }
            else if (normalizedCommand.Equals("Shutdown", StringComparison.OrdinalIgnoreCase))
                parsedCommandType = "Shutdown";
            else if (normalizedCommand.Equals("Restart", StringComparison.OrdinalIgnoreCase))
                parsedCommandType = "Restart";
            else if (normalizedCommand.Equals("RefreshMetrics", StringComparison.OrdinalIgnoreCase))
                parsedCommandType = "RefreshMetrics";
            else if (normalizedCommand.Equals("FreezeOn", StringComparison.OrdinalIgnoreCase))
                parsedCommandType = "FreezeOn";
            else if (normalizedCommand.Equals("FreezeOff", StringComparison.OrdinalIgnoreCase))
                parsedCommandType = "FreezeOff";
            else
                return false;

            var normalizedMac = NormalizeMacAddress(macAddress);
            if (string.IsNullOrWhiteSpace(normalizedMac))
                return false;

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IRISDbContext>();

            var now = DateTime.UtcNow;
            context.PendingCommands.Add(new PendingCommand
            {
                MacAddress = normalizedMac,
                CommandType = parsedCommandType,
                Payload = payload,
                CreatedAtUtc = now,
                ExpiresAtUtc = now + CommandTtl,
                Status = PendingCommandStatus.Pending
            });

            await context.SaveChangesAsync();
            return true;
        }

        public async Task<string?> DequeuePendingCommandAsync(string macAddress)
        {
            if (string.IsNullOrWhiteSpace(macAddress))
                return null;

            var normalizedMac = NormalizeMacAddress(macAddress);
            if (string.IsNullOrWhiteSpace(normalizedMac))
                return null;

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IRISDbContext>();

            var now = DateTime.UtcNow;
            var pending = await context.PendingCommands
                .Where(c => c.MacAddress == normalizedMac
                            && c.Status == PendingCommandStatus.Pending
                            && c.ExpiresAtUtc > now)
                .OrderBy(c => c.CreatedAtUtc)
                .FirstOrDefaultAsync();

            if (pending == null)
                return null;

            pending.Status = PendingCommandStatus.Consumed;
            await context.SaveChangesAsync();

            // Reconstruct the command string with payload if present
            return string.IsNullOrWhiteSpace(pending.Payload)
                ? pending.CommandType
                : $"{pending.CommandType}::{pending.Payload}";
        }

        public async Task<int> CleanupExpiredCommandsAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IRISDbContext>();

            var now = DateTime.UtcNow;
            var oneDayAgo = now.AddDays(-1);

            // Mark expired pending commands
            var expiredPending = await context.PendingCommands
                .Where(c => c.Status == PendingCommandStatus.Pending && c.ExpiresAtUtc < now)
                .ToListAsync();

            foreach (var cmd in expiredPending)
                cmd.Status = PendingCommandStatus.Expired;

            // Delete old consumed/expired commands (older than 1 day)
            var oldCommands = await context.PendingCommands
                .Where(c => (c.Status == PendingCommandStatus.Consumed || c.Status == PendingCommandStatus.Expired)
                            && c.CreatedAtUtc < oneDayAgo)
                .ToListAsync();

            context.PendingCommands.RemoveRange(oldCommands);

            await context.SaveChangesAsync();
            return expiredPending.Count + oldCommands.Count;
        }

        private static string NormalizeMacAddress(string macAddress)
        {
            var normalized = new string(macAddress
                .Where(char.IsLetterOrDigit)
                .ToArray());

            return normalized.ToUpperInvariant();
        }
    }
}
