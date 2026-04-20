using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

var urls = builder.Configuration["Server:Urls"]?.Trim();
if (!string.IsNullOrWhiteSpace(urls))
{
    builder.WebHost.UseUrls(urls);
}

var app = builder.Build();
var logger = app.Logger;
var settings = WallpaperStorageSettings.FromConfiguration(app.Configuration);

if (string.IsNullOrWhiteSpace(settings.ApiToken) || string.Equals(settings.ApiToken, "CHANGE-ME-TO-A-STRONG-TOKEN", StringComparison.Ordinal))
{
    throw new InvalidOperationException("WallpaperStorage:ApiToken must be configured to a strong, non-default value.");
}

Directory.CreateDirectory(settings.RootPath);
logger.LogInformation("Wallpaper server started. RootPath={RootPath}, RoutePrefix={RoutePrefix}, UploadRoute={UploadRoute}",
    settings.RootPath,
    settings.RoutePrefix,
    settings.UploadRoute);

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost(settings.UploadRoute, async (HttpRequest request, CancellationToken cancellationToken) =>
{
    if (!IsAuthorized(request, settings.ApiToken))
    {
        return Results.Unauthorized();
    }

    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { message = "Expected multipart/form-data payload." });
    }

    var form = await request.ReadFormAsync(cancellationToken);
    var upload = form.Files["file"] ?? form.Files.FirstOrDefault();
    if (upload == null || upload.Length == 0)
    {
        return Results.BadRequest(new { message = "No wallpaper file was uploaded." });
    }

    if (upload.Length > settings.MaxUploadBytes)
    {
        return Results.BadRequest(new { message = $"Uploaded file exceeds max size of {settings.MaxUploadBytes} bytes." });
    }

    var extension = Path.GetExtension(upload.FileName).ToLowerInvariant();
    if (!WallpaperStorageSettings.AllowedExtensions.Contains(extension))
    {
        return Results.BadRequest(new { message = "Unsupported file type. Allowed: .jpg, .jpeg, .png, .bmp" });
    }

    var storedFileName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}{extension}";
    var destinationPath = Path.Combine(settings.RootPath, storedFileName);

    await using (var destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
    {
        await upload.CopyToAsync(destination, cancellationToken);
    }

    var hash = await CalculateFileHashAsync(destinationPath, cancellationToken);
    var wallpaperUrl = BuildWallpaperUrl(settings.PublicBaseUrl, settings.RoutePrefix, storedFileName);

    return Results.Ok(new WallpaperUploadResponse(storedFileName, wallpaperUrl, hash));
});

app.MapMethods($"{settings.RoutePrefix}/{{fileName}}", new[] { HttpMethods.Get, HttpMethods.Head },
    async (HttpRequest request, HttpResponse response, string fileName, CancellationToken cancellationToken) =>
{
    if (!IsAuthorized(request, settings.ApiToken))
    {
        return Results.Unauthorized();
    }

    if (!IsSafeFileName(fileName))
    {
        return Results.BadRequest(new { message = "Invalid wallpaper file name." });
    }

    var fullPath = Path.Combine(settings.RootPath, fileName);
    if (!File.Exists(fullPath))
    {
        return Results.NotFound();
    }

    response.Headers["Cache-Control"] = "private, max-age=300";

    if (HttpMethods.IsHead(request.Method))
    {
        var fileInfo = new FileInfo(fullPath);
        response.ContentType = GetContentType(fullPath);
        response.ContentLength = fileInfo.Length;
        return Results.Empty;
    }

    await using var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    response.ContentType = GetContentType(fullPath);
    response.ContentLength = fileStream.Length;
    await fileStream.CopyToAsync(response.Body, cancellationToken);
    return Results.Empty;
});

app.Run();

static bool IsAuthorized(HttpRequest request, string expectedToken)
{
    var headerToken = request.Headers["X-IRIS-Wallpaper-Token"].ToString().Trim();
    if (SecureEquals(headerToken, expectedToken))
    {
        return true;
    }

    var authorization = request.Headers["Authorization"].ToString();
    if (!string.IsNullOrWhiteSpace(authorization) && authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        var bearerToken = authorization["Bearer ".Length..].Trim();
        return SecureEquals(bearerToken, expectedToken);
    }

    return false;
}

static bool SecureEquals(string value, string expected)
{
    if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(expected))
    {
        return false;
    }

    var left = Encoding.UTF8.GetBytes(value);
    var right = Encoding.UTF8.GetBytes(expected);
    return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
}

static bool IsSafeFileName(string fileName)
{
    if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains('/') || fileName.Contains('\\'))
    {
        return false;
    }

    if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
    {
        return false;
    }

    var extension = Path.GetExtension(fileName).ToLowerInvariant();
    return WallpaperStorageSettings.AllowedExtensions.Contains(extension);
}

static string GetContentType(string filePath)
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

static async Task<string> CalculateFileHashAsync(string filePath, CancellationToken cancellationToken)
{
    await using var stream = File.OpenRead(filePath);
    var hash = await SHA256.HashDataAsync(stream, cancellationToken);
    return Convert.ToHexString(hash);
}

static string BuildWallpaperUrl(string baseUrl, string routePrefix, string fileName)
{
    var normalizedBaseUrl = baseUrl.Trim().TrimEnd('/');
    var normalizedPrefix = routePrefix.Trim();
    if (!normalizedPrefix.StartsWith('/'))
    {
        normalizedPrefix = "/" + normalizedPrefix;
    }

    var encodedFileName = Uri.EscapeDataString(fileName);
    return $"{normalizedBaseUrl}{normalizedPrefix}/{encodedFileName}";
}

internal sealed class WallpaperStorageSettings
{
    public static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".bmp"
    };

    public required string RootPath { get; init; }
    public required string RoutePrefix { get; init; }
    public required string UploadRoute { get; init; }
    public required string ApiToken { get; init; }
    public required string PublicBaseUrl { get; init; }
    public required long MaxUploadBytes { get; init; }

    public static WallpaperStorageSettings FromConfiguration(IConfiguration configuration)
    {
        var rootPath = configuration["WallpaperStorage:RootPath"]?.Trim();
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            rootPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "IRIS", "Server", "Wallpapers");
        }

        rootPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(rootPath));

        var routePrefix = NormalizeRoute(configuration["WallpaperStorage:RoutePrefix"], "/api/wallpapers");
        var uploadRoute = NormalizeRoute(configuration["WallpaperStorage:UploadRoute"], "/api/wallpapers/upload");

        var apiToken = configuration["WallpaperStorage:ApiToken"]?.Trim() ?? string.Empty;

        var publicBaseUrl = configuration["WallpaperStorage:PublicBaseUrl"]?.Trim();
        if (!Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var publicUri)
            || (publicUri.Scheme != Uri.UriSchemeHttp && publicUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("WallpaperStorage:PublicBaseUrl must be a valid absolute http(s) URL.");
        }

        var maxUploadBytes = long.TryParse(configuration["WallpaperStorage:MaxUploadBytes"], out var configuredMaxBytes)
            ? configuredMaxBytes
            : 10 * 1024 * 1024;

        if (maxUploadBytes <= 0)
        {
            maxUploadBytes = 10 * 1024 * 1024;
        }

        return new WallpaperStorageSettings
        {
            RootPath = rootPath,
            RoutePrefix = routePrefix,
            UploadRoute = uploadRoute,
            ApiToken = apiToken,
            PublicBaseUrl = publicUri.ToString().TrimEnd('/'),
            MaxUploadBytes = maxUploadBytes
        };
    }

    private static string NormalizeRoute(string? configured, string fallback)
    {
        var route = string.IsNullOrWhiteSpace(configured) ? fallback : configured.Trim();
        if (!route.StartsWith('/'))
        {
            route = "/" + route;
        }

        return route.TrimEnd('/');
    }
}

internal sealed record WallpaperUploadResponse(string FileName, string Url, string Sha256);
