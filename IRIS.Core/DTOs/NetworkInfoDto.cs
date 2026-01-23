namespace IRIS.Core.DTOs
{
    public record NetworkInfoDto(
        string IpAddress,
        string MacAddress,
        string SubnetMask,
        string DefaultGateway
    );
}
