<#
.SYNOPSIS
    1000 sequential /snapshot calls against one agent (docs/STRESS_TESTING.md §5.1).

.PARAMETER AgentIp
    IP of the target agent PC.

.PARAMETER Token
    Value of the X-IRIS-Snapshot-Token header (from the agent's appsettings).

.PARAMETER Iterations
    Number of snapshots to fetch sequentially. Default 1000.

.EXAMPLE
    pwsh -File scripts/stress-snapshot-sequential.ps1 -AgentIp 10.0.0.21 -Token "abc123"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $AgentIp,
    [Parameter(Mandatory)] [string] $Token,
    [int]    $Iterations = 1000,
    [int]    $Port       = 5057,
    [string] $WorkDir    = $(Join-Path $PSScriptRoot ("..\dist\stress\snapshot-seq-{0}" -f (Get-Date -Format 'yyyyMMdd-HHmmss'))),
    [string] $OutputCsv  = $null
)

if (-not (Test-Path $WorkDir)) { New-Item -ItemType Directory -Path $WorkDir -Force | Out-Null }
if (-not $OutputCsv) { $OutputCsv = Join-Path $WorkDir "snapshot-seq.csv" }

$url = "http://$($AgentIp):$Port/snapshot"
$results = New-Object System.Collections.Generic.List[object]

Write-Host "Sequential snapshot stress: $url x $Iterations" -ForegroundColor Cyan

for ($i = 1; $i -le $Iterations; $i++) {
    $file = Join-Path $WorkDir ("snap_{0:D4}.jpg" -f $i)
    $sw   = [System.Diagnostics.Stopwatch]::StartNew()
    & curl.exe -s -o $file -H "X-IRIS-Snapshot-Token: $Token" $url 2>$null
    $exit = $LASTEXITCODE
    $sw.Stop()
    $bytes = if (Test-Path $file) { (Get-Item $file).Length } else { 0 }

    $results.Add([pscustomobject]@{
        N        = $i
        Ms       = $sw.ElapsedMilliseconds
        Bytes    = $bytes
        ExitCode = $exit
    })

    if (($i % 50) -eq 0) {
        Write-Host ("  {0,4}/{1}  last={2,5} ms  size={3,7} B" -f $i, $Iterations, $sw.ElapsedMilliseconds, $bytes)
    }
}

$results | Export-Csv -Path $OutputCsv -NoTypeInformation

$lat       = $results | ForEach-Object { [double]$_.Ms } | Sort-Object
$failures  = ($results | Where-Object { $_.Bytes -lt 1024 -or $_.ExitCode -ne 0 }).Count
$mean      = ($results | Measure-Object Ms    -Average).Average
$p95Idx    = [int][Math]::Ceiling(0.95 * $lat.Count) - 1
$p99Idx    = [int][Math]::Ceiling(0.99 * $lat.Count) - 1
$meanBytes = ($results | Measure-Object Bytes -Average).Average

Write-Host ""
Write-Host "RESULT  -----------------------------------------------------" -ForegroundColor Green
"  Iterations    : $Iterations"
"  Failures      : $failures   (target: 0)"
"  Mean latency  : {0:N1} ms" -f $mean
"  95p latency   : {0:N1} ms   (target: < 500 ms)" -f $lat[$p95Idx]
"  99p latency   : {0:N1} ms" -f $lat[$p99Idx]
"  Mean JPEG     : {0:N0} bytes  (target: 30-80 KB)" -f $meanBytes
"  CSV           : $OutputCsv"
"  JPEG dir      : $WorkDir"
