#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Installs IRIS.UI on a Windows host PC.
.DESCRIPTION
    Publishes IRIS.UI in Release mode, deploys it to Program Files (or a custom path),
    creates Start Menu/Desktop shortcuts, and ensures required firewall rules.
.EXAMPLE
    .\install-ui.ps1
    .\install-ui.ps1 -InstallDir "C:\IRIS\UI"
#>
param(
    [string]$InstallDir = "C:\Program Files\IRIS\UI",
    [string]$StartMenuFolder = "IRIS",
    [string]$ShortcutName = "IRIS UI",
    [string]$PayloadDir = "",
    [string]$RuntimeIdentifier = "win-x64",
    [switch]$SelfContained,
    [switch]$OverwriteConfig,
    [switch]$NoDesktopShortcut,
    [string]$FirewallRulePower = "IRIS UI Power Command TCP 5091",
    [string]$FirewallRuleSnapshotOut = "IRIS UI Snapshot Outbound TCP 5057",
    [string]$FirewallRuleFileApiOut = "IRIS UI File API Outbound TCP 5065"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Write-Step {
    param([string]$Message)
    Write-Host "[IRIS.UI] $Message" -ForegroundColor Cyan
}

function Ensure-DotNet {
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnet) {
        throw "dotnet CLI not found. Install .NET 9 SDK/runtime before running this installer."
    }
}

function Ensure-FirewallRule {
    param(
        [Parameter(Mandatory = $true)][string]$RuleName,
        [Parameter(Mandatory = $true)][int]$Port
    )

    $existing = netsh advfirewall firewall show rule name="$RuleName" 2>&1
    if (($LASTEXITCODE -eq 0) -and ($existing -notmatch "No rules match")) {
        Write-Host "  Firewall rule already exists: $RuleName" -ForegroundColor DarkGray
        return
    }

    netsh advfirewall firewall add rule name="$RuleName" dir=in action=allow protocol=TCP localport=$Port profile=private,domain remoteip=localsubnet | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to add firewall rule '$RuleName' on TCP $Port."
    }
}

function Ensure-OutboundFirewallRule {
    param(
        [Parameter(Mandatory = $true)][string]$RuleName,
        [Parameter(Mandatory = $true)][int]$Port
    )

    $existing = netsh advfirewall firewall show rule name="$RuleName" 2>&1
    if (($LASTEXITCODE -eq 0) -and ($existing -notmatch "No rules match")) {
        Write-Host "  Outbound firewall rule already exists: $RuleName" -ForegroundColor DarkGray
        return
    }

    netsh advfirewall firewall add rule name="$RuleName" dir=out action=allow protocol=TCP remoteport=$Port profile=any | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to add outbound firewall rule '$RuleName' on TCP $Port."
    }
}

function New-Shortcut {
    param(
        [Parameter(Mandatory = $true)][string]$ShortcutPath,
        [Parameter(Mandatory = $true)][string]$TargetPath,
        [string]$WorkingDirectory,
        [string]$Description = "IRIS Dashboard"
    )

    $parent = Split-Path -Parent $ShortcutPath
    if (-not (Test-Path $parent)) {
        New-Item -Path $parent -ItemType Directory -Force | Out-Null
    }

    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($ShortcutPath)
    $shortcut.TargetPath = $TargetPath
    if ($WorkingDirectory) {
        $shortcut.WorkingDirectory = $WorkingDirectory
    }
    $shortcut.Description = $Description
    $shortcut.IconLocation = $TargetPath
    $shortcut.Save()
}

function Copy-DirectoryContent {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    if (-not (Test-Path $Destination)) {
        New-Item -Path $Destination -ItemType Directory -Force | Out-Null
    }

    $sourceNormalized = Join-Path $Source "*"
    Copy-Item -Path $sourceNormalized -Destination $Destination -Recurse -Force
}

$projectPath = Join-Path $PSScriptRoot "IRIS.UI.csproj"
$packagedPayloadDir = Join-Path $PSScriptRoot "payload"
$usePayload = $false

if ([string]::IsNullOrWhiteSpace($PayloadDir)) {
    if (Test-Path $packagedPayloadDir) {
        $PayloadDir = $packagedPayloadDir
        $usePayload = $true
    }
}
else {
    $usePayload = $true
}

$stagingDir = Join-Path $env:TEMP ("IRIS.UI.Stage." + [Guid]::NewGuid().ToString("N"))
New-Item -Path $stagingDir -ItemType Directory -Force | Out-Null

try {
    if ($usePayload) {
        Write-Step "Using prebuilt payload from $PayloadDir"
        if (-not (Test-Path $PayloadDir)) {
            throw "Payload directory not found: $PayloadDir"
        }

        Copy-DirectoryContent -Source $PayloadDir -Destination $stagingDir
    }
    else {
        if (-not (Test-Path $projectPath)) {
            throw "IRIS.UI.csproj not found at $projectPath"
        }

        Ensure-DotNet

        Write-Step "Publishing IRIS.UI from source..."
        $selfContainedValue = "false"
        if ($SelfContained.IsPresent) {
            $selfContainedValue = "true"
        }

        $publishArgs = @(
            "publish", $projectPath,
            "--configuration", "Release",
            "--runtime", $RuntimeIdentifier,
            "--output", $stagingDir,
            "--self-contained", $selfContainedValue
        )

        & dotnet @publishArgs
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet publish failed with exit code $LASTEXITCODE"
        }
    }

    $exePath = Join-Path $stagingDir "IRIS.UI.exe"
    if (-not (Test-Path $exePath)) {
        throw "Published executable not found: $exePath"
    }

    $previousConfig = Join-Path $InstallDir "appsettings.json"
    $configBackup = Join-Path $env:TEMP ("IRIS.UI.appsettings.backup." + [Guid]::NewGuid().ToString("N") + ".json")

    if ((-not $OverwriteConfig) -and (Test-Path $previousConfig)) {
        Write-Step "Preserving existing appsettings.json"
        Copy-Item -Path $previousConfig -Destination $configBackup -Force
    }

    Write-Step "Deploying files to $InstallDir"
    if (-not (Test-Path $InstallDir)) {
        New-Item -Path $InstallDir -ItemType Directory -Force | Out-Null
    }

    Get-ChildItem -Path $InstallDir -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    Copy-DirectoryContent -Source $stagingDir -Destination $InstallDir

    if (Test-Path $configBackup) {
        Copy-Item -Path $configBackup -Destination (Join-Path $InstallDir "appsettings.json") -Force
    }

    $installedExe = Join-Path $InstallDir "IRIS.UI.exe"
    if (-not (Test-Path $installedExe)) {
        throw "Installed executable missing at $installedExe"
    }

    Write-Step "Ensuring firewall prerequisites"
    Ensure-FirewallRule -RuleName $FirewallRulePower -Port 5091
    Ensure-OutboundFirewallRule -RuleName $FirewallRuleSnapshotOut -Port 5057
    Ensure-OutboundFirewallRule -RuleName $FirewallRuleFileApiOut -Port 5065

    Write-Step "Creating shortcuts"
    $startMenuPath = Join-Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs" $StartMenuFolder
    $startMenuShortcut = Join-Path $startMenuPath ("$ShortcutName.lnk")
    New-Shortcut -ShortcutPath $startMenuShortcut -TargetPath $installedExe -WorkingDirectory $InstallDir -Description "IRIS Management Dashboard"

    if (-not $NoDesktopShortcut) {
        $desktopShortcut = Join-Path "$env:Public\Desktop" ("$ShortcutName.lnk")
        New-Shortcut -ShortcutPath $desktopShortcut -TargetPath $installedExe -WorkingDirectory $InstallDir -Description "IRIS Management Dashboard"
    }

    Write-Host "" 
    Write-Host "IRIS.UI installed successfully." -ForegroundColor Green
    Write-Host "  Install Path : $InstallDir"
    Write-Host "  Launch EXE   : $installedExe"
    Write-Host "  Start Menu   : $startMenuShortcut"
    if (-not $NoDesktopShortcut) {
        Write-Host "  Desktop Link : $env:Public\Desktop\$ShortcutName.lnk"
    }
    Write-Host ""
    Write-Host "Post-install checklist:" -ForegroundColor Yellow
    Write-Host "  1) Edit appsettings.json connection string for your IRIS database host"
    Write-Host "  2) Verify command and outbound agent ports are reachable from agent PCs"
    Write-Host "  3) Configure WallpaperService:UploadUrl to your central server endpoint"
    Write-Host "  4) Launch IRIS UI and sign in"
}
finally {
    if (Test-Path $stagingDir) {
        Remove-Item -Path $stagingDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
