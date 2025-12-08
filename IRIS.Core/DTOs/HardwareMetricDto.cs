namespace IRIS.Core.DTOs
{
    public record HardwareMetricDto(
        int MetricId,
        int PCId,
        double CPUUsage,
        double RAMUsage,
        double? GPUUsage,
        double? CPUTemperature,
        double? GPUTemperature,
        DateTime Timestamp
    );

    public record HardwareMetricCreateDto(
        int PCId,
        double CPUUsage,
        double RAMUsage,
        double? GPUUsage,
        double? CPUTemperature,
        double? GPUTemperature
    );
}
