namespace IRIS.Core.DTOs;

public class WebsiteUsageDto
{
    public string Domain { get; set; } = string.Empty;
    public int VisitCount { get; set; }
    public double Percentage { get; set; }
}
