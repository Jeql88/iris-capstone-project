using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IRIS.Core.Models
{
    public class Alert
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PCId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(1000)]
        public string Message { get; set; } = string.Empty;

        public AlertSeverity Severity { get; set; } = AlertSeverity.Medium;

        public AlertType Type { get; set; }

        public bool IsAcknowledged { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? AcknowledgedAt { get; set; }

        // Navigation properties
        [ForeignKey("PCId")]
        public virtual PC PC { get; set; } = null!;

        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;
    }

    public enum AlertSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum AlertType
    {
        Hardware,
        Network,
        Software,
        Security,
        System
    }
}