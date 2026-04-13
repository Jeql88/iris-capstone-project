Set-StrictMode -Version Latest

$settingsPath = Join-Path $PSScriptRoot 'appsettings.json'
if (-not (Test-Path $settingsPath)) {
    Write-Warning "appsettings.json not found at $settingsPath - skipping pipe token configuration."
    exit 0
}

# Generate a 32-byte cryptographically random hex token
$bytes = New-Object byte[] 32
[System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
$token = -join ($bytes | ForEach-Object { '{0:x2}' -f $_ })

try {
    $json = Get-Content $settingsPath -Raw | ConvertFrom-Json

    if (-not $json.PSObject.Properties['HelperSettings']) {
        $json | Add-Member -NotePropertyName HelperSettings -NotePropertyValue ([pscustomobject]@{
            PipeName  = 'IRIS.Agent.Helper'
            PipeToken = $token
        })
    }
    else {
        $json.HelperSettings.PipeToken = $token
    }

    ($json | ConvertTo-Json -Depth 10) | Set-Content -Path $settingsPath -Encoding UTF8
}
catch {
    Write-Warning "Failed to configure pipe token: $_"
    exit 1
}
