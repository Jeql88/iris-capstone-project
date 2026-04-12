<#
.SYNOPSIS
    Updates IRIS UI/Agent/Core appsettings for deployment.
.DESCRIPTION
    Applies server-host values to connection strings and selected agent endpoint settings.
    This script edits JSON/JSONC text directly so it works with commented appsettings files.
.EXAMPLE
    .\scripts\Update-IrisAppSettings.ps1 -DbHost localhost -UiHost localhost
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$DbHost,

    [int]$DbPort = 5432,
    [string]$DbName = "iris_db",
    [string]$DbUser = "postgres",
    [string]$DbPassword = "postgres",

    [string]$UiHost = "",
    [int]$UiCommandPort = 5091,
    [int]$UiWallpaperPort = 5092,

    [string]$UiConfigPath = "IRIS.UI\appsettings.json",
    [string]$AgentConfigPath = "IRIS.Agent\appsettings.json",
    [string]$CoreConfigPath = "IRIS.Core\appsettings.json"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

function Resolve-ConfigPath {
    param([string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    return (Join-Path $repoRoot $Path)
}

function Build-ConnectionString {
    param([string]$DbHostName, [int]$Port, [string]$Database, [string]$Username, [string]$Password)
    return "Host=$DbHostName;Port=$Port;Database=$Database;Username=$Username;Password=$Password"
}

function Replace-RegexOrFail {
    param(
        [string]$Text,
        [string]$Pattern,
        [string]$Replacement,
        [string]$Description
    )

    if (-not [Regex]::IsMatch($Text, $Pattern, [System.Text.RegularExpressions.RegexOptions]::Multiline)) {
        throw "Could not find $Description in file content."
    }

    $updated = [Regex]::Replace($Text, $Pattern, $Replacement, [System.Text.RegularExpressions.RegexOptions]::Multiline)

    return $updated
}

function Update-ConnectionString {
    param([string]$Path, [string]$NewConnectionString)

    $resolvedPath = Resolve-ConfigPath -Path $Path

    if (-not (Test-Path $resolvedPath)) {
        Write-Host "Skip: $resolvedPath (not found)" -ForegroundColor Yellow
        return
    }

    $raw = Get-Content -Path $resolvedPath -Raw
    $replacement = '"IRISDatabase": "' + $NewConnectionString + '"'
    $updated = Replace-RegexOrFail -Text $raw -Pattern '"IRISDatabase"\s*:\s*"[^"]*"' -Replacement $replacement -Description "IRISDatabase connection string"

    Set-Content -Path $resolvedPath -Value $updated -Encoding UTF8
    Write-Host "Updated: $resolvedPath (IRISDatabase)" -ForegroundColor Green
}

function Update-AgentEndpoints {
    param([string]$Path, [string]$UiHostName, [int]$CommandPort, [int]$WallpaperPort)

    $resolvedPath = Resolve-ConfigPath -Path $Path

    if (-not (Test-Path $resolvedPath)) {
        Write-Host "Skip: $resolvedPath (not found)" -ForegroundColor Yellow
        return
    }

    if ([string]::IsNullOrWhiteSpace($UiHostName)) {
        Write-Host "Skip agent endpoint update: UiHost not provided." -ForegroundColor Yellow
        return
    }

    $raw = Get-Content -Path $resolvedPath -Raw
    $raw = Replace-RegexOrFail -Text $raw -Pattern '"CommandServerHost"\s*:\s*"[^"]*"' -Replacement ('"CommandServerHost": "' + $UiHostName + '"') -Description "CommandServerHost"
    $raw = Replace-RegexOrFail -Text $raw -Pattern '"CommandServerPort"\s*:\s*\d+' -Replacement ('"CommandServerPort": ' + $CommandPort) -Description "CommandServerPort"
    $raw = Replace-RegexOrFail -Text $raw -Pattern '"WallpaperServerBaseUrl"\s*:\s*"[^"]*"' -Replacement ('"WallpaperServerBaseUrl": "' + $UiHostName + ':' + $WallpaperPort + '"') -Description "WallpaperServerBaseUrl"

    Set-Content -Path $resolvedPath -Value $raw -Encoding UTF8
    Write-Host "Updated: $resolvedPath (agent endpoint settings)" -ForegroundColor Green
}

$connectionString = Build-ConnectionString -DbHostName $DbHost -Port $DbPort -Database $DbName -Username $DbUser -Password $DbPassword

Update-ConnectionString -Path $UiConfigPath -NewConnectionString $connectionString
Update-ConnectionString -Path $AgentConfigPath -NewConnectionString $connectionString
Update-ConnectionString -Path $CoreConfigPath -NewConnectionString $connectionString
Update-AgentEndpoints -Path $AgentConfigPath -UiHostName $UiHost -CommandPort $UiCommandPort -WallpaperPort $UiWallpaperPort

Write-Host "Done." -ForegroundColor Cyan
