namespace IRIS.UI.Services.Contracts
{
    public interface ILocalMachineIdentityService
    {
        bool IsLocalMachine(string macAddress);
    }
}
