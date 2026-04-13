#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Installs the IRIS Agent as a Scheduled Task that runs on user logon.
.DESCRIPTION
    Publishes the agent, removes any legacy Windows Service, creates a Scheduled Task
    named "IRISAgent" that launches the agent in the user's interactive session on logon.
    This ensures full desktop access for screen capture, freeze overlay, and dialogs.
.EXAMPLE
    .\install-agent.ps1
    .\install-agent.ps1 -PublishDir "C:\IRIS\Agent"
#>
param(
    [string]$PublishDir = "C:\IRIS\Agent",
    [string]$TaskName = "IRISAgent"
)

$ErrorActionPreference = "Stop"

# Remove legacy Windows Service if it exists (migration path)
$existing = Get-Service -Name $TaskName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Found legacy Windows Service '$TaskName'. Removing..." -ForegroundColor Yellow
    Stop-Service -Name $TaskName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    sc.exe delete $TaskName | Out-Null
    Start-Sleep -Seconds 2
    Write-Host "Legacy service removed." -ForegroundColor Green
}

# Remove existing scheduled task if it exists
if (Get-Command Get-ScheduledTask -ErrorAction SilentlyContinue) {
    $existingTask = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
    if ($existingTask) {
        Write-Host "Removing existing scheduled task '$TaskName'..." -ForegroundColor Yellow
        Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
    }
}
else {
    # Fallback for environments where ScheduledTasks cmdlets are unavailable.
    schtasks /Query /TN $TaskName 2>$null | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Removing existing scheduled task '$TaskName'..." -ForegroundColor Yellow
        schtasks /Delete /TN $TaskName /F | Out-Null
    }
}

# Kill any running agent processes
$agentProcesses = Get-Process -Name "IRIS.Agent" -ErrorAction SilentlyContinue
if ($agentProcesses) {
    Write-Host "Stopping running IRIS Agent processes..." -ForegroundColor Yellow
    $agentProcesses | Stop-Process -Force
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

# Create the Scheduled Task (ONLOGON — runs in user's interactive session as admin)
Write-Host "Creating Scheduled Task '$TaskName' (runs on user logon with admin rights)..." -ForegroundColor Cyan

# Determine the user to run as — prefer current installer (who must already be admin per #Requires)
$runAsUser = "$env:USERDOMAIN\$env:USERNAME"
Write-Host "  Task will run as: $runAsUser (with highest privileges)" -ForegroundColor Gray

# Verify current user is in Administrators group
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "Current user '$runAsUser' is not in the Administrators group. Run this installer as an admin user."
    exit 1
}

try {
    $action    = New-ScheduledTaskAction    -Execute $exePath -Argument "--background"
    $trigger   = New-ScheduledTaskTrigger   -AtLogOn -User $runAsUser
    $principal = New-ScheduledTaskPrincipal -UserId $runAsUser -LogonType Interactive -RunLevel Highest
    $settings  = New-ScheduledTaskSettingsSet `
        -AllowStartIfOnBatteries `
        -DontStopIfGoingOnBatteries `
        -ExecutionTimeLimit ([TimeSpan]::Zero) `
        -MultipleInstances IgnoreNew `
        -StartWhenAvailable

    Register-ScheduledTask -TaskName $TaskName `
        -Action $action `
        -Trigger $trigger `
        -Principal $principal `
        -Settings $settings `
        -Force | Out-Null
}
catch {
    Write-Error "Register-ScheduledTask failed: $_"
    exit 1
}

# Validate appsettings.json
$settingsPath = Join-Path $PublishDir "appsettings.json"
if (Test-Path $settingsPath) {
    $content = Get-Content $settingsPath -Raw
    if ($content -match '"Host=localhost') {
        Write-Host ""
        Write-Host "WARNING: appsettings.json still has 'localhost' as the database host." -ForegroundColor Yellow
        Write-Host "         Update the connection string to point to the IRIS server before logon." -ForegroundColor Yellow
        Write-Host ""
    }
}

# Launch the agent now for the current session (via scheduled task so it runs elevated)
Write-Host "Starting IRIS Agent for current session (elevated)..." -ForegroundColor Cyan
schtasks /Run /TN $TaskName | Out-Null

Write-Host ""
Write-Host "IRIS Agent installed successfully." -ForegroundColor Green
Write-Host "  Task:      $TaskName (runs on every user logon)"
Write-Host "  Path:      $exePath"
Write-Host "  Mode:      Background (console hidden)"
Write-Host ""
Write-Host "Manage with:"
Write-Host "  Status:    schtasks /Query /TN $TaskName"
Write-Host "  Run now:   schtasks /Run /TN $TaskName"
Write-Host "  Stop:      Get-Process IRIS.Agent | Stop-Process"
Write-Host "  Uninstall: .\uninstall-agent.ps1"
