using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IRIS.Core.Models
{
    public class NetworkMetric
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PCId { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Bandwidth Metrics
        public double? DownloadSpeed { get; set; } // Mbps
        public double? UploadSpeed { get; set; } // Mbps
        public long? BytesReceived { get; set; } // Total bytes
        public long? BytesSent { get; set; } // Total bytes

        // Latency and Packet Loss
        public double? Latency { get; set; } // Milliseconds
        public double? PacketLoss { get; set; } // Percentage (0-100)

        // Connection Status
        public bool? IsConnected { get; set; }
        public string? NetworkInterface { get; set; }

        // Navigation property
        [ForeignKey("PCId")]
        public virtual PC PC { get; set; } = null!;
    }
}