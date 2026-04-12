<#
.SYNOPSIS
    Builds an MSI installer for IRIS.UI with Add/Remove Programs integration.
.DESCRIPTION
    Publishes IRIS.UI to a payload directory and builds IRIS.UI.Installer.wixproj.
    By default, it publishes self-contained to minimize runtime dependencies.
.EXAMPLE
    .\build-ui-msi.ps1
    .\build-ui-msi.ps1 -ProductVersion "1.0.1" -OutputDir "C:\Builds\IRIS.UI.MSI"
#>
param(
    [string]$ProductVersion = "1.0.0",
    [string]$RuntimeIdentifier = "win-x64",
    [bool]$SelfContained = $true,
    [string]$Configuration = "Release",
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Write-Step {
    param([string]$Message)
    Write-Host "[IRIS.UI.MSI] $Message" -ForegroundColor Cyan
}

function Assert-Tool {
    param([string]$Name)
    $tool = Get-Command $Name -ErrorAction SilentlyContinue
    if (-not $tool) {
        throw "Required tool '$Name' not found in PATH."
    }
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$uiProject = Join-Path $repoRoot "IRIS.UI\IRIS.UI.csproj"
$installerProject = Join-Path $PSScriptRoot "IRIS.UI.Installer.wixproj"

if (-not (Test-Path $uiProject)) {
    throw "UI project not found: $uiProject"
}

if (-not (Test-Path $installerProject)) {
    throw "Installer project not found: $installerProject"
}

Assert-Tool -Name "dotnet"

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $repoRoot "dist\IRIS.UI.MSI"
}

$payloadDir = Join-Path $env:TEMP ("IRIS.UI.MSI.Payload." + [Guid]::NewGuid().ToString("N"))

try {
    New-Item -Path $payloadDir -ItemType Directory -Force | Out-Null
    New-Item -Path $OutputDir -ItemType Directory -Force | Out-Null

    $selfContainedValue = "false"
    if ($SelfContained) {
        $selfContainedValue = "true"
    }

    Write-Step "Publishing IRIS.UI payload"
    & dotnet publish $uiProject --configuration $Configuration --runtime $RuntimeIdentifier --self-contained $selfContainedValue --output $payloadDir
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }

    if (-not (Test-Path (Join-Path $payloadDir "IRIS.UI.exe"))) {
        throw "Published payload does not contain IRIS.UI.exe"
    }

    Write-Step "Building MSI"
    & dotnet build $installerProject --configuration $Configuration "-p:PayloadDir=$payloadDir" "-p:ProductVersion=$ProductVersion"
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build (WiX MSI) failed with exit code $LASTEXITCODE"
    }

    $msiSearchRoot = Join-Path $PSScriptRoot "bin"
    $msiCandidates = @(Get-ChildItem -Path $msiSearchRoot -Filter *.msi -Recurse -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTimeUtc -Descending
    )

    if ($msiCandidates.Count -eq 0) {
        throw "MSI output not found under $msiSearchRoot"
    }

    $msiPath = $msiCandidates[0].FullName
    $targetMsi = Join-Path $OutputDir ("IRIS.UI." + $ProductVersion + ".msi")
    Copy-Item -Path $msiPath -Destination $targetMsi -Force

    Write-Host ""
    Write-Host "MSI build successful." -ForegroundColor Green
    Write-Host "  MSI: $targetMsi"
    Write-Host ""
    Write-Host "Install command on target PC (elevated):"
    Write-Host "  msiexec /i `"$targetMsi`""
    Write-Host ""
    Write-Host "Silent install command:"
    Write-Host "  msiexec /i `"$targetMsi`" /qn"
}
finally {
    if (Test-Path $payloadDir) {
        Remove-Item -Path $payloadDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
