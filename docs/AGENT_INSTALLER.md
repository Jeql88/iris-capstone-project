# IRIS Agent Installer Guide

This guide documents the MSI-based installer pipeline for deploying `IRIS.Agent` to target Windows PCs without requiring source code or dotnet SDK on those PCs.

## Files

- `IRIS.Agent.Installer/IRIS.Agent.Installer.wixproj`
- `IRIS.Agent.Installer/Product.wxs`
- `IRIS.Agent.Installer/build-agent-msi.ps1`
- `IRIS.Agent.Installer/scripts/RegisterAgentStartupTask.ps1`

## MSI Installer Scope

The Agent MSI currently:
- Installs agent files to `C:\Program Files\IRIS\Agent`
- Registers uninstall entry in Add/Remove Programs
- Adds fallback startup entry at `HKLM\Software\Microsoft\Windows\CurrentVersion\Run\IRISAgentFallback`
- Removes old `IRISAgent` task/service remnants
- Reserves URL ACLs for agent HTTP listeners (`http://+:5057/` and `http://+:5065/`)
- Creates scheduled task `IRISAgent` as ONLOGON + HIGHEST (script-based registration)
- Starts scheduled task immediately
- Runs custom bootstrap by default
- Fails installation if scheduled task creation fails (fail-fast)

## What To Transfer To A New PC

For installation only (recommended):

- Transfer only the built MSI from `dist\IRIS.Agent.MSI\`.

For rebuilding MSI on another machine:

- You need the full repository source plus `dotnet` SDK and NuGet restore access.
- Copying only `IRIS.Agent.Installer` is not enough to rebuild, because publish step depends on `IRIS.Agent` and `IRIS.Core` projects.

## Build MSI

Run from repository root or installer folder:

```powershell
cd IRIS.Agent.Installer
.\build-agent-msi.ps1 -ProductVersion "1.0.0"
```

Default output:

- `dist\IRIS.Agent.MSI\IRIS.Agent.<version>.msi`

## Install on Target PC

Interactive install:

```powershell
msiexec /i "IRIS.Agent.1.0.0.msi"
```

Important:

- Run installation from an elevated (Run as Administrator) terminal.
- The MSI is per-machine and will fail if not elevated.

Install with bootstrap disabled (if endpoint policy blocks custom actions):

```powershell
msiexec /i "IRIS.Agent.1.0.0.msi" ENABLE_CUSTOM_BOOTSTRAP=0
```

Silent install:

```powershell
msiexec /i "IRIS.Agent.1.0.0.msi" /qn
```

## Notes

- Build script defaults to self-contained publish, reducing runtime dependencies on target PCs.
- Installer is x64-oriented (`win-x64`).
- URL ACL reservations are installed by MSI bootstrap so snapshot and file APIs can bind without running the agent as Administrator at runtime.
- Fallback startup via HKLM Run is always installed and works even when custom actions are disabled.
- Use `scripts/Update-IrisAppSettings.ps1` to apply DB/UI host values to deployment appsettings files before rollout.
- Existing script installers still exist for task/service workflows:
  - `IRIS.Agent/install-agent.ps1`
  - `IRIS.Agent/install-service.ps1`
