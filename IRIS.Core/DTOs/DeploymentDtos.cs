namespace IRIS.Core.DTOs
{
    public record DeploymentPCDto(
        int Id,
        string? Hostname,
        string? IpAddress,
        string Status,
        int RoomId,
        string RoomNumber,
        DateTime LastSeen
    );

    public record DeploymentLogCreateDto(
        int? PCId,
        string PCName,
        string? IPAddress,
        string FileName,
        string Status,
        string? Details,
        DateTime? Timestamp = null
    );

    public record DeploymentLogDto(
        long Id,
        int? PCId,
        string PCName,
        string? IPAddress,
        string FileName,
        string Status,
        string? Details,
        DateTime Timestamp
    );
}
