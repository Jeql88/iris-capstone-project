namespace IRIS.UI.Services.Contracts
{
    public interface IPowerCommandPollingServer
    {
        void Start();
        Task StopAsync();
    }
}