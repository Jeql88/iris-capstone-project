namespace IRIS.Core.DTOs
{
    public record NetworkMetricDto(
        int NetworkMetricId,
        int PCId,
        double BandwidthUsage,
        double? Latency,
        double? PacketLoss,
        DateTime Timestamp
    );

    public record NetworkMetricCreateDto(
        int PCId,
        double BandwidthUsage,
        double? Latency,
        double? PacketLoss
    );
}
