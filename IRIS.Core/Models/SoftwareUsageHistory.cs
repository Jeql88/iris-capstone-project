using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IRIS.Core.Models
{
    public class SoftwareUsageHistory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PCId { get; set; }

        [Required]
        [MaxLength(200)]
        public string ApplicationName { get; set; } = string.Empty;

        public DateTime StartTime { get; set; }

        public DateTime? EndTime { get; set; }

        public TimeSpan? Duration { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        [ForeignKey("PCId")]
        public virtual PC PC { get; set; } = null!;
    }
}