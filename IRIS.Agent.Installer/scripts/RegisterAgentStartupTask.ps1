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

$user = "$env:USERDOMAIN\$env:USERNAME"
if ([string]::IsNullOrWhiteSpace($user) -or $user -eq "\") {
	throw "Unable to determine current interactive user for scheduled task registration."
}

$action = New-ScheduledTaskAction -Execute $exePath -Argument "--background"
$trigger = New-ScheduledTaskTrigger -AtLogOn
$principal = New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -MultipleInstances IgnoreNew -Hidden

Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force | Out-Null

try {
	Start-ScheduledTask -TaskName $TaskName | Out-Null
}
catch {
	# Task creation is the fail-fast requirement. Immediate start can be blocked by session conditions.
}
