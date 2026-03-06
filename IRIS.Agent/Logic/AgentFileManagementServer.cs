using System.Net;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Serilog;

namespace IRIS.Agent.Logic
{
    public sealed class AgentFileManagementServer : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly int _port;
        private readonly string _rootPath;
        private readonly string _apiToken;
        private CancellationTokenSource? _cts;
        private Task? _listenTask;

        public AgentFileManagementServer(int port, string rootPath, string apiToken)
        {
            _port = port;
            _rootPath = Path.GetFullPath(rootPath);
            _apiToken = apiToken;

            Directory.CreateDirectory(_rootPath);
            _listener.Prefixes.Add($"http://+:{_port}/api/");
        }

        public Task StartAsync()
        {
            if (_listener.IsListening)
            {
                return Task.CompletedTask;
            }

            _cts = new CancellationTokenSource();
            _listener.Start();
            _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token));
            Log.Information("Agent file management API started on port {Port} with root {Root}", _port, _rootPath);
            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            try
            {
                _cts?.Cancel();
                if (_listener.IsListening)
                {
                    _listener.Stop();
                }

                if (_listenTask != null)
                {
                    await _listenTask;
                }
            }
            catch
            {
                // ignore shutdown races
            }
        }

        private async Task ListenLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                HttpListenerContext? context = null;
                try
                {
                    context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequestAsync(context), cancellationToken);
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "File API listener error");
                }
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            try
            {
                if (!IsAuthorized(context.Request))
                {
                    await WriteJsonAsync(context.Response, 401, new { error = "Unauthorized" });
                    return;
                }

                var method = context.Request.HttpMethod.ToUpperInvariant();
                var path = context.Request.Url?.AbsolutePath ?? "/";

                if (path.Equals("/api/files", StringComparison.OrdinalIgnoreCase) && method == "GET")
                {
                    await HandleListFilesAsync(context);
                    return;
                }

                if (path.Equals("/api/files/browse", StringComparison.OrdinalIgnoreCase) && method == "GET")
                {
                    await HandleBrowseAsync(context);
                    return;
                }

                if (path.Equals("/api/files/drives", StringComparison.OrdinalIgnoreCase) && method == "GET")
                {
                    await HandleDrivesAsync(context);
                    return;
                }

                if (path.Equals("/api/files/rename", StringComparison.OrdinalIgnoreCase) && method == "POST")
                {
                    await HandleRenameAsync(context);
                    return;
                }

                if (path.Equals("/api/files/exists", StringComparison.OrdinalIgnoreCase) && method == "GET")
                {
                    await HandleExistsAsync(context);
                    return;
                }

                if (path.Equals("/api/files/upload", StringComparison.OrdinalIgnoreCase) && method == "POST")
                {
                    await HandleUploadAsync(context);
                    return;
                }

                if (path.Equals("/api/files", StringComparison.OrdinalIgnoreCase) && method == "DELETE")
                {
                    await HandleDeleteAsync(context);
                    return;
                }

                if (path.Equals("/api/files/download", StringComparison.OrdinalIgnoreCase) && method == "GET")
                {
                    await HandleDownloadAsync(context);
                    return;
                }

                if (path.Equals("/api/deployment/cache-msi", StringComparison.OrdinalIgnoreCase) && method == "POST")
                {
                    await HandleCacheMsiAsync(context);
                    return;
                }

                if (path.Equals("/api/deployment/upload-msi", StringComparison.OrdinalIgnoreCase) && method == "POST")
                {
                    await HandleUploadMsiAsync(context);
                    return;
                }

                await WriteJsonAsync(context.Response, 404, new { error = "Not found" });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "File API request failed");
                await WriteJsonAsync(context.Response, 500, new { error = ex.Message });
            }
            finally
            {
                try { context.Response.Close(); } catch { }
            }
        }

        private bool IsAuthorized(HttpListenerRequest request)
        {
            if (string.IsNullOrWhiteSpace(_apiToken))
            {
                return true;
            }

            var token = request.Headers["X-IRIS-Token"];
            return string.Equals(token, _apiToken, StringComparison.Ordinal);
        }

        private async Task HandleListFilesAsync(HttpListenerContext context)
        {
            var inputPath = context.Request.QueryString["path"] ?? ".";
            string fullPath;
            try { fullPath = ResolveFullPath(inputPath); }
            catch (Exception ex)
            {
                await WriteJsonAsync(context.Response, 400, new { error = ex.Message });
                return;
            }

            if (!Directory.Exists(fullPath))
            {
                await WriteJsonAsync(context.Response, 404, new { error = "Directory not found" });
                return;
            }

            var dirInfo = new DirectoryInfo(fullPath);
            var entries = dirInfo
                .EnumerateFileSystemInfos()
                .Select(info => new
                {
                    Name = info.Name,
                    FullPath = info.FullName,
                    IsDirectory = (info.Attributes & FileAttributes.Directory) == FileAttributes.Directory,
                    Length = info is FileInfo fileInfo ? fileInfo.Length : 0,
                    LastWriteTimeUtc = info.LastWriteTimeUtc
                })
                .ToList();

            await WriteJsonAsync(context.Response, 200, entries);
        }

        private async Task HandleExistsAsync(HttpListenerContext context)
        {
            var inputPath = context.Request.QueryString["path"];
            if (string.IsNullOrWhiteSpace(inputPath))
            {
                await WriteJsonAsync(context.Response, 400, new { error = "path is required" });
                return;
            }

            string fullPath;
            try { fullPath = ResolveFullPath(inputPath); }
            catch (Exception ex)
            {
                await WriteJsonAsync(context.Response, 400, new { error = ex.Message });
                return;
            }

            var fileExists = File.Exists(fullPath);
            var dirExists = Directory.Exists(fullPath);

            await WriteJsonAsync(context.Response, 200, new
            {
                exists = fileExists || dirExists,
                isFile = fileExists,
                isDirectory = dirExists
            });
        }

        private async Task HandleUploadAsync(HttpListenerContext context)
        {
            var inputDir = context.Request.QueryString["path"] ?? ".";
            var fileName = context.Request.QueryString["fileName"];
            var overwrite = string.Equals(context.Request.QueryString["overwrite"], "true", StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(fileName))
            {
                await WriteJsonAsync(context.Response, 400, new { error = "fileName is required" });
                return;
            }

            string targetDir;
            try { targetDir = ResolveFullPath(inputDir); }
            catch (Exception ex)
            {
                await WriteJsonAsync(context.Response, 400, new { error = ex.Message });
                return;
            }

            Directory.CreateDirectory(targetDir);
            var targetPath = Path.Combine(targetDir, Path.GetFileName(fileName));

            if (File.Exists(targetPath) && !overwrite)
            {
                await WriteJsonAsync(context.Response, 409, new { error = "File already exists", path = targetPath });
                return;
            }

            await using var file = File.Create(targetPath);
            await context.Request.InputStream.CopyToAsync(file);

            await WriteJsonAsync(context.Response, 200, new { message = "Uploaded", path = targetPath });
        }

        private async Task HandleBrowseAsync(HttpListenerContext context)
        {
            var inputPath = context.Request.QueryString["path"] ?? ".";
            var search = context.Request.QueryString["search"]?.Trim();

            string fullPath;
            try { fullPath = ResolveFullPath(inputPath); }
            catch (Exception ex)
            {
                await WriteJsonAsync(context.Response, 400, new { error = ex.Message });
                return;
            }

            if (!Directory.Exists(fullPath))
            {
                await WriteJsonAsync(context.Response, 404, new { error = "Directory not found" });
                return;
            }

            var dirInfo = new DirectoryInfo(fullPath);
            var rawEntries = new List<(string Name, string FullPath, bool IsDirectory, long Length, DateTime LastWriteTimeUtc)>();

            try
            {
                foreach (var info in dirInfo.EnumerateFileSystemInfos())
                {
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(search) &&
                            !info.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                            continue;

                        rawEntries.Add((
                            info.Name,
                            info.FullName,
                            info.Attributes.HasFlag(FileAttributes.Directory),
                            info is FileInfo fi ? fi.Length : 0L,
                            info.LastWriteTimeUtc
                        ));
                    }
                    catch { /* skip inaccessible entries */ }
                }
            }
            catch (UnauthorizedAccessException)
            {
                await WriteJsonAsync(context.Response, 403, new { error = "Access denied to directory" });
                return;
            }

            var entries = rawEntries
                .OrderByDescending(e => e.IsDirectory)
                .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .Select(e => new
                {
                    e.Name,
                    e.FullPath,
                    e.IsDirectory,
                    e.Length,
                    e.LastWriteTimeUtc
                })
                .ToList();

            await WriteJsonAsync(context.Response, 200, new
            {
                CurrentPath = dirInfo.FullName,
                ParentPath = dirInfo.Parent?.FullName,
                Entries = entries
            });
        }

        private async Task HandleDeleteAsync(HttpListenerContext context)
        {
            var inputPath = context.Request.QueryString["path"];
            if (string.IsNullOrWhiteSpace(inputPath))
            {
                await WriteJsonAsync(context.Response, 400, new { error = "path is required" });
                return;
            }

            string fullPath;
            try { fullPath = ResolveFullPath(inputPath); }
            catch (Exception ex)
            {
                await WriteJsonAsync(context.Response, 400, new { error = ex.Message });
                return;
            }

            if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, true);
                await WriteJsonAsync(context.Response, 200, new { message = "Directory deleted" });
                return;
            }

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                await WriteJsonAsync(context.Response, 200, new { message = "File deleted" });
                return;
            }

            await WriteJsonAsync(context.Response, 404, new { error = "Path not found" });
        }

        private async Task HandleDownloadAsync(HttpListenerContext context)
        {
            var inputPath = context.Request.QueryString["path"];
            if (string.IsNullOrWhiteSpace(inputPath))
            {
                await WriteJsonAsync(context.Response, 400, new { error = "path is required" });
                return;
            }

            string fullPath;
            try { fullPath = ResolveFullPath(inputPath); }
            catch (Exception ex)
            {
                await WriteJsonAsync(context.Response, 400, new { error = ex.Message });
                return;
            }

            if (File.Exists(fullPath))
            {
                await using var source = File.OpenRead(fullPath);
                await WriteBinaryAsync(context.Response, source, Path.GetFileName(fullPath), "application/octet-stream");
                return;
            }

            if (Directory.Exists(fullPath))
            {
                var folderName = Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar));
                if (string.IsNullOrWhiteSpace(folderName))
                {
                    folderName = "folder";
                }

                var tempZipPath = Path.Combine(Path.GetTempPath(), $"iris-download-{Guid.NewGuid():N}.zip");
                try
                {
                    ZipFile.CreateFromDirectory(fullPath, tempZipPath, CompressionLevel.Fastest, includeBaseDirectory: true);
                    await using var source = File.OpenRead(tempZipPath);
                    await WriteBinaryAsync(context.Response, source, folderName + ".zip", "application/zip");
                }
                finally
                {
                    try
                    {
                        if (File.Exists(tempZipPath))
                        {
                            File.Delete(tempZipPath);
                        }
                    }
                    catch
                    {
                        // ignore temp cleanup failures
                    }
                }

                return;
            }

            await WriteJsonAsync(context.Response, 404, new { error = "Path not found" });
        }

        private async Task HandleCacheMsiAsync(HttpListenerContext context)
        {
            using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();
            var request = JsonSerializer.Deserialize<CacheMsiRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request == null || string.IsNullOrWhiteSpace(request.SourceUncPath))
            {
                await WriteJsonAsync(context.Response, 400, new { error = "Invalid request" });
                return;
            }

            var packagesRelativeDir = "packages";
            var fileName = string.IsNullOrWhiteSpace(request.LocalFileName)
                ? Path.GetFileName(request.SourceUncPath)
                : request.LocalFileName;

            var cacheFilePath = ResolvePath(Path.Combine(packagesRelativeDir, fileName));
            Directory.CreateDirectory(Path.GetDirectoryName(cacheFilePath)!);

            var alreadyExists = File.Exists(cacheFilePath);
            if (!alreadyExists)
            {
                File.Copy(request.SourceUncPath, cacheFilePath, overwrite: false);
            }

            await WriteJsonAsync(context.Response, 200, new
            {
                LocalPath = cacheFilePath,
                AlreadyExists = alreadyExists
            });
        }

        private async Task HandleUploadMsiAsync(HttpListenerContext context)
        {
            var requestedName = context.Request.QueryString["fileName"];
            if (string.IsNullOrWhiteSpace(requestedName))
            {
                await WriteJsonAsync(context.Response, 400, new { error = "fileName is required" });
                return;
            }

            var safeFileName = Path.GetFileName(requestedName);
            if (!safeFileName.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
            {
                await WriteJsonAsync(context.Response, 400, new { error = "Only .msi files are allowed" });
                return;
            }

            var cacheRelativeDir = "packages";
            var cacheFilePath = ResolvePath(Path.Combine(cacheRelativeDir, safeFileName));
            Directory.CreateDirectory(Path.GetDirectoryName(cacheFilePath)!);

            await using (var file = File.Create(cacheFilePath))
            {
                await context.Request.InputStream.CopyToAsync(file);
            }

            await WriteJsonAsync(context.Response, 200, new
            {
                LocalPath = cacheFilePath,
                AlreadyExists = false
            });
        }

        private string ResolvePath(string relativePath)
        {
            var normalized = (relativePath ?? string.Empty).Trim();
            normalized = normalized.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
            var combined = Path.GetFullPath(Path.Combine(_rootPath, normalized));

            if (!combined.StartsWith(_rootPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("Path escapes managed root.");
            }

            return combined;
        }

        /// <summary>
        /// Resolves a path that may be absolute (e.g. C:\Users) or relative to root.
        /// Absolute paths are used directly; relative paths are sandboxed to _rootPath.
        /// </summary>
        private string ResolveFullPath(string inputPath)
        {
            var normalized = (inputPath ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(normalized) || normalized == ".")
                return _rootPath;

            // Handle absolute paths (C:\... or \\share\...)
            if (Path.IsPathFullyQualified(normalized))
            {
                // Normalize drive-only paths like "C:" to "C:\\"
                if (normalized.Length == 2 && normalized[1] == ':')
                    normalized += "\\";
                return Path.GetFullPath(normalized);
            }

            // Relative: resolve against root with sandboxing
            normalized = normalized.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
            var combined = Path.GetFullPath(Path.Combine(_rootPath, normalized));
            if (!combined.StartsWith(_rootPath, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Path escapes managed root.");
            return combined;
        }

        private async Task HandleDrivesAsync(HttpListenerContext context)
        {
            var drives = DriveInfo.GetDrives()
                .Where(d => { try { return d.IsReady; } catch { return false; } })
                .Select(d =>
                {
                    try
                    {
                        return new
                        {
                            Name = d.Name,
                            Label = d.VolumeLabel,
                            TotalSize = d.TotalSize,
                            FreeSpace = d.AvailableFreeSpace
                        };
                    }
                    catch { return null; }
                })
                .Where(d => d != null)
                .ToList();

            await WriteJsonAsync(context.Response, 200, drives);
        }

        private async Task HandleRenameAsync(HttpListenerContext context)
        {
            var inputPath = context.Request.QueryString["path"];
            var newName = context.Request.QueryString["newName"];

            if (string.IsNullOrWhiteSpace(inputPath) || string.IsNullOrWhiteSpace(newName))
            {
                await WriteJsonAsync(context.Response, 400, new { error = "path and newName are required" });
                return;
            }

            string fullPath;
            try { fullPath = ResolveFullPath(inputPath); }
            catch (Exception ex)
            {
                await WriteJsonAsync(context.Response, 400, new { error = ex.Message });
                return;
            }

            var parentDir = Path.GetDirectoryName(fullPath);
            if (parentDir == null)
            {
                await WriteJsonAsync(context.Response, 400, new { error = "Cannot determine parent directory" });
                return;
            }

            var newPath = Path.Combine(parentDir, Path.GetFileName(newName));

            if (Directory.Exists(fullPath))
            {
                Directory.Move(fullPath, newPath);
            }
            else if (File.Exists(fullPath))
            {
                File.Move(fullPath, newPath);
            }
            else
            {
                await WriteJsonAsync(context.Response, 404, new { error = "Path not found" });
                return;
            }

            await WriteJsonAsync(context.Response, 200, new { message = "Renamed", newPath });
        }

        private string MakeRelative(string fullPath)
        {
            var normalized = Path.GetFullPath(fullPath);
            if (!normalized.StartsWith(_rootPath, StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            var relative = normalized[_rootPath.Length..].TrimStart(Path.DirectorySeparatorChar);
            return string.IsNullOrWhiteSpace(relative) ? "." : relative;
        }

        private string? GetParentRelative(string fullPath)
        {
            var normalized = Path.GetFullPath(fullPath);
            var rootNormalized = Path.GetFullPath(_rootPath);
            if (string.Equals(normalized, rootNormalized, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var parent = Directory.GetParent(normalized);
            if (parent == null)
            {
                return null;
            }

            var parentPath = Path.GetFullPath(parent.FullName);
            if (!parentPath.StartsWith(rootNormalized, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return MakeRelative(parentPath);
        }

        private static async Task WriteJsonAsync(HttpListenerResponse response, int statusCode, object payload)
        {
            response.StatusCode = statusCode;
            response.ContentType = "application/json";
            var json = JsonSerializer.Serialize(payload);
            var bytes = Encoding.UTF8.GetBytes(json);
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        }

        private static async Task WriteBinaryAsync(HttpListenerResponse response, Stream source, string downloadFileName, string contentType)
        {
            response.StatusCode = 200;
            response.ContentType = contentType;
            response.AddHeader("Content-Disposition", $"attachment; filename=\"{downloadFileName.Replace("\"", string.Empty)}\"");
            response.ContentLength64 = source.Length;
            await source.CopyToAsync(response.OutputStream);
        }

        public void Dispose()
        {
            _cts?.Cancel();
            if (_listener.IsListening)
            {
                _listener.Stop();
            }
            _listener.Close();
            _cts?.Dispose();
        }

        private sealed class CacheMsiRequest
        {
            public string SourceUncPath { get; set; } = string.Empty;
            public string LocalFileName { get; set; } = string.Empty;
        }
    }
}
