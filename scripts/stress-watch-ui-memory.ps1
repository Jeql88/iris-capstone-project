<#
.SYNOPSIS
    Logs IRIS.UI process memory + CPU once every 30 minutes for the 8h soak (docs/STRESS_TESTING.md §4.4).

.PARAMETER OutputCsv
    Where to append samples. Defaults to dist/stress/ui-memory.csv.

.PARAMETER IntervalSeconds
    Sampling interval. The doc asks for 30-minute samples.

.EXAMPLE
    pwsh -File scripts/stress-watch-ui-memory.ps1
    pwsh -File scripts/stress-watch-ui-memory.ps1 -IntervalSeconds 60   # quick smoke
#>

[CmdletBinding()]
param(
    [string] $OutputCsv       = $(Join-Path $PSScriptRoot "..\dist\stress\ui-memory.csv"),
    [int]    $IntervalSeconds = 1800,
    [string] $ProcessName     = "IRIS.UI"
)

$outDir = Split-Path -Parent $OutputCsv
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

if (-not (Test-Path $OutputCsv)) {
    "Timestamp,PID,WorkingSetMB,PrivateBytesMB,CpuSeconds,Threads,Handles" | Out-File -FilePath $OutputCsv -Encoding UTF8
}

Write-Host "Watching '$ProcessName' every $IntervalSeconds s -> $OutputCsv  (Ctrl+C to stop)" -ForegroundColor Cyan

while ($true) {
    $proc = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $proc) {
        $line = "{0:yyyy-MM-dd HH:mm:ss},,,,,," -f (Get-Date)
    } else {
        $wsMb  = [Math]::Round($proc.WorkingSet64    / 1MB, 1)
        $pvMb  = [Math]::Round($proc.PrivateMemorySize64 / 1MB, 1)
        $cpuS  = [Math]::Round($proc.TotalProcessorTime.TotalSeconds, 1)
        $line  = "{0:yyyy-MM-dd HH:mm:ss},{1},{2},{3},{4},{5},{6}" -f `
                 (Get-Date), $proc.Id, $wsMb, $pvMb, $cpuS, $proc.Threads.Count, $proc.HandleCount
    }
    $line | Out-File -FilePath $OutputCsv -Append -Encoding UTF8
    Write-Host $line
    Start-Sleep -Seconds $IntervalSeconds
}
