using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IRIS.Core.Models
{
    public class Room
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string RoomNumber { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Description { get; set; }

        public int Capacity { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<PC> PCs { get; set; } = new List<PC>();
        public virtual ICollection<Policy> Policies { get; set; } = new List<Policy>();
    }
}