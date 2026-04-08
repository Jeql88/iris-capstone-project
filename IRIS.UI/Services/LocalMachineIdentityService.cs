using System.Net.NetworkInformation;
using IRIS.UI.Services.Contracts;

namespace IRIS.UI.Services
{
    public sealed class LocalMachineIdentityService : ILocalMachineIdentityService
    {
        private readonly HashSet<string> _localMacs;

        public LocalMachineIdentityService()
        {
            _localMacs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up)
                    continue;

                if (nic.NetworkInterfaceType != NetworkInterfaceType.Ethernet &&
                    nic.NetworkInterfaceType != NetworkInterfaceType.Wireless80211)
                    continue;

                var mac = nic.GetPhysicalAddress().ToString();
                if (!string.IsNullOrEmpty(mac))
                    _localMacs.Add(mac);
            }
        }

        public bool IsLocalMachine(string macAddress)
        {
            if (string.IsNullOrWhiteSpace(macAddress))
                return false;

            var normalized = NormalizeMac(macAddress);
            return _localMacs.Contains(normalized);
        }

        private static string NormalizeMac(string mac) =>
            new string(mac.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    }
}
