using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Microsoft.Win32;

namespace IRIS.UI.Helpers
{
    public static class IconExtractor
    {
        private static readonly ConcurrentDictionary<string, byte[]?> _cache = new(StringComparer.OrdinalIgnoreCase);

        public static byte[]? TryExtractForApplication(string applicationName)
        {
            if (string.IsNullOrWhiteSpace(applicationName)) return null;
            return _cache.GetOrAdd(applicationName, ExtractInternal);
        }

        private static byte[]? ExtractInternal(string applicationName)
        {
            var exePath = ResolveExecutablePath(applicationName);
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                return null;
            }

            try
            {
                using var icon = Icon.ExtractAssociatedIcon(exePath);
                if (icon == null) return null;

                using var bitmap = icon.ToBitmap();
                using var ms = new MemoryStream();
                bitmap.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
            catch
            {
                return null;
            }
        }

        private static string? ResolveExecutablePath(string applicationName)
        {
            var candidate = applicationName.Trim();
            if (!candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                candidate += ".exe";
            }

            // 1. App Paths registry
            foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
            {
                try
                {
                    using var key = root.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{candidate}");
                    if (key != null)
                    {
                        var path = key.GetValue(null) as string;
                        if (!string.IsNullOrEmpty(path))
                        {
                            path = path.Trim('"');
                            if (File.Exists(path)) return path;
                        }
                    }
                }
                catch { }
            }

            // 2. System32
            try
            {
                var sys32 = Path.Combine(Environment.SystemDirectory, candidate);
                if (File.Exists(sys32)) return sys32;
            }
            catch { }

            // 3. Common Program Files scans (shallow two-level)
            foreach (var root in new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            })
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
                try
                {
                    foreach (var dir in Directory.EnumerateDirectories(root))
                    {
                        var direct = Path.Combine(dir, candidate);
                        if (File.Exists(direct)) return direct;
                        try
                        {
                            foreach (var sub in Directory.EnumerateDirectories(dir))
                            {
                                var nested = Path.Combine(sub, candidate);
                                if (File.Exists(nested)) return nested;
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }

            return null;
        }
    }
}
