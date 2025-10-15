using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IRIS.Core.Models
{
    public class SoftwareInstalled
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PCId { get; set; }

        [Required]
        public int SoftwareId { get; set; }

        [MaxLength(50)]
        public string? InstalledVersion { get; set; }

        public DateTime InstalledAt { get; set; } = DateTime.UtcNow;

        public DateTime? UninstalledAt { get; set; }

        public bool IsActive { get; set; } = true;

        // Navigation properties
        [ForeignKey("PCId")]
        public virtual PC PC { get; set; } = null!;

        [ForeignKey("SoftwareId")]
        public virtual Software Software { get; set; } = null!;
    }
}