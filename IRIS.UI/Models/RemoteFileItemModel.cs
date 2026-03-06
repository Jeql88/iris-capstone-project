namespace IRIS.UI.Models
{
    public class RemoteFileItemModel
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public long Length { get; set; }
        public DateTime LastWriteTimeUtc { get; set; }

        public string TypeText => IsDirectory ? "Folder" : "File";
        public string SizeText => IsDirectory ? "-" : FormatBytes(Length);

        private static string FormatBytes(long bytes)
        {
            string[] sizes = ["B", "KB", "MB", "GB", "TB"];
            var len = (double)bytes;
            var order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }
    }
}
