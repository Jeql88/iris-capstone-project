using System.ComponentModel.DataAnnotations;

namespace IRIS.Core.Models
{
    public class Software
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(50)]
        public string? Version { get; set; }

        [MaxLength(500)]
        public string? DownloadUrl { get; set; }

        public bool IsApproved { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<SoftwareInstalled> SoftwareInstalled { get; set; } = new List<SoftwareInstalled>();
        public virtual ICollection<SoftwareRequest> SoftwareRequests { get; set; } = new List<SoftwareRequest>();
    }
}