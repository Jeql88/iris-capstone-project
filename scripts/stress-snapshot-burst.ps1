<#
.SYNOPSIS
    200-concurrent burst against one agent's /snapshot - regression test for the
    24H2 / dual-stack TcpListener fix (docs/STRESS_TESTING.md §5.3).

.PARAMETER AgentIp
    Target agent IP.

.PARAMETER Token
    X-IRIS-Snapshot-Token value.

.PARAMETER Concurrency
    Total parallel connections fired in one burst. Default 200.

.EXAMPLE
    pwsh -File scripts/stress-snapshot-burst.ps1 -AgentIp 10.0.0.21 -Token "abc123"

    # After running, tail the agent log on the target PC and confirm every
    # request is logged as `-> 200`:
    #   C:\ProgramData\IRIS\Agent\user-<date>.log
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $AgentIp,
    [Parameter(Mandatory)] [string] $Token,
    [int]    $Concurrency = 200,
    [int]    $Port        = 5057,
    [string] $WorkDir     = $(Join-Path $PSScriptRoot ("..\dist\stress\snapshot-burst-{0}" -f (Get-Date -Format 'yyyyMMdd-HHmmss')))
)

if ($PSVersionTable.PSVersion.Major -lt 7) {
    throw "Requires PowerShell 7+. Run with 'pwsh'."
}
if (-not (Test-Path $WorkDir)) { New-Item -ItemType Directory -Path $WorkDir -Force | Out-Null }

$url = "http://$($AgentIp):$Port/snapshot"
Write-Host "Burst snapshot stress: $url x $Concurrency  (single-shot, simultaneous)" -ForegroundColor Cyan

$rawLog = Join-Path $WorkDir "burst.log"

$results = 1..$Concurrency | ForEach-Object -ThrottleLimit $Concurrency -Parallel {
    $i     = $_
    $url   = $using:url
    $token = $using:Token
    $log   = $using:rawLog

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $output = & curl.exe -s -S -o nul -w "%{http_code}|%{time_total}|%{size_download}|%{errormsg}" `
                          --max-time 25 `
                          -H "X-IRIS-Snapshot-Token: $token" `
                          $url 2>&1
    $sw.Stop()
    $line = "{0,4}|{1,5} ms|{2}" -f $i, $sw.ElapsedMilliseconds, $output
    Add-Content -Path $log -Value $line

    $parts = ($output -split '\|', 4)
    [pscustomobject]@{
        N        = $i
        Http     = ($parts[0])
        TimeS    = ($parts[1])
        Bytes    = ($parts[2])
        Error    = if ($parts.Length -ge 4) { $parts[3] } else { "" }
        Ms       = $sw.ElapsedMilliseconds
    }
}

$csvPath = Join-Path $WorkDir "burst.csv"
$results | Export-Csv -Path $csvPath -NoTypeInformation

$ok           = ($results | Where-Object { $_.Http -eq "200" }).Count
$resetErrors  = ($results | Where-Object { $_.Error -match "reset|forcibly closed|Connection refused|timed out|timeout" }).Count
$nonOk        = $results.Count - $ok

Write-Host ""
Write-Host "RESULT  -----------------------------------------------------" -ForegroundColor Green
"  Total fired   : $($results.Count)"
"  HTTP 200      : $ok"
"  Non-200       : $nonOk             (target: 0)"
"  Reset/timeout : $resetErrors        (target: 0 - any non-zero is the 24H2 regression returning)"
"  Detail log    : $rawLog"
"  CSV           : $csvPath"
Write-Host ""
Write-Host "Now tail the agent log on $AgentIp and confirm every request is '-> 200':" -ForegroundColor Yellow
Write-Host "    C:\ProgramData\IRIS\Agent\user-<date>.log" -ForegroundColor Yellow
