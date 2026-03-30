#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Uninstalls the IRIS Agent Windows Service.
.EXAMPLE
    .\uninstall-service.ps1
#>
param(
    [string]$ServiceName = "IRISAgent"
)

$ErrorActionPreference = "Stop"

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if (-not $existing) {
    Write-Host "Service '$ServiceName' not found. Nothing to uninstall." -ForegroundColor Yellow
    exit 0
}

Write-Host "Stopping service '$ServiceName'..." -ForegroundColor Yellow
Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

Write-Host "Removing service '$ServiceName'..." -ForegroundColor Yellow
sc.exe delete $ServiceName

if ($LASTEXITCODE -eq 0) {
    Write-Host "Service '$ServiceName' removed successfully." -ForegroundColor Green
} else {
    Write-Error "Failed to remove service. It may be marked for deletion after reboot."
}
