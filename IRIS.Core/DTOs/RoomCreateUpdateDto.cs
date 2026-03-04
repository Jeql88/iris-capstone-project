namespace IRIS.Core.DTOs
{
    public record RoomCreateUpdateDto(
        string RoomNumber,
        string? Description,
        int Capacity,
        bool IsActive
    );
}
