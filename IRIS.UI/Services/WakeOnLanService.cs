using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using IRIS.UI.Services.Contracts;

namespace IRIS.UI.Services
{
    public sealed class WakeOnLanService : IWakeOnLanService
    {
        private readonly ILogger<WakeOnLanService> _logger;

        public WakeOnLanService(ILogger<WakeOnLanService> logger)
        {
            _logger = logger;
        }

        public async Task<bool> SendWakeOnLanAsync(string macAddress)
        {
            try
            {
                var macBytes = ParseMacAddress(macAddress);
                if (macBytes == null)
                {
                    _logger.LogWarning("Invalid MAC address format: {Mac}", macAddress);
                    return false;
                }

                var packet = BuildMagicPacket(macBytes);

                using var client = new UdpClient();
                client.EnableBroadcast = true;
                await client.SendAsync(packet, packet.Length, new IPEndPoint(IPAddress.Broadcast, 9));

                _logger.LogInformation("WoL magic packet sent to {Mac}", macAddress);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send WoL packet to {Mac}", macAddress);
                return false;
            }
        }

        private static byte[] BuildMagicPacket(byte[] macBytes)
        {
            // 6 bytes of 0xFF + 16 repetitions of the 6-byte MAC = 102 bytes
            var packet = new byte[102];

            // Preamble: 6 bytes of 0xFF
            for (var i = 0; i < 6; i++)
                packet[i] = 0xFF;

            // 16 repetitions of the MAC address
            for (var i = 0; i < 16; i++)
                Array.Copy(macBytes, 0, packet, 6 + i * 6, 6);

            return packet;
        }

        private static byte[]? ParseMacAddress(string mac)
        {
            if (string.IsNullOrWhiteSpace(mac))
                return null;

            // Strip common delimiters
            var cleaned = mac.Replace(":", "").Replace("-", "").Replace(" ", "").Trim();

            if (cleaned.Length != 12)
                return null;

            try
            {
                var bytes = new byte[6];
                for (var i = 0; i < 6; i++)
                    bytes[i] = Convert.ToByte(cleaned.Substring(i * 2, 2), 16);
                return bytes;
            }
            catch
            {
                return null;
            }
        }
    }
}
