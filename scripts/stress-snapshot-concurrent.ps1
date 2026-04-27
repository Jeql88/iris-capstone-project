<#
.SYNOPSIS
    8 concurrent runners hitting one agent's /snapshot (docs/STRESS_TESTING.md §5.2).

.PARAMETER AgentIp
    Target agent IP.

.PARAMETER Token
    X-IRIS-Snapshot-Token value.

.PARAMETER Runners
    Parallelism. Default 8 to match doc.

.PARAMETER PerRunner
    Snapshots per runner. Default 200 (8 x 200 = 1600 total requests).

.EXAMPLE
    pwsh -File scripts/stress-snapshot-concurrent.ps1 -AgentIp 10.0.0.21 -Token "abc123"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $AgentIp,
    [Parameter(Mandatory)] [string] $Token,
    [int]    $Runners   = 8,
    [int]    $PerRunner = 200,
    [int]    $Port      = 5057,
    [string] $WorkDir   = $(Join-Path $PSScriptRoot ("..\dist\stress\snapshot-conc-{0}" -f (Get-Date -Format 'yyyyMMdd-HHmmss')))
)

if ($PSVersionTable.PSVersion.Major -lt 7) {
    throw "Requires PowerShell 7+. Run with 'pwsh'."
}
if (-not (Test-Path $WorkDir)) { New-Item -ItemType Directory -Path $WorkDir -Force | Out-Null }

$url = "http://$($AgentIp):$Port/snapshot"
Write-Host "Concurrent snapshot stress: $url   $Runners runners x $PerRunner = $($Runners * $PerRunner) requests" -ForegroundColor Cyan

$samples = 1..$Runners | ForEach-Object -ThrottleLimit $Runners -Parallel {
    $runnerId = $_
    $url      = $using:url
    $token    = $using:Token
    $work     = $using:WorkDir
    $count    = $using:PerRunner

    $rows = New-Object System.Collections.Generic.List[object]
    for ($i = 1; $i -le $count; $i++) {
        $file = Join-Path $work ("r{0}_{1:D4}.jpg" -f $runnerId, $i)
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        & curl.exe -s -o $file -H "X-IRIS-Snapshot-Token: $token" $url 2>$null
        $exit = $LASTEXITCODE
        $sw.Stop()
        $bytes = if (Test-Path $file) { (Get-Item $file).Length } else { 0 }
        $rows.Add([pscustomobject]@{
            Runner   = $runnerId
            N        = $i
            Ms       = $sw.ElapsedMilliseconds
            Bytes    = $bytes
            ExitCode = $exit
        })
    }
    return $rows
}

# Flatten -Parallel return (it returns lists from each runner).
$flat = New-Object System.Collections.Generic.List[object]
foreach ($s in $samples) {
    foreach ($row in $s) { $flat.Add($row) }
}

$csvPath = Join-Path $WorkDir "snapshot-concurrent.csv"
$flat | Export-Csv -Path $csvPath -NoTypeInformation

# Per-runner verdict.
Write-Host ""
Write-Host "Per-runner summary -----------------------------------------" -ForegroundColor Green
$flat | Group-Object Runner | ForEach-Object {
    $g       = $_.Group
    $lat     = $g | ForEach-Object { [double]$_.Ms } | Sort-Object
    $fail    = ($g | Where-Object { $_.Bytes -lt 1024 -or $_.ExitCode -ne 0 }).Count
    $success = 100.0 * ($g.Count - $fail) / $g.Count
    $p95idx  = [int][Math]::Ceiling(0.95 * $lat.Count) - 1
    "  Runner {0}  total={1}  success={2:N2}%  95p={3} ms" -f $_.Name, $g.Count, $success, $lat[$p95idx]
}

$allLat   = $flat | ForEach-Object { [double]$_.Ms } | Sort-Object
$failures = ($flat | Where-Object { $_.Bytes -lt 1024 -or $_.ExitCode -ne 0 }).Count
$success  = 100.0 * ($flat.Count - $failures) / $flat.Count
$p95idx   = [int][Math]::Ceiling(0.95 * $allLat.Count) - 1

Write-Host ""
Write-Host "OVERALL --------------------------------------------------" -ForegroundColor Green
"  Total requests : $($flat.Count)"
"  Failures       : $failures"
"  Success rate   : {0:N3} %  (target: >= 99 %)" -f $success
"  95p latency    : {0:N1} ms  (target: < 1000 ms)" -f $allLat[$p95idx]
"  CSV            : $csvPath"
