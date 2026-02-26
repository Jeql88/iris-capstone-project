using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IRIS.Core.Models
{
    public class HardwareMetric
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PCId { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // CPU Metrics
        public double? CpuUsage { get; set; } // Percentage (0-100)
        public double? CpuTemperature { get; set; } // Celsius
        public string? CpuTemperatureSource { get; set; }

        // Memory Metrics
        public double? MemoryUsage { get; set; } // Percentage (0-100)
        public long? MemoryUsed { get; set; } // Bytes
        public long? MemoryTotal { get; set; } // Bytes

        // GPU Metrics
        public double? GpuUsage { get; set; } // Percentage (0-100)
        public double? GpuTemperature { get; set; } // Celsius
        public string? GpuTemperatureSource { get; set; }
        public long? GpuMemoryUsed { get; set; } // Bytes
        public long? GpuMemoryTotal { get; set; } // Bytes

        // Disk Metrics
        public double? DiskUsage { get; set; } // Percentage (0-100)
        public long? DiskUsed { get; set; } // Bytes
        public long? DiskTotal { get; set; } // Bytes
        public double? DiskReadSpeed { get; set; } // MB/s
        public double? DiskWriteSpeed { get; set; } // MB/s

        // Navigation property
        [ForeignKey("PCId")]
        public virtual PC PC { get; set; } = null!;
    }
}