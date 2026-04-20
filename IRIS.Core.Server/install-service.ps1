#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Installs IRIS Core Wallpaper Server as a Windows Service.
.DESCRIPTION
    Publishes IRIS.Core.Server, configures a Windows Service that starts automatically on boot,
    ensures the firewall rule for port 5092, and starts the service.
.EXAMPLE
    .\install-service.ps1
    .\install-service.ps1 -PublishDir "C:\IRIS\CoreServer"
#>
param(
    [string]$PublishDir = "C:\IRIS\CoreServer",
    [string]$ServiceName = "IRISCoreWallpaperServer",
    [string]$DisplayName = "IRIS Core Wallpaper Server",
    [string]$Description = "IRIS centralized wallpaper upload/download server",
    [string]$FirewallRuleName = "IRIS Core Wallpaper Server TCP 5092",
    [int]$Port = 5092,
    [string]$RuntimeIdentifier = "win-x64",
    [switch]$SelfContained,
    [switch]$OverwriteConfig
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Ensure-FirewallRule {
    param(
        [Parameter(Mandatory = $true)][string]$RuleName,
        [Parameter(Mandatory = $true)][int]$LocalPort
    )

    $existing = netsh advfirewall firewall show rule name="$RuleName" 2>&1
    if (($LASTEXITCODE -eq 0) -and ($existing -notmatch "No rules match")) {
        Write-Host "Firewall rule already exists: $RuleName" -ForegroundColor DarkGray
        return
    }

    netsh advfirewall firewall add rule name="$RuleName" dir=in action=allow protocol=TCP localport=$LocalPort profile=private,domain remoteip=localsubnet | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to add firewall rule '$RuleName' for TCP $LocalPort."
    }
}

function Stop-AndRemoveServiceIfExists {
    param([string]$Name)

    $existing = Get-Service -Name $Name -ErrorAction SilentlyContinue
    if (-not $existing) {
        return
    }

    Write-Host "Stopping existing service '$Name'..." -ForegroundColor Yellow
    Stop-Service -Name $Name -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2

    Write-Host "Removing existing service '$Name'..." -ForegroundColor Yellow
    sc.exe delete $Name | Out-Null
    Start-Sleep -Seconds 2
}

function Copy-DirectoryContent {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    if (-not (Test-Path $Destination)) {
        New-Item -Path $Destination -ItemType Directory -Force | Out-Null
    }

    Copy-Item -Path (Join-Path $Source "*") -Destination $Destination -Recurse -Force
}

$projectPath = Join-Path $PSScriptRoot "IRIS.Core.Server.csproj"
if (-not (Test-Path $projectPath)) {
    throw "Project file not found: $projectPath"
}

$stagingDir = Join-Path $env:TEMP ("IRIS.Core.Server.Stage." + [Guid]::NewGuid().ToString("N"))
$configBackup = Join-Path $env:TEMP ("IRIS.Core.Server.appsettings.backup." + [Guid]::NewGuid().ToString("N") + ".json")
New-Item -Path $stagingDir -ItemType Directory -Force | Out-Null

try {
    $selfContainedValue = "false"
    if ($SelfContained.IsPresent) {
        $selfContainedValue = "true"
    }

    Write-Host "Publishing IRIS.Core.Server..." -ForegroundColor Cyan
    dotnet publish $projectPath --configuration Release --runtime $RuntimeIdentifier --output $stagingDir --self-contained $selfContainedValue
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }

    $exePath = Join-Path $stagingDir "IRIS.Core.Server.exe"
    if (-not (Test-Path $exePath)) {
        throw "Published executable not found at $exePath"
    }

    $targetConfig = Join-Path $PublishDir "appsettings.json"
    if ((-not $OverwriteConfig) -and (Test-Path $targetConfig)) {
        Copy-Item -Path $targetConfig -Destination $configBackup -Force
        Write-Host "Preserving existing appsettings.json" -ForegroundColor DarkGray
    }

    Write-Host "Deploying files to $PublishDir" -ForegroundColor Cyan
    if (Test-Path $PublishDir) {
        Get-ChildItem -Path $PublishDir -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    }
    Copy-DirectoryContent -Source $stagingDir -Destination $PublishDir

    if (Test-Path $configBackup) {
        Copy-Item -Path $configBackup -Destination (Join-Path $PublishDir "appsettings.json") -Force
    }

    $installedExe = Join-Path $PublishDir "IRIS.Core.Server.exe"
    if (-not (Test-Path $installedExe)) {
        throw "Installed executable not found at $installedExe"
    }

    Stop-AndRemoveServiceIfExists -Name $ServiceName

    Write-Host "Creating Windows Service '$ServiceName'..." -ForegroundColor Cyan
    sc.exe create $ServiceName binPath= "\"$installedExe\"" start= delayed-auto DisplayName= "\"$DisplayName\"" | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "sc.exe create failed with exit code $LASTEXITCODE"
    }

    sc.exe description $ServiceName "\"$Description\"" | Out-Null
    sc.exe failure $ServiceName reset= 86400 actions= restart/10000/restart/10000/restart/10000 | Out-Null

    Ensure-FirewallRule -RuleName $FirewallRuleName -LocalPort $Port

    Write-Host "Starting service '$ServiceName'..." -ForegroundColor Cyan
    Start-Service -Name $ServiceName

    $service = Get-Service -Name $ServiceName
    Write-Host ""
    Write-Host "IRIS Core Wallpaper Server service installed successfully." -ForegroundColor Green
    Write-Host "  Service Name : $ServiceName"
    Write-Host "  Status       : $($service.Status)"
    Write-Host "  Start Type   : $($service.StartType)"
    Write-Host "  Install Path : $PublishDir"
    Write-Host ""
    Write-Host "Post-install checklist:" -ForegroundColor Yellow
    Write-Host "  1) Verify appsettings.json has correct PublicBaseUrl and ApiToken"
    Write-Host "  2) Test health endpoint: http://<server-ip>:$Port/health"
    Write-Host "  3) Ensure UI and Agent tokens match WallpaperStorage:ApiToken"
}
finally {
    if (Test-Path $stagingDir) {
        Remove-Item -Path $stagingDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path $configBackup) {
        Remove-Item -Path $configBackup -Force -ErrorAction SilentlyContinue
    }
}
