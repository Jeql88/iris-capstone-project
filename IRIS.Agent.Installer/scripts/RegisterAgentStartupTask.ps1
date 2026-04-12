param(
	[Parameter(Mandatory = $true)]
	[string]$InstallDir,
	[string]$TaskName = "IRISAgent"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$exePath = Join-Path $InstallDir "IRIS.Agent.exe"
if (-not (Test-Path $exePath)) {
	throw "Agent executable not found at $exePath"
}

$action = New-ScheduledTaskAction -Execute $exePath -Argument "--background"
$trigger = New-ScheduledTaskTrigger -AtLogOn
$principal = New-ScheduledTaskPrincipal -GroupId "BUILTIN\Users" -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -MultipleInstances IgnoreNew -Hidden

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

$wakeTaskName = "IRISAgentWakeCheck"
$wakeIntervalMinutes = 15

try {
	$wakeAction  = New-ScheduledTaskAction -Execute "cmd.exe" -Argument "/c exit 0"
	$wakeTrigger = New-ScheduledTaskTrigger -Once -At (Get-Date).Date `
		-RepetitionInterval (New-TimeSpan -Minutes $wakeIntervalMinutes) `
		-RepetitionDuration (New-TimeSpan -Days 9999)
	$wakePrincipal = New-ScheduledTaskPrincipal -GroupId "BUILTIN\Users" -RunLevel Highest
	$wakeSettings = New-ScheduledTaskSettingsSet `
		-AllowStartIfOnBatteries `
		-DontStopIfGoingOnBatteries `
		-WakeToRun `
		-StartWhenAvailable `
		-MultipleInstances IgnoreNew `
		-Hidden

	Register-ScheduledTask -TaskName $wakeTaskName -Action $wakeAction -Trigger $wakeTrigger -Principal $wakePrincipal -Settings $wakeSettings -Force | Out-Null
}
catch {
	# Wake timer is non-critical - agent still works, just won't wake sleeping PCs
	Write-Warning "Failed to create wake timer task '$wakeTaskName': $_"
}
