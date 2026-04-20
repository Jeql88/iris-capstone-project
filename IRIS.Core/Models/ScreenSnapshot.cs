using System.ComponentModel.DataAnnotations;

namespace IRIS.Core.Models
{
    // DB-relayed screen-snapshot transport. UI upserts RequestedUntilUtc to subscribe;
    // agent pushes the latest JPEG into JpegBytes while the subscription is live.
    // Replaces the blocked HTTP:5057 pull path.
    public class ScreenSnapshot
    {
        [Key]
        [MaxLength(50)]
        public string MacAddress { get; set; } = string.Empty;

        public byte[]? JpegBytes { get; set; }

        public DateTime UpdatedAtUtc { get; set; }

        public DateTime RequestedUntilUtc { get; set; }
    }
}
