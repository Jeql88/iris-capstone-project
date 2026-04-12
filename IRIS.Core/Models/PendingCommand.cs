using System.ComponentModel.DataAnnotations;

namespace IRIS.Core.Models
{
    public class PendingCommand
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string MacAddress { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string CommandType { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Payload { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime ExpiresAtUtc { get; set; }

        public PendingCommandStatus Status { get; set; } = PendingCommandStatus.Pending;
    }

    public enum PendingCommandStatus
    {
        Pending,
        Consumed,
        Expired
    }
}
