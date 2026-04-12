#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Builds a redistributable IRIS.UI installer package.
.DESCRIPTION
    Publishes IRIS.UI and creates a package folder containing:
      - install-ui.ps1
      - uninstall-ui.ps1
      - payload\ (published app files)
    The package can be copied to target PCs and installed without source code.
.EXAMPLE
    .\build-ui-installer-package.ps1
    .\build-ui-installer-package.ps1 -OutputDir "C:\Builds\IRIS.UI.Installer"
#>
param(
    [string]$OutputDir = "",
    [string]$RuntimeIdentifier = "win-x64",
    [switch]$SelfContained,
    [switch]$CreateZip
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Write-Step {
    param([string]$Message)
    Write-Host "[IRIS.UI.PACK] $Message" -ForegroundColor Cyan
}

$projectPath = Join-Path $PSScriptRoot "IRIS.UI.csproj"
$installScript = Join-Path $PSScriptRoot "install-ui.ps1"
$uninstallScript = Join-Path $PSScriptRoot "uninstall-ui.ps1"

foreach ($required in @($projectPath, $installScript, $uninstallScript)) {
    if (-not (Test-Path $required)) {
        throw "Required file not found: $required"
    }
}

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    throw "dotnet CLI not found. Install .NET 9 SDK before building installer package."
}

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $PSScriptRoot "dist\IRIS.UI.Installer"
}

$stageDir = Join-Path $env:TEMP ("IRIS.UI.Package." + [Guid]::NewGuid().ToString("N"))
$payloadDir = Join-Path $OutputDir "payload"

try {
    New-Item -Path $stageDir -ItemType Directory -Force | Out-Null
    New-Item -Path $OutputDir -ItemType Directory -Force | Out-Null

    $selfContainedValue = "false"
    if ($SelfContained.IsPresent) {
        $selfContainedValue = "true"
    }

    Write-Step "Publishing IRIS.UI"
    & dotnet publish $projectPath --configuration Release --runtime $RuntimeIdentifier --output $stageDir --self-contained $selfContainedValue
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }

    if (Test-Path $payloadDir) {
        Remove-Item -Path $payloadDir -Recurse -Force
    }

    Write-Step "Copying payload + scripts"
    New-Item -Path $payloadDir -ItemType Directory -Force | Out-Null
    Copy-Item -Path (Join-Path $stageDir "*") -Destination $payloadDir -Recurse -Force
    Copy-Item -Path $installScript -Destination (Join-Path $OutputDir "install-ui.ps1") -Force
    Copy-Item -Path $uninstallScript -Destination (Join-Path $OutputDir "uninstall-ui.ps1") -Force

    $readmePath = Join-Path $OutputDir "README.txt"
    @"
IRIS UI Installer Package

Install on target PC (run elevated PowerShell):
  .\install-ui.ps1

Uninstall:
  .\uninstall-ui.ps1

Notes:
- install-ui.ps1 auto-detects the local .\payload folder.
- Update installed appsettings.json after install for database connection.
"@ | Set-Content -Path $readmePath -Encoding UTF8

    if ($CreateZip) {
        $zipPath = "$OutputDir.zip"
        if (Test-Path $zipPath) {
            Remove-Item -Path $zipPath -Force
        }

        Write-Step "Creating ZIP package"
        Compress-Archive -Path (Join-Path $OutputDir "*") -DestinationPath $zipPath -Force
        Write-Host "ZIP created: $zipPath" -ForegroundColor Green
    }

    Write-Host ""
    Write-Host "Installer package ready." -ForegroundColor Green
    Write-Host "  Folder: $OutputDir"
    Write-Host "  Payload: $payloadDir"
}
finally {
    if (Test-Path $stageDir) {
        Remove-Item -Path $stageDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
