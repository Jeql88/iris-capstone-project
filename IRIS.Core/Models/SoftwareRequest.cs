using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IRIS.Core.Models
{
    public class SoftwareRequest
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int SoftwareId { get; set; }

        [MaxLength(500)]
        public string? Reason { get; set; }

        public SoftwareRequestStatus Status { get; set; } = SoftwareRequestStatus.Pending;

        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ReviewedAt { get; set; }

        public int? ReviewedByUserId { get; set; }

        // Navigation properties
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        [ForeignKey("SoftwareId")]
        public virtual Software Software { get; set; } = null!;

        [ForeignKey("ReviewedByUserId")]
        public virtual User? ReviewedByUser { get; set; }
    }

    public enum SoftwareRequestStatus
    {
        Pending,
        Approved,
        Rejected
    }
}