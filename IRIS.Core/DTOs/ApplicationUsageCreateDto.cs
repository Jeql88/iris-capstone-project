namespace IRIS.Core.DTOs;

public class ApplicationUsageCreateDto
{
    public int PCId { get; set; }
    public string ApplicationName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan? Duration { get; set; }
}
