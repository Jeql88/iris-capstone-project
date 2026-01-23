namespace IRIS.Core.DTOs
{
    public record PCDto(
        int Id,
        string MacAddress,
        string? IpAddress,
        string? SubnetMask,
        string? DefaultGateway,
        int RoomId,
        string? Hostname,
        string? OperatingSystem,
        string Status,
        DateTime LastSeen
    );

    public record PCCreateDto(
        string MacAddress,
        string? IpAddress,
        string? SubnetMask,
        string? DefaultGateway,
        int RoomId,
        string? Hostname,
        string? OperatingSystem
    );

    public record PCUpdateDto(
        string? IpAddress,
        string? SubnetMask,
        string? DefaultGateway,
        string? Hostname,
        string? OperatingSystem,
        string Status
    );
}
