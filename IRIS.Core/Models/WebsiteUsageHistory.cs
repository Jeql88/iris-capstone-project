using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IRIS.Core.Models
{
    public class WebsiteUsageHistory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PCId { get; set; }

        [Required]
        [MaxLength(500)]
        public string Url { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Domain { get; set; }

        public DateTime VisitedAt { get; set; } = DateTime.UtcNow;

        public int VisitCount { get; set; } = 1;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        [ForeignKey("PCId")]
        public virtual PC PC { get; set; } = null!;
    }
}