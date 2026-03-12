namespace IRIS.Core.DTOs;

public class ApplicationUsageDetailDto
{
    public int Id { get; set; }
    public string ApplicationName { get; set; } = string.Empty;
    public string PCName { get; set; } = string.Empty;
    public string RoomNumber { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan? Duration { get; set; }
}

public class WebsiteUsageDetailDto
{
    public int Id { get; set; }
    public string Domain { get; set; } = string.Empty;
    public string Browser { get; set; } = string.Empty;
    public string PCName { get; set; } = string.Empty;
    public string RoomNumber { get; set; } = string.Empty;
    public DateTime VisitTime { get; set; }
    public int VisitCount { get; set; }
}

public class UsageMetricsSummaryDto
{
    public int TotalApplications { get; set; }
    public int TotalWebsites { get; set; }
}
