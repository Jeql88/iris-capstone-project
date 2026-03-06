using System.Collections.Concurrent;
using IRIS.Core.Services.Contracts;

namespace IRIS.Core.Services
{
    public class PowerCommandQueueService : IPowerCommandQueueService
    {
        private static readonly TimeSpan CommandTtl = TimeSpan.FromMinutes(3);

        private readonly ConcurrentDictionary<string, PendingCommandEntry> _pendingCommands =
            new(StringComparer.OrdinalIgnoreCase);

        public Task<bool> QueueCommandAsync(string macAddress, string commandType)
        {
            if (string.IsNullOrWhiteSpace(macAddress) || string.IsNullOrWhiteSpace(commandType))
            {
                return Task.FromResult(false);
            }

            var normalizedCommand = commandType.Trim();
            if (!normalizedCommand.Equals("Shutdown", StringComparison.OrdinalIgnoreCase) &&
                !normalizedCommand.Equals("Restart", StringComparison.OrdinalIgnoreCase) &&
                !normalizedCommand.Equals("RefreshMetrics", StringComparison.OrdinalIgnoreCase) &&
                !normalizedCommand.Equals("FreezeOn", StringComparison.OrdinalIgnoreCase) &&
                !normalizedCommand.Equals("FreezeOff", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(false);
            }

            var normalizedMacAddress = macAddress.Trim();
            CleanupExpiredCommand(normalizedMacAddress);

            var finalCommand = normalizedCommand.Equals("Shutdown", StringComparison.OrdinalIgnoreCase)
                ? "Shutdown"
                : normalizedCommand.Equals("Restart", StringComparison.OrdinalIgnoreCase)
                    ? "Restart"
                    : normalizedCommand.Equals("RefreshMetrics", StringComparison.OrdinalIgnoreCase)
                        ? "RefreshMetrics"
                        : normalizedCommand.Equals("FreezeOn", StringComparison.OrdinalIgnoreCase)
                            ? "FreezeOn"
                            : "FreezeOff";

            _pendingCommands[normalizedMacAddress] = new PendingCommandEntry(
                finalCommand,
                DateTime.UtcNow);

            return Task.FromResult(true);
        }

        public Task<string?> DequeuePendingCommandAsync(string macAddress)
        {
            if (string.IsNullOrWhiteSpace(macAddress))
            {
                return Task.FromResult<string?>(null);
            }

            var normalizedMacAddress = macAddress.Trim();
            CleanupExpiredCommand(normalizedMacAddress);

            if (!_pendingCommands.TryRemove(normalizedMacAddress, out var pendingCommand))
            {
                return Task.FromResult<string?>(null);
            }

            return Task.FromResult<string?>(pendingCommand.CommandType);
        }

        private void CleanupExpiredCommand(string macAddress)
        {
            if (!_pendingCommands.TryGetValue(macAddress, out var existing))
            {
                return;
            }

            if (DateTime.UtcNow - existing.CreatedAtUtc > CommandTtl)
            {
                _pendingCommands.TryRemove(macAddress, out _);
            }
        }

        private sealed record PendingCommandEntry(string CommandType, DateTime CreatedAtUtc);
    }
}