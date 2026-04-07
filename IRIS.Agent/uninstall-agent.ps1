#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Uninstalls the IRIS Agent scheduled task and optionally cleans up.
.EXAMPLE
    .\uninstall-agent.ps1
#>
param(
    [string]$TaskName = "IRISAgent"
)

$ErrorActionPreference = "Stop"

# Stop any running agent processes
$agentProcesses = Get-Process -Name "IRIS.Agent" -ErrorAction SilentlyContinue
if ($agentProcesses) {
    Write-Host "Stopping running IRIS Agent processes..." -ForegroundColor Yellow
    $agentProcesses | Stop-Process -Force
    Start-Sleep -Seconds 2
}

# Remove scheduled task
$taskExists = schtasks /Query /TN $TaskName 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "Removing scheduled task '$TaskName'..." -ForegroundColor Yellow
    schtasks /Delete /TN $TaskName /F
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Scheduled task '$TaskName' removed successfully." -ForegroundColor Green
    } else {
        Write-Error "Failed to remove scheduled task."
    }
} else {
    Write-Host "Scheduled task '$TaskName' not found." -ForegroundColor Yellow
}

# Also remove legacy Windows Service if it exists
$existing = Get-Service -Name $TaskName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Found legacy Windows Service '$TaskName'. Removing..." -ForegroundColor Yellow
    Stop-Service -Name $TaskName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    sc.exe delete $TaskName | Out-Null
    Write-Host "Legacy service removed." -ForegroundColor Green
}

Write-Host ""
Write-Host "IRIS Agent uninstalled." -ForegroundColor Green
Write-Host "Published files at C:\IRIS\Agent were NOT removed. Delete manually if desired."
