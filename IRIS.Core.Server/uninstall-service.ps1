#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Uninstalls the IRIS Core Wallpaper Server Windows Service.
.EXAMPLE
    .\uninstall-service.ps1
#>
param(
    [string]$ServiceName = "IRISCoreWallpaperServer",
    [string]$FirewallRuleName = "IRIS Core Wallpaper Server TCP 5092",
    [switch]$KeepFirewallRule,
    [switch]$RemoveInstalledFiles,
    [string]$PublishDir = "C:\IRIS\CoreServer"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Stopping service '$ServiceName'..." -ForegroundColor Yellow
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2

    Write-Host "Removing service '$ServiceName'..." -ForegroundColor Yellow
    sc.exe delete $ServiceName | Out-Null

    if ($LASTEXITCODE -eq 0) {
        Write-Host "Service removed successfully." -ForegroundColor Green
    }
    else {
        Write-Warning "Service deletion returned a non-zero code. It may be marked for deletion until reboot."
    }
}
else {
    Write-Host "Service '$ServiceName' was not found." -ForegroundColor Yellow
}

if (-not $KeepFirewallRule) {
    netsh advfirewall firewall delete rule name="$FirewallRuleName" | Out-Null
    Write-Host "Firewall rule removed: $FirewallRuleName" -ForegroundColor DarkGray
}

if ($RemoveInstalledFiles -and (Test-Path $PublishDir)) {
    Remove-Item -Path $PublishDir -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "Removed installed files: $PublishDir" -ForegroundColor DarkGray
}
