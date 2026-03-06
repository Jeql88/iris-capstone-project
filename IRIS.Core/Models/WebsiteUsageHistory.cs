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
        [MaxLength(20)]
        public string Browser { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(200)]
        public string Domain { get; set; } = string.Empty;

        public DateTime VisitedAt { get; set; }

        public int VisitCount { get; set; } = 1;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        [ForeignKey("PCId")]
        public virtual PC PC { get; set; } = null!;
    }
}