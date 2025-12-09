namespace IRIS.Core.DTOs
{
    public record PCHardwareConfigDto(
        int Id,
        int PCId,
        string? Processor,
        string? GraphicsCard,
        string? Motherboard,
        long? RamCapacity,
        long? StorageCapacity,
        string? StorageType,
        DateTime AppliedAt,
        bool IsActive
    );

    public record PCHardwareConfigCreateDto(
        int PCId,
        string? Processor,
        string? GraphicsCard,
        string? Motherboard,
        long? RamCapacity,
        long? StorageCapacity,
        string? StorageType
    );

}
