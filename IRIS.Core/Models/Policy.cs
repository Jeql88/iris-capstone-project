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

        public bool EnableAccessControl { get; set; } = false;

        public bool AutoShutdownEnabled { get; set; } = false;

        public int? AutoShutdownIdleMinutes { get; set; } = 30;

        public TimeSpan? ScheduledShutdownTime { get; set; }

        public bool BlockUnauthorizedApplications { get; set; } = false;

        public bool MonitorApplicationUsage { get; set; } = true;

        public bool MonitorWebsiteUsage { get; set; } = true;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        [ForeignKey("RoomId")]
        public virtual Room Room { get; set; } = null!;
    }
}