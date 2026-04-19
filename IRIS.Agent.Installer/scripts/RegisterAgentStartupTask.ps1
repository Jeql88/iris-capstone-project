Set-StrictMode -Version Latest

# --- Install-time logging -----------------------------------------------------
# Every step + any exception is appended to install.log so we can explain any
# "red" messages the operator sees in the MSI UI or console flashes from
# deferred CustomActions.
$logDir  = 'C:\ProgramData\IRIS\Agent'
$logPath = Join-Path $logDir 'install.log'
try {
	if (-not (Test-Path $logDir)) {
		New-Item -ItemType Directory -Path $logDir -Force | Out-Null
	}
}
catch {
	# Fall back to TEMP if ProgramData is inaccessible.
	$logDir  = $env:TEMP
	$logPath = Join-Path $logDir 'IRIS.Agent.install.log'
}

function Write-Log {
	param(
		[Parameter(Mandatory)] [string] $Message,
		[ValidateSet('INFO','WARN','ERROR')] [string] $Level = 'INFO'
	)
	$line = '{0} [{1}] RegisterAgentStartupTask: {2}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff'), $Level, $Message
	Add-Content -Path $logPath -Value $line -Encoding UTF8 -ErrorAction SilentlyContinue
}

Write-Log "Script start. PSVersion=$($PSVersionTable.PSVersion) User=$env:USERNAME Host=$env:COMPUTERNAME ScriptRoot=$PSScriptRoot"

try {
	$exePath = Join-Path $PSScriptRoot "IRIS.Agent.exe"
	if (-not (Test-Path $exePath)) {
		Write-Log "Agent executable not found at $exePath" 'ERROR'
		throw "Agent executable not found at $exePath"
	}
	Write-Log "Resolved agent exe: $exePath"

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
		Write-Log "Registered scheduled task 'IRISAgentHelper'"
	}
	catch {
		Write-Log "Failed to create helper task 'IRISAgentHelper': $_" 'ERROR'
		Write-Warning "Failed to create helper task 'IRISAgentHelper': $_"
		exit 1
	}

	# 2) Remove legacy per-user IRISAgent task if present (helper replaces it).
	# Probe first so we only call Unregister when the task actually exists.
	# Unregister-ScheduledTask emits a non-terminating (red) error for a missing
	# task even with -ErrorAction SilentlyContinue on some Windows builds.
	$legacyTask = Get-ScheduledTask -TaskName 'IRISAgent' -ErrorAction SilentlyContinue
	if ($legacyTask) {
		try {
			Unregister-ScheduledTask -TaskName 'IRISAgent' -Confirm:$false -ErrorAction Stop
			Write-Log "Removed legacy per-user task 'IRISAgent'"
		}
		catch {
			Write-Log "Failed to remove legacy task 'IRISAgent': $_" 'WARN'
		}
	}
	else {
		Write-Log "No legacy 'IRISAgent' task found - skipping."
	}

	# 3) Start the helper now so the current boot session gets an agent without a reboot
	try {
		Start-ScheduledTask -TaskName 'IRISAgentHelper' -ErrorAction Stop | Out-Null
		Write-Log "Started 'IRISAgentHelper' for current session."
	}
	catch {
		Write-Log "Start-ScheduledTask 'IRISAgentHelper' failed (often benign during MSI CustomAction): $_" 'WARN'
	}

	# 4) Wake timer task for sleep-wake scenarios
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
		Write-Log "Registered wake-timer task '$wakeTaskName' (interval=${wakeIntervalMinutes}m)"
	}
	catch {
		Write-Log "Failed to create wake timer task '$wakeTaskName': $_" 'WARN'
		Write-Warning "Failed to create wake timer task '$wakeTaskName': $_"
	}

	Write-Log "Script completed successfully."
}
catch {
	$errMsg = "UNHANDLED EXCEPTION: " + $_.Exception.Message + [Environment]::NewLine + $_.ScriptStackTrace
	Write-Log $errMsg 'ERROR'
	throw
}
