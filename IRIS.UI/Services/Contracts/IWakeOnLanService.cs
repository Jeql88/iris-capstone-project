namespace IRIS.UI.Services.Contracts
{
    public interface IWakeOnLanService
    {
        Task<bool> SendWakeOnLanAsync(string macAddress);
    }
}
