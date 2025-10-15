using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IRIS.Core.Models
{
    public class PCHardwareConfig
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PCId { get; set; }

        [MaxLength(100)]
        public string? Processor { get; set; }

        [MaxLength(100)]
        public string? GraphicsCard { get; set; }

        [MaxLength(100)]
        public string? Motherboard { get; set; }

        public long? RamCapacity { get; set; } // In bytes

        public long? StorageCapacity { get; set; } // In bytes

        [MaxLength(50)]
        public string? StorageType { get; set; } // SSD, HDD, etc.

        public DateTime AppliedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        // Navigation property
        [ForeignKey("PCId")]
        public virtual PC PC { get; set; } = null!;
    }
}