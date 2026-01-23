using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IRIS.Core.Models
{
    public class PC
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string MacAddress { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? IpAddress { get; set; }

        [MaxLength(50)]
        public string? SubnetMask { get; set; }

        [MaxLength(50)]
        public string? DefaultGateway { get; set; }

        [Required]
        public int RoomId { get; set; }

        [MaxLength(100)]
        public string? Hostname { get; set; }

        [MaxLength(100)]
        public string? OperatingSystem { get; set; }

        public PCStatus Status { get; set; } = PCStatus.Offline;

        public DateTime LastSeen { get; set; } = DateTime.UtcNow;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("RoomId")]
        public virtual Room Room { get; set; } = null!;

        public virtual ICollection<HardwareMetric> HardwareMetrics { get; set; } = new List<HardwareMetric>();
        public virtual ICollection<NetworkMetric> NetworkMetrics { get; set; } = new List<NetworkMetric>();
        public virtual ICollection<SoftwareInstalled> SoftwareInstalled { get; set; } = new List<SoftwareInstalled>();
        public virtual ICollection<SoftwareUsageHistory> SoftwareUsageHistory { get; set; } = new List<SoftwareUsageHistory>();
        public virtual ICollection<WebsiteUsageHistory> WebsiteUsageHistory { get; set; } = new List<WebsiteUsageHistory>();
        public virtual ICollection<UserLog> UserLogs { get; set; } = new List<UserLog>();
        public virtual ICollection<Alert> Alerts { get; set; } = new List<Alert>();
        public virtual ICollection<PCHardwareConfig> HardwareConfigs { get; set; } = new List<PCHardwareConfig>();
    }

    public enum PCStatus
    {
        Online,
        Offline,
        Maintenance,
        Locked, 
        Warning
    }
}