namespace IRIS.Core.Services.Contracts
{
    public interface IPowerCommandQueueService
    {
        Task<bool> QueueCommandAsync(string macAddress, string commandType);
        Task<string?> DequeuePendingCommandAsync(string macAddress);
    }
}