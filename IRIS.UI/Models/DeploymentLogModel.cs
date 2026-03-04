namespace IRIS.UI.Models
{
    public class DeploymentLogModel
    {
        public string PCName { get; set; } = string.Empty;
        public string IPAddress { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}
