namespace IRIS.Core.DTOs
{
    public record RoomDto(
        int Id,
        string RoomNumber,
        string? Description,
        int Capacity,
        bool IsActive,
        DateTime CreatedAt
    );
}
