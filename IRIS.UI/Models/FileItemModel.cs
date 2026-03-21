using System.IO;
using IRIS.UI.Helpers;

namespace IRIS.UI.Models
{
    public class FileItemModel
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public bool IsDrive { get; set; }
        public long Length { get; set; }
        public DateTime LastWriteTimeUtc { get; set; }

        public string DisplayIcon => IsDrive ? "💿" : IsDirectory ? "📁" : "📄";

        public string TypeText => IsDrive ? "Drive" : IsDirectory ? "Folder" : GetFileType();

        public string SizeText => IsDirectory || IsDrive ? "" : FormatBytes(Length);

        public string DateText => IsDrive || LastWriteTimeUtc == default
            ? ""
            : DateTimeDisplayHelper.ToManilaFromUtc(LastWriteTimeUtc).ToString("yyyy-MM-dd HH:mm");

        private string GetFileType()
        {
            var ext = Path.GetExtension(Name);
            return string.IsNullOrEmpty(ext) ? "File" : ext.TrimStart('.').ToUpperInvariant() + " File";
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "0 B";
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
