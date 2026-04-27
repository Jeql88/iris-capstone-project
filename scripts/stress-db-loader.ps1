<#
.SYNOPSIS
    Synthetic IRIS-agent loader for the database heartbeat-scaling stress test (docs/STRESS_TESTING.md §3).

.DESCRIPTION
    Spawns N concurrent runspaces. Each runspace impersonates one lab PC by talking
    to PostgreSQL via Npgsql with the same cadence the real agent uses:
        every  5 s : UPDATE "PCs" SET "LastSeen" = now() WHERE "MacAddress" = ...
                     SELECT * FROM "PendingCommands" WHERE "MacAddress" = ... AND "Status" = 0
        every 30 s : INSERT into "HardwareMetrics" + "NetworkMetrics"

    Per-operation latencies (heartbeat UPDATE) are recorded with high-precision
    Stopwatch and dumped to a CSV. A summary (50p/95p/99p/max + error count) is
    printed at the end.

    Synthetic PC rows must already exist (run scripts/stress-db-seed.sql first).
    Cleanup is the operator's job (scripts/stress-db-cleanup.sql).

.PARAMETER ConnectionString
    Npgsql connection string. Defaults to the value in IRIS.Core/appsettings.json.

.PARAMETER AgentCount
    Number of synthetic agents (parallel runspaces). Run 80, 160, 240, 320 per the doc.

.PARAMETER DurationSeconds
    Test duration. The doc asks for 60-minute runs (3600).

.PARAMETER OutputCsv
    Path to write per-heartbeat latency samples.

.EXAMPLE
    pwsh -File scripts/stress-db-loader.ps1 -AgentCount 80  -DurationSeconds 3600
    pwsh -File scripts/stress-db-loader.ps1 -AgentCount 240 -DurationSeconds 3600
#>

[CmdletBinding()]
param(
    [string] $ConnectionString = "Host=localhost;Port=5432;Database=iris_db;Username=postgres;Password=postgres;Pooling=true;Maximum Pool Size=400;Timeout=15;Command Timeout=15",
    [int]    $AgentCount       = 80,
    [int]    $DurationSeconds  = 3600,
    [string] $OutputCsv        = $(Join-Path $PSScriptRoot ("..\dist\stress\db-loader-{0}-{1}.csv" -f (Get-Date -Format 'yyyyMMdd-HHmmss'), $AgentCount)),
    [string] $NpgsqlDllPath    = $(Join-Path $PSScriptRoot "..\IRIS.Core\bin\Debug\net9.0\Npgsql.dll")
)

if ($PSVersionTable.PSVersion.Major -lt 7) {
    throw "Requires PowerShell 7+ (ForEach-Object -Parallel). Run with 'pwsh', not 'powershell'."
}

# ---------------------------------------------------------------------------
# Load Npgsql. We reuse the DLL that already ships with the IRIS.Core build
# output so there's nothing extra to install.
# ---------------------------------------------------------------------------
if (-not (Test-Path $NpgsqlDllPath)) {
    Write-Host "Npgsql.dll not found at $NpgsqlDllPath" -ForegroundColor Yellow
    Write-Host "Build IRIS.Core first:  dotnet build IRIS.Core/IRIS.Core.csproj" -ForegroundColor Yellow
    throw "Missing Npgsql.dll"
}
Add-Type -Path $NpgsqlDllPath
$resolvedDll = (Resolve-Path $NpgsqlDllPath).Path

# Ensure output directory exists.
$outDir = Split-Path -Parent $OutputCsv
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

# ---------------------------------------------------------------------------
# Verify synthetic PCs exist.
# ---------------------------------------------------------------------------
$conn = New-Object Npgsql.NpgsqlConnection $ConnectionString
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = 'SELECT COUNT(*) FROM "PCs" WHERE "Hostname" LIKE ''LOAD_PC_%'''
$existing = [int]$cmd.ExecuteScalar()
$conn.Close()

if ($existing -lt $AgentCount) {
    throw "Only $existing synthetic PCs found in DB, but AgentCount=$AgentCount. Run scripts/stress-db-seed.sql with -v count=$AgentCount first."
}

Write-Host "==============================================================" -ForegroundColor Cyan
Write-Host " IRIS DB Stress Loader" -ForegroundColor Cyan
Write-Host "   AgentCount      : $AgentCount" -ForegroundColor Cyan
Write-Host "   DurationSeconds : $DurationSeconds" -ForegroundColor Cyan
Write-Host "   Output CSV      : $OutputCsv" -ForegroundColor Cyan
Write-Host "==============================================================" -ForegroundColor Cyan

$globalStart = Get-Date

# ---------------------------------------------------------------------------
# Run N agents in parallel.  Each agent reports a hashtable summary back.
# ---------------------------------------------------------------------------
$results = 1..$AgentCount | ForEach-Object -ThrottleLimit $AgentCount -Parallel {
    $agentIndex      = $_ - 1
    $connStr         = $using:ConnectionString
    $duration        = $using:DurationSeconds
    $dllPath         = $using:resolvedDll

    Add-Type -Path $dllPath

    $mac = "AA:BB:CC:{0:X2}:{1:X2}:{2:X2}" -f ($agentIndex -band 0xFF),
                                              (($agentIndex -shr 8)  -band 0xFF),
                                              (($agentIndex -shr 16) -band 0xFF)

    $conn = New-Object Npgsql.NpgsqlConnection $connStr
    $conn.Open()

    # Resolve PCId once.
    $pcCmd = $conn.CreateCommand()
    $pcCmd.CommandText = 'SELECT "Id" FROM "PCs" WHERE "MacAddress" = @mac'
    [void]$pcCmd.Parameters.AddWithValue("mac", $mac)
    $pcIdObj = $pcCmd.ExecuteScalar()
    if ($null -eq $pcIdObj) {
        $conn.Close()
        return [pscustomobject]@{ Mac = $mac; Heartbeats = 0; Errors = 1; Latencies = @(); Note = "PC row missing" }
    }
    $pcId = [int]$pcIdObj

    # Prepared commands for the hot path.
    $hb = $conn.CreateCommand()
    $hb.CommandText = 'UPDATE "PCs" SET "LastSeen" = now() WHERE "MacAddress" = @mac'
    [void]$hb.Parameters.AddWithValue("mac", $mac)
    $hb.Prepare()

    $poll = $conn.CreateCommand()
    $poll.CommandText = 'SELECT "Id" FROM "PendingCommands" WHERE "MacAddress" = @mac AND "Status" = 0'
    [void]$poll.Parameters.AddWithValue("mac", $mac)
    $poll.Prepare()

    $hwCmd = $conn.CreateCommand()
    $hwCmd.CommandText = @'
INSERT INTO "HardwareMetrics" ("PCId","Timestamp","CpuUsage","MemoryUsage","DiskUsage","MemoryUsed","MemoryTotal","DiskUsed","DiskTotal")
VALUES (@pc, now() AT TIME ZONE 'UTC', @cpu, @mem, @disk, @memU, @memT, @diskU, @diskT)
'@
    [void]$hwCmd.Parameters.AddWithValue("pc",    $pcId)
    [void]$hwCmd.Parameters.AddWithValue("cpu",   0.0)
    [void]$hwCmd.Parameters.AddWithValue("mem",   0.0)
    [void]$hwCmd.Parameters.AddWithValue("disk",  0.0)
    [void]$hwCmd.Parameters.AddWithValue("memU",  [long]0)
    [void]$hwCmd.Parameters.AddWithValue("memT",  [long]17179869184)
    [void]$hwCmd.Parameters.AddWithValue("diskU", [long]0)
    [void]$hwCmd.Parameters.AddWithValue("diskT", [long]512000000000)
    $hwCmd.Prepare()

    $netCmd = $conn.CreateCommand()
    $netCmd.CommandText = @'
INSERT INTO "NetworkMetrics" ("PCId","Timestamp","DownloadSpeed","UploadSpeed","Latency","PacketLoss","IsConnected")
VALUES (@pc, now() AT TIME ZONE 'UTC', @dl, @ul, @lat, @loss, true)
'@
    [void]$netCmd.Parameters.AddWithValue("pc",   $pcId)
    [void]$netCmd.Parameters.AddWithValue("dl",   0.0)
    [void]$netCmd.Parameters.AddWithValue("ul",   0.0)
    [void]$netCmd.Parameters.AddWithValue("lat",  0.0)
    [void]$netCmd.Parameters.AddWithValue("loss", 0.0)
    $netCmd.Prepare()

    $latencies = New-Object System.Collections.Generic.List[double]
    $errors    = 0
    $heartbeats= 0
    $rng       = New-Object System.Random ($agentIndex + [int](Get-Date).Ticks)

    # Stagger startup so 80+ clients don't all fire at once.
    Start-Sleep -Milliseconds ($rng.Next(0, 5000))

    $sw   = [System.Diagnostics.Stopwatch]::StartNew()
    $tick = 0
    $deadline = (Get-Date).AddSeconds($duration)

    while ((Get-Date) -lt $deadline) {
        $tick++

        try {
            $hb.Parameters[0].Value = $mac
            $opSw = [System.Diagnostics.Stopwatch]::StartNew()
            [void]$hb.ExecuteNonQuery()
            $opSw.Stop()
            $latencies.Add([double]$opSw.Elapsed.TotalMilliseconds)
            $heartbeats++

            $reader = $poll.ExecuteReader()
            while ($reader.Read()) { } # drain
            $reader.Close()

            # Hardware/network insert every 6 ticks (~30 s).
            if (($tick % 6) -eq 0) {
                $hwCmd.Parameters["cpu"].Value   = [double]$rng.NextDouble() * 100.0
                $hwCmd.Parameters["mem"].Value   = [double]$rng.NextDouble() * 100.0
                $hwCmd.Parameters["disk"].Value  = [double]$rng.NextDouble() * 100.0
                $hwCmd.Parameters["memU"].Value  = [long]($rng.NextDouble() * 17179869184)
                $hwCmd.Parameters["diskU"].Value = [long]($rng.NextDouble() * 512000000000)
                [void]$hwCmd.ExecuteNonQuery()

                $netCmd.Parameters["dl"].Value   = [double]$rng.NextDouble() * 1000.0
                $netCmd.Parameters["ul"].Value   = [double]$rng.NextDouble() * 1000.0
                $netCmd.Parameters["lat"].Value  = [double]$rng.NextDouble() * 5.0
                $netCmd.Parameters["loss"].Value = [double]$rng.NextDouble() * 0.1
                [void]$netCmd.ExecuteNonQuery()
            }
        } catch {
            $errors++
            # Try to keep the connection healthy; reopen if it died.
            if ($conn.State -ne [System.Data.ConnectionState]::Open) {
                try { $conn.Open() } catch {}
            }
        }

        Start-Sleep -Seconds 5
    }

    $conn.Close()

    [pscustomobject]@{
        Mac        = $mac
        Heartbeats = $heartbeats
        Errors     = $errors
        Latencies  = $latencies.ToArray()
    }
}

# ---------------------------------------------------------------------------
# Aggregate.
# ---------------------------------------------------------------------------
$allLat = New-Object System.Collections.Generic.List[double]
$totalHb = 0
$totalErr = 0
$rows = New-Object System.Collections.Generic.List[object]

foreach ($r in $results) {
    $totalHb  += $r.Heartbeats
    $totalErr += $r.Errors
    foreach ($l in $r.Latencies) {
        $allLat.Add($l)
        $rows.Add([pscustomobject]@{ Mac = $r.Mac; LatencyMs = $l })
    }
}

$rows | Export-Csv -Path $OutputCsv -NoTypeInformation

function Get-Percentile([double[]]$values, [double]$p) {
    if ($values.Length -eq 0) { return 0 }
    $sorted = [double[]]$values | Sort-Object
    $idx = [int][Math]::Ceiling(($p / 100.0) * $sorted.Length) - 1
    if ($idx -lt 0) { $idx = 0 }
    if ($idx -ge $sorted.Length) { $idx = $sorted.Length - 1 }
    return $sorted[$idx]
}

$arr = $allLat.ToArray()
$elapsedSec = ((Get-Date) - $globalStart).TotalSeconds

Write-Host ""
Write-Host "==============================================================" -ForegroundColor Green
Write-Host " RESULT" -ForegroundColor Green
Write-Host "==============================================================" -ForegroundColor Green
"   Agents              : {0}" -f $AgentCount                            | Write-Host
"   Wall-clock duration : {0:N1} s" -f $elapsedSec                        | Write-Host
"   Heartbeats sent     : {0:N0}" -f $totalHb                             | Write-Host
"   Errors              : {0}" -f $totalErr                               | Write-Host
"   Heartbeat success % : {0:N3}" -f (100.0 * $totalHb / [Math]::Max(1, $totalHb + $totalErr)) | Write-Host
"   Latency 50p ms      : {0:N1}" -f (Get-Percentile $arr 50)             | Write-Host
"   Latency 95p ms      : {0:N1}" -f (Get-Percentile $arr 95)             | Write-Host
"   Latency 99p ms      : {0:N1}" -f (Get-Percentile $arr 99)             | Write-Host
"   Latency max ms      : {0:N1}" -f (($arr | Measure-Object -Maximum).Maximum) | Write-Host
"   CSV                 : {0}" -f $OutputCsv                              | Write-Host
Write-Host "==============================================================" -ForegroundColor Green
