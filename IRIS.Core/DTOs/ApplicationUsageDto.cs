namespace IRIS.Core.DTOs;

public class ApplicationUsageDto
{
    public string ApplicationName { get; set; } = string.Empty;
    public int UsageCount { get; set; }
    public double Percentage { get; set; }
}
