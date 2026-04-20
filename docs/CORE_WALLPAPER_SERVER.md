# IRIS Core Wallpaper Server

This service centralizes wallpaper upload and download for all UI hosts and agents.

Project:
- `IRIS.Core.Server`

## Purpose

- Stores wallpapers on a single server host.
- Accepts uploads from any IRIS UI instance.
- Serves wallpapers to all agents from one reliable endpoint.

## Configuration

Edit `IRIS.Core.Server/appsettings.json`:

- `Server:Urls`: HTTP bind URL (default `http://0.0.0.0:5092`)
- `WallpaperStorage:RootPath`: central wallpaper directory
- `WallpaperStorage:RoutePrefix`: download route prefix
- `WallpaperStorage:UploadRoute`: upload endpoint route
- `WallpaperStorage:ApiToken`: shared API token (must be strong and non-default)
- `WallpaperStorage:PublicBaseUrl`: public base URL used when generating returned wallpaper URLs

Example:

```json
"WallpaperStorage": {
  "RootPath": "%PROGRAMDATA%\\IRIS\\Server\\Wallpapers",
  "RoutePrefix": "/api/wallpapers",
  "UploadRoute": "/api/wallpapers/upload",
  "ApiToken": "<strong-token>",
  "PublicBaseUrl": "http://192.168.1.20:5092"
}
```

## UI and Agent wiring

- UI upload target: `IRIS.UI/appsettings.json` -> `WallpaperService:UploadUrl`
- Agent download base: `IRIS.Agent/appsettings.json` -> `AgentSettings:WallpaperServerBaseUrl`

Use helper script:

```powershell
.\scripts\Update-IrisAppSettings.ps1 -DbHost "192.168.1.20" -WallpaperHost "192.168.1.20" -DbUser "postgres" -DbPassword "your_password"
```

## Run

```powershell
dotnet run --project IRIS.Core.Server
```

## Run as Windows Service (auto-start on boot)

On the server PC, in elevated PowerShell:

```powershell
cd IRIS.Core.Server
.\install-service.ps1
```

Optional flags:

- `-PublishDir "C:\IRIS\CoreServer"`
- `-OverwriteConfig` (replace existing deployed appsettings)
- `-SelfContained`

Useful commands:

```powershell
Get-Service IRISCoreWallpaperServer
Restart-Service IRISCoreWallpaperServer
Stop-Service IRISCoreWallpaperServer
Start-Service IRISCoreWallpaperServer
```

Uninstall:

```powershell
cd IRIS.Core.Server
.\uninstall-service.ps1
```

## Security

- Upload and download endpoints require API token via `X-IRIS-Wallpaper-Token` or `Authorization: Bearer <token>`.
- File names are sanitized and restricted to image extensions (`.jpg`, `.jpeg`, `.png`, `.bmp`).
- Upload size limit is controlled by `WallpaperStorage:MaxUploadBytes`.
