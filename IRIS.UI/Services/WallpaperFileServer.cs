using System.IO;
using System.Net;
using IRIS.UI.Services.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IRIS.UI.Services
{
    public sealed class WallpaperFileServer : IWallpaperFileServer
    {
        private readonly string _rootPath;
        private readonly int _port;
        private readonly string _routePrefix;
        private readonly string _apiToken;
        private readonly ILogger<WallpaperFileServer> _logger;

        private HttpListener? _listener;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _serverTask;

        public WallpaperFileServer(IConfiguration configuration, ILogger<WallpaperFileServer> logger)
        {
            _logger = logger;

            var configuredRootPath = configuration["WallpaperServer:RootPath"];
            _rootPath = ResolveRootPath(configuredRootPath);

            _port = int.TryParse(configuration["WallpaperServer:Port"], out var configuredPort)
                ? configuredPort
                : 5092;

            _routePrefix = NormalizeRoutePrefix(configuration["WallpaperServer:RoutePrefix"]);
            _apiToken = (configuration["WallpaperServer:ApiToken"] ?? string.Empty).Trim();
        }

        public void Start()
        {
            if (_listener != null)
            {
                return;
            }

            Directory.CreateDirectory(_rootPath);

            _cancellationTokenSource = new CancellationTokenSource();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://+:{_port}/");
            _listener.Start();

            _serverTask = Task.Run(() => AcceptLoopAsync(_cancellationTokenSource.Token));

            _logger.LogInformation(
                "Wallpaper file server started on port {Port}, route prefix '{RoutePrefix}', root '{RootPath}'.",
                _port,
                _routePrefix,
                _rootPath);
        }

        public async Task StopAsync()
        {
            if (_listener == null)
            {
                return;
            }

            _cancellationTokenSource?.Cancel();

            try
            {
                _listener.Stop();
                _listener.Close();
            }
            catch
            {
                // Ignore shutdown exceptions.
            }

            if (_serverTask != null)
            {
                await _serverTask;
            }

            _serverTask = null;
            _listener = null;

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }

        private async Task AcceptLoopAsync(CancellationToken cancellationToken)
        {
            if (_listener == null)
            {
                return;
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                HttpListenerContext? context = null;
                try
                {
                    context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequestAsync(context, cancellationToken), cancellationToken);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (HttpListenerException)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Wallpaper file server accept loop error.");
                }
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
        {
            try
            {
                if (!HttpMethodsSupported(context.Request.HttpMethod))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                    context.Response.Close();
                    return;
                }

                if (!ValidateToken(context.Request))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    context.Response.Close();
                    return;
                }

                var relativePath = context.Request.Url?.AbsolutePath ?? string.Empty;
                if (!relativePath.StartsWith(_routePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    context.Response.Close();
                    return;
                }

                var encodedFileName = relativePath[_routePrefix.Length..].Trim('/');
                if (string.IsNullOrWhiteSpace(encodedFileName))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    context.Response.Close();
                    return;
                }

                var fileName = Uri.UnescapeDataString(encodedFileName);
                if (!IsSafeFileName(fileName))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    context.Response.Close();
                    return;
                }

                var fullPath = Path.Combine(_rootPath, fileName);
                if (!File.Exists(fullPath))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    context.Response.Close();
                    return;
                }

                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.ContentType = GetContentType(fullPath);
                context.Response.AddHeader("Cache-Control", "public, max-age=300");

                await using var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                context.Response.ContentLength64 = fileStream.Length;
                await fileStream.CopyToAsync(context.Response.OutputStream, cancellationToken);
                context.Response.OutputStream.Close();
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation during shutdown.
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Wallpaper file server request failed.");
                try
                {
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    context.Response.Close();
                }
                catch
                {
                    // Ignore connection errors.
                }
            }
        }

        private bool ValidateToken(HttpListenerRequest request)
        {
            if (string.IsNullOrWhiteSpace(_apiToken))
            {
                return true;
            }

            var headerToken = request.Headers["X-IRIS-Wallpaper-Token"];
            if (string.Equals(headerToken, _apiToken, StringComparison.Ordinal))
            {
                return true;
            }

            var authorization = request.Headers["Authorization"];
            if (!string.IsNullOrWhiteSpace(authorization) &&
                authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var bearerToken = authorization["Bearer ".Length..].Trim();
                return string.Equals(bearerToken, _apiToken, StringComparison.Ordinal);
            }

            return false;
        }

        private static bool HttpMethodsSupported(string method)
        {
            return method.Equals("GET", StringComparison.OrdinalIgnoreCase)
                || method.Equals("HEAD", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSafeFileName(string fileName)
        {
            if (fileName.Contains('/') || fileName.Contains('\\'))
            {
                return false;
            }

            if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                return false;
            }

            return true;
        }

        private static string ResolveRootPath(string? configuredRootPath)
        {
            if (!string.IsNullOrWhiteSpace(configuredRootPath))
            {
                var expandedPath = Environment.ExpandEnvironmentVariables(configuredRootPath);
                return Path.GetFullPath(expandedPath);
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "IRIS",
                "Wallpapers");
        }

        private static string NormalizeRoutePrefix(string? configuredPrefix)
        {
            var prefix = string.IsNullOrWhiteSpace(configuredPrefix)
                ? "/api/wallpapers/"
                : configuredPrefix.Trim();

            if (!prefix.StartsWith('/'))
            {
                prefix = "/" + prefix;
            }

            if (!prefix.EndsWith('/'))
            {
                prefix += "/";
            }

            return prefix;
        }

        private static string GetContentType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".bmp" => "image/bmp",
                _ => "application/octet-stream"
            };
        }
    }
}