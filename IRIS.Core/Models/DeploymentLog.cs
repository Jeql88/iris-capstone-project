using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IRIS.Core.Models
{
    public class DeploymentLog
    {
        [Key]
        public long Id { get; set; }

        public int? PCId { get; set; }

        [MaxLength(120)]
        public string PCName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? IPAddress { get; set; }

        [Required]
        [MaxLength(260)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Pending";

        [MaxLength(4000)]
        public string? Details { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [ForeignKey("PCId")]
        public virtual PC? PC { get; set; }
    }
}
