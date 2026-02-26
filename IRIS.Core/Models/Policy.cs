using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IRIS.Core.Models
{
    public class Policy
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [Required]
        public int RoomId { get; set; }

        // Policy Settings
        public bool ResetWallpaperOnStartup { get; set; } = false;

        [MaxLength(500)]
        public string? WallpaperPath { get; set; }

        public int? AutoShutdownIdleMinutes { get; set; } = 30;

        // Monitoring thresholds (warning / critical)
        public double CpuUsageWarningThreshold { get; set; } = 85;
        public double CpuUsageCriticalThreshold { get; set; } = 95;
        public double RamUsageWarningThreshold { get; set; } = 85;
        public double RamUsageCriticalThreshold { get; set; } = 95;
        public double DiskUsageWarningThreshold { get; set; } = 90;
        public double DiskUsageCriticalThreshold { get; set; } = 98;
        public double CpuTemperatureWarningThreshold { get; set; } = 80;
        public double CpuTemperatureCriticalThreshold { get; set; } = 90;
        public double GpuTemperatureWarningThreshold { get; set; } = 80;
        public double GpuTemperatureCriticalThreshold { get; set; } = 90;
        public double LatencyWarningThreshold { get; set; } = 150;
        public double LatencyCriticalThreshold { get; set; } = 300;
        public double PacketLossWarningThreshold { get; set; } = 3;
        public double PacketLossCriticalThreshold { get; set; } = 10;
        public int WarningSustainSeconds { get; set; } = 30;
        public int CriticalSustainSeconds { get; set; } = 20;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        [ForeignKey("RoomId")]
        public virtual Room Room { get; set; } = null!;
    }
}