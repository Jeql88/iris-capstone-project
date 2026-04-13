Set-StrictMode -Version Latest

# Use the script's own directory - it's installed alongside IRIS.Agent.exe
$exePath = Join-Path $PSScriptRoot "IRIS.Agent.exe"
if (-not (Test-Path $exePath)) {
	throw "Agent executable not found at $exePath"
}

# 1) Register the SYSTEM helper task - runs at boot, spawns per-session user agents, hosts admin-op pipe
try {
	$helperAction    = New-ScheduledTaskAction -Execute $exePath -Argument '--system-helper'
	$helperTrigger   = New-ScheduledTaskTrigger -AtStartup
	$helperPrincipal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
	$helperSettings  = New-ScheduledTaskSettingsSet `
		-AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
		-RestartCount 5 -RestartInterval (New-TimeSpan -Minutes 1) `
		-ExecutionTimeLimit ([TimeSpan]::Zero) `
		-MultipleInstances IgnoreNew `
		-StartWhenAvailable -Hidden

	Register-ScheduledTask -TaskName 'IRISAgentHelper' `
		-Action $helperAction -Trigger $helperTrigger `
		-Principal $helperPrincipal -Settings $helperSettings -Force | Out-Null
}
catch {
	Write-Warning "Failed to create helper task 'IRISAgentHelper': $_"
	exit 1
}

# 2) Remove legacy per-user IRISAgent task if present (helper replaces it)
Unregister-ScheduledTask -TaskName 'IRISAgent' -Confirm:$false -ErrorAction SilentlyContinue

# 3) Start the helper now so the current boot session gets an agent without a reboot
try {
	Start-ScheduledTask -TaskName 'IRISAgentHelper' | Out-Null
}
catch {
	# Immediate start can be blocked by session conditions.
}

# 4) Keep the wake timer task for sleep-wake scenarios
$wakeTaskName = "IRISAgentWakeCheck"
$wakeIntervalMinutes = 15

try {
	$wakeAction  = New-ScheduledTaskAction -Execute "cmd.exe" -Argument "/c exit 0"
	$wakeTrigger = New-ScheduledTaskTrigger -Once -At (Get-Date).Date `
		-RepetitionInterval (New-TimeSpan -Minutes $wakeIntervalMinutes) `
		-RepetitionDuration (New-TimeSpan -Days 9999)
	$wakePrincipal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
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
	Write-Warning "Failed to create wake timer task '$wakeTaskName': $_"
}
