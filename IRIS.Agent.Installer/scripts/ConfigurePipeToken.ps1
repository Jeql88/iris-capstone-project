Set-StrictMode -Version Latest

$logDir  = 'C:\ProgramData\IRIS\Agent'
$logPath = Join-Path $logDir 'install.log'
try {
    if (-not (Test-Path $logDir)) {
        New-Item -ItemType Directory -Path $logDir -Force | Out-Null
    }
}
catch {
    $logDir  = $env:TEMP
    $logPath = Join-Path $logDir 'IRIS.Agent.install.log'
}

function Write-Log {
    param(
        [Parameter(Mandatory)] [string] $Message,
        [ValidateSet('INFO','WARN','ERROR')] [string] $Level = 'INFO'
    )
    $line = '{0} [{1}] ConfigurePipeToken: {2}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff'), $Level, $Message
    Add-Content -Path $logPath -Value $line -Encoding UTF8 -ErrorAction SilentlyContinue
}

Write-Log "Script start. ScriptRoot=$PSScriptRoot"

$settingsPath = Join-Path $PSScriptRoot 'appsettings.json'
if (-not (Test-Path $settingsPath)) {
    Write-Log "appsettings.json not found at $settingsPath - skipping pipe token configuration." 'WARN'
    Write-Warning "appsettings.json not found at $settingsPath - skipping pipe token configuration."
    exit 0
}

# Generate a 32-byte cryptographically random hex token.
# Use Create().GetBytes() - works on Windows PowerShell 5.1 AND PS 7+.
# (RandomNumberGenerator.Fill is .NET 5+ only, not available in PS 5.1.)
$bytes = New-Object byte[] 32
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
try {
    $rng.GetBytes($bytes)
}
finally {
    $rng.Dispose()
}
$token = -join ($bytes | ForEach-Object { '{0:x2}' -f $_ })

try {
    $json = Get-Content $settingsPath -Raw | ConvertFrom-Json

    if (-not $json.PSObject.Properties['HelperSettings']) {
        $json | Add-Member -NotePropertyName HelperSettings -NotePropertyValue ([pscustomobject]@{
            PipeName  = 'IRIS.Agent.Helper'
            PipeToken = $token
        })
        Write-Log "Created HelperSettings section in appsettings.json"
    }
    else {
        $json.HelperSettings.PipeToken = $token
        Write-Log "Updated HelperSettings.PipeToken in appsettings.json"
    }

    ($json | ConvertTo-Json -Depth 10) | Set-Content -Path $settingsPath -Encoding UTF8
    Write-Log "Script completed successfully."
}
catch {
    $errMsg = "Failed to configure pipe token: " + $_.Exception.Message + [Environment]::NewLine + $_.ScriptStackTrace
    Write-Log $errMsg 'ERROR'
    Write-Warning "Failed to configure pipe token: $_"
    exit 1
}
