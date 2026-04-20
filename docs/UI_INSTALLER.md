# IRIS UI Installer Guide

This guide documents the script-based installer for deploying `IRIS.UI` to selected Windows PCs.

## Files

- `IRIS.UI/install-ui.ps1`
- `IRIS.UI/uninstall-ui.ps1`
- `IRIS.UI/build-ui-installer-package.ps1`
- `IRIS.UI.Installer/IRIS.UI.Installer.wixproj`
- `IRIS.UI.Installer/Product.wxs`
- `IRIS.UI.Installer/build-ui-msi.ps1`

## MSI Installer (Add/Remove Programs)

`IRIS.UI` now has a WiX-based MSI project for proper Programs and Features integration.

Current MSI scope:
- Installs UI files to Program Files
- Creates Start Menu/Desktop shortcuts
- Registers uninstall entry in Add/Remove Programs
- Ensures firewall rules for TCP `5091` and `5092`
- Ensures URL ACL for `http://+:5092/`
- Runs custom bootstrap by default

Build MSI (elevated PowerShell):

```powershell
cd IRIS.UI.Installer
.\build-ui-msi.ps1 -ProductVersion "1.0.0"
```

Output MSI default location:

- `dist\IRIS.UI.MSI\IRIS.UI.<version>.msi`

## What To Transfer To A New PC

For installation only (recommended):

- Transfer only the built MSI from `dist\IRIS.UI.MSI\`.

For rebuilding MSI on another machine:

- You need the full repository source plus `dotnet` SDK and NuGet restore access.
- Copying only `IRIS.UI.Installer` is not enough to rebuild, because publish step depends on `IRIS.UI` and `IRIS.Core` projects.

Install on target PC:

```powershell
msiexec /i "IRIS.UI.1.0.0.msi"
```

Install with bootstrap disabled (if endpoint policy blocks custom actions):

```powershell
msiexec /i "IRIS.UI.1.0.0.msi" ENABLE_CUSTOM_BOOTSTRAP=0
```

Silent install:

```powershell
msiexec /i "IRIS.UI.1.0.0.msi" /qn
```

## Appsettings Update (UI + Agent + Core)

Use deployment helper script from repo root:

```powershell
.\scripts\Update-IrisAppSettings.ps1 -DbHost "192.168.1.20" -UiHost "192.168.1.10" -DbUser "postgres" -DbPassword "your_password"
```

What it updates:

- `IRIS.UI\appsettings.json` -> `ConnectionStrings:IRISDatabase`
- `IRIS.Agent\appsettings.json` -> `ConnectionStrings:IRISDatabase`, `AgentSettings:CommandServerHost`, `AgentSettings:CommandServerPort`, `AgentSettings:WallpaperServerBaseUrl`
- `IRIS.Core\appsettings.json` -> `ConnectionStrings:IRISDatabase`

Why Core appsettings is included:

- Core is not deployed as a standalone runtime app.
- Core appsettings is mainly useful for EF tooling/migration workflows, especially when commands are run from Core context.

## What the installer does

`install-ui.ps1`:
- Publishes `IRIS.UI` in Release mode
- Deploys files to `C:\Program Files\IRIS\UI` (default)
- Preserves existing `appsettings.json` unless `-OverwriteConfig` is used
- Ensures firewall rules for TCP `5091` and `5092`
- Ensures URL ACL for `http://+:5092/` (required by wallpaper HTTP server)
- Creates Start Menu shortcut for all users
- Optionally creates a Public Desktop shortcut

`uninstall-ui.ps1`:
- Stops running `IRIS.UI` process
- Removes install directory and shortcuts
- Removes firewall rules and URL ACL by default (or keeps them with `-KeepNetworkRules`)

## Install Command

Run in elevated PowerShell from project root:

```powershell
cd IRIS.UI
.\install-ui.ps1
```

## Recommended Deployment Workflow (for target PCs)

1. Build installer package on a development/build machine:

```powershell
cd IRIS.UI
.\build-ui-installer-package.ps1 -CreateZip
```

2. Copy the generated package folder (or ZIP) to target PC.

3. On target PC (elevated PowerShell), run:

```powershell
.\install-ui.ps1
```

When `payload\` exists beside `install-ui.ps1`, the installer uses that prebuilt payload and does not require source code or `dotnet publish` on the target machine.

## Install Command (custom path)

```powershell
cd IRIS.UI
.\install-ui.ps1 -InstallDir "C:\IRIS\UI"
```

## Optional Flags

- `-SelfContained` : Publish self-contained app (larger output, no runtime prerequisite)
- `-OverwriteConfig` : Replace existing `appsettings.json` during install
- `-NoDesktopShortcut` : Skip Public Desktop shortcut

## Uninstall Command

```powershell
cd IRIS.UI
.\uninstall-ui.ps1
```

Keep network rules during uninstall:

```powershell
cd IRIS.UI
.\uninstall-ui.ps1 -KeepNetworkRules
```

## Post-Install Checklist

1. Set `ConnectionStrings:IRISDatabase` in installed `appsettings.json`.
2. Confirm host firewall allows inbound TCP `5091` and `5092`.
3. Ensure agent PCs can reach UI host over LAN.
4. Launch IRIS UI and verify login.

## Notes

- Installer requires Administrator rights (`#Requires -RunAsAdministrator`).
- Default publish mode is framework-dependent (`--self-contained false`), so target PC needs .NET 9 Desktop Runtime unless you install with `-SelfContained`.
- If you want zero runtime prerequisites on target PCs, build package with `-SelfContained`.
- MSI build script defaults to self-contained publish (`SelfContained = true`) to reduce runtime dependencies on target PCs.
- MSI package produced by `build-ui-msi.ps1` is x64-oriented (`win-x64`).
- UI MSI network bootstrap uses bundled script: `IRIS.UI.Installer/scripts/ConfigureUiHostNetwork.ps1`.
