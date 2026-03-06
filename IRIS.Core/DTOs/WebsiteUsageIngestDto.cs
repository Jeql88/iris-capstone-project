namespace IRIS.Core.DTOs;

public class WebsiteUsageIngestDto
{
    public int PCId { get; set; }
    public string Browser { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public DateTime VisitedAt { get; set; }
    public int VisitCount { get; set; }
}
