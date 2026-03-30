#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Installs the IRIS Agent as a Windows Service.
.DESCRIPTION
    Publishes the agent, creates a Windows Service named "IRISAgent",
    and starts it. The service runs as Local System and starts automatically on boot.
.EXAMPLE
    .\install-service.ps1
    .\install-service.ps1 -PublishDir "C:\IRIS\Agent"
#>
param(
    [string]$PublishDir = "C:\IRIS\Agent",
    [string]$ServiceName = "IRISAgent",
    [string]$DisplayName = "IRIS Agent",
    [string]$Description = "IRIS Integrated Remote Infrastructure System - Lab PC monitoring agent"
)

$ErrorActionPreference = "Stop"

# Stop existing service if running
$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Stopping existing service '$ServiceName'..." -ForegroundColor Yellow
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Write-Host "Removing existing service..." -ForegroundColor Yellow
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

# Publish the agent
$projectDir = $PSScriptRoot
Write-Host "Publishing IRIS.Agent to '$PublishDir'..." -ForegroundColor Cyan
dotnet publish "$projectDir\IRIS.Agent.csproj" `
    --configuration Release `
    --runtime win-x64 `
    --output $PublishDir `
    --self-contained false

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed with exit code $LASTEXITCODE"
    exit 1
}

$exePath = Join-Path $PublishDir "IRIS.Agent.exe"
if (-not (Test-Path $exePath)) {
    Write-Error "Published executable not found at '$exePath'"
    exit 1
}

# Create the Windows Service
Write-Host "Creating Windows Service '$ServiceName'..." -ForegroundColor Cyan
sc.exe create $ServiceName `
    binPath= "`"$exePath`"" `
    start= auto `
    DisplayName= "`"$DisplayName`""

if ($LASTEXITCODE -ne 0) {
    Write-Error "sc.exe create failed with exit code $LASTEXITCODE"
    exit 1
}

# Set description
sc.exe description $ServiceName "`"$Description`""

# Configure recovery: restart on first, second, and subsequent failures (delay 10s)
sc.exe failure $ServiceName reset= 86400 actions= restart/10000/restart/10000/restart/10000

# Start the service
Write-Host "Starting service '$ServiceName'..." -ForegroundColor Cyan
Start-Service -Name $ServiceName

$svc = Get-Service -Name $ServiceName
Write-Host ""
Write-Host "Service '$ServiceName' installed and running." -ForegroundColor Green
Write-Host "  Status:    $($svc.Status)"
Write-Host "  StartType: $($svc.StartType)"
Write-Host "  Path:      $exePath"
Write-Host ""
Write-Host "Manage with:"
Write-Host "  Stop:      Stop-Service $ServiceName"
Write-Host "  Start:     Start-Service $ServiceName"
Write-Host "  Status:    Get-Service $ServiceName"
Write-Host "  Logs:      Get-WinEvent -LogName Application -FilterXPath '*[System[Provider[@Name=""$ServiceName""]]]' -MaxEvents 20"
Write-Host "  Uninstall: .\uninstall-service.ps1"
