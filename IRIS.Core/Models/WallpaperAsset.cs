using System.ComponentModel.DataAnnotations;

namespace IRIS.Core.Models
{
    public class WallpaperAsset
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(64)]
        public string Hash { get; set; } = string.Empty;

        public long FileSize { get; set; }

        [Required]
        [MaxLength(500)]
        public string FilePath { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? UploadedBy { get; set; }
    }
}