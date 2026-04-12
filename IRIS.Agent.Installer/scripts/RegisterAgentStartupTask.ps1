param(
	[Parameter(Mandatory = $true)]
	[string]$InstallDir,
	[string]$TaskName = "IRISAgent",
	[string]$TaskUser = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$exePath = Join-Path $InstallDir "IRIS.Agent.exe"
if (-not (Test-Path $exePath)) {
	throw "Agent executable not found at $exePath"
}

# Determine the user principal for the task
$isSystemOrEmpty = [string]::IsNullOrWhiteSpace($TaskUser) -or
                   $TaskUser -match '(?i)^NT AUTHORITY\\SYSTEM$' -or
                   $TaskUser -eq "\" -or
                   $TaskUser -match '(?i)SYSTEM$'

$action = New-ScheduledTaskAction -Execute $exePath -Argument "--background"
$trigger = New-ScheduledTaskTrigger -AtLogOn
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -MultipleInstances IgnoreNew -Hidden

if ($isSystemOrEmpty) {
	# SCCM/GPO scenario: create task for the built-in Users group (triggers for ALL users)
	$principal = New-ScheduledTaskPrincipal -GroupId "BUILTIN\Users" -RunLevel Highest
} else {
	# Interactive install: create task for the specific installing user
	$principal = New-ScheduledTaskPrincipal -UserId $TaskUser -LogonType Interactive -RunLevel Highest
}

Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force | Out-Null

try {
	Start-ScheduledTask -TaskName $TaskName | Out-Null
}
catch {
	# Task creation is the fail-fast requirement. Immediate start can be blocked by session conditions.
}

# --- Wake Timer Task ---
# Creates a lightweight scheduled task whose sole purpose is to wake the PC
# from sleep periodically. The agent process is already running; it detects
# the wake via SystemEvents.PowerModeChanged and runs its idle check.
# The task runs a no-op command — it just needs to exist with WakeToRun enabled.

$wakeTaskName = "IRISAgentWakeCheck"
$wakeIntervalMinutes = 15

try {
	$wakeAction  = New-ScheduledTaskAction -Execute "cmd.exe" -Argument "/c exit 0"
	$wakeTrigger = New-ScheduledTaskTrigger -Once -At (Get-Date).Date -RepetitionInterval (New-TimeSpan -Minutes $wakeIntervalMinutes) -RepetitionDuration ([TimeSpan]::MaxValue)
	$wakeSettings = New-ScheduledTaskSettingsSet `
		-AllowStartIfOnBatteries `
		-DontStopIfGoingOnBatteries `
		-WakeToRun `
		-StartWhenAvailable `
		-MultipleInstances IgnoreNew `
		-Hidden

	if ($isSystemOrEmpty) {
		$wakePrincipal = New-ScheduledTaskPrincipal -GroupId "BUILTIN\Users" -RunLevel Highest
	} else {
		$wakePrincipal = New-ScheduledTaskPrincipal -UserId $TaskUser -LogonType Interactive -RunLevel Highest
	}

	Register-ScheduledTask -TaskName $wakeTaskName -Action $wakeAction -Trigger $wakeTrigger -Principal $wakePrincipal -Settings $wakeSettings -Force | Out-Null
}
catch {
	# Wake timer is non-critical - agent still works, just won't wake sleeping PCs
	Write-Warning "Failed to create wake timer task '$wakeTaskName': $_"
}
