<#
.SYNOPSIS
Collects the data needed to fill in IRIS system-requirements tables for the
capstone report. Run on each representative role of PC and consolidate the
output into the Installation Manual's hardware/software tables.

.DESCRIPTION
This script gathers OS, CPU, RAM, disk, display, network, .NET runtime, and
IRIS-component-specific data points (process memory, log sizes, listening
ports, install footprint) from the local machine.

It writes a human-readable summary to the console and saves a timestamped
JSON file alongside the script for inclusion in the project report appendix.

.PARAMETER Role
One of: UI, Agent, DB. Determines which IRIS-specific metrics are gathered.

.PARAMETER DbHost
(DB role only) PostgreSQL host to connect to. Default: localhost.

.PARAMETER DbPort
(DB role only) PostgreSQL port. Default: 5432.

.PARAMETER DbName
(DB role only) Database name. Default: iris_db.

.PARAMETER DbUser
(DB role only) PostgreSQL username. Default: postgres.

.PARAMETER DbPassword
(DB role only) PostgreSQL password.

.PARAMETER OutputDir
Directory to save the JSON report. Default: same directory as this script.

.EXAMPLE
.\Get-IrisRequirementsData.ps1 -Role UI

.EXAMPLE
.\Get-IrisRequirementsData.ps1 -Role Agent

.EXAMPLE
.\Get-IrisRequirementsData.ps1 -Role DB -DbHost localhost -DbUser postgres -DbPassword postgres

.NOTES
- Run as Administrator for full access (process WorkingSet, listening ports).
- For the DB role on a Linux host, run the equivalent psql commands manually
  (this script targets Windows but documents the queries it runs).
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('UI','Agent','DB')]
    [string]$Role,

    [string]$DbHost = 'localhost',
    [int]$DbPort = 5432,
    [string]$DbName = 'iris_db',
    [string]$DbUser = 'postgres',
    [string]$DbPassword = '',

    [string]$OutputDir = $PSScriptRoot
)

$ErrorActionPreference = 'Continue'
$report = [ordered]@{}
$report.Role = $Role
$report.HostName = $env:COMPUTERNAME
$report.UserName = $env:USERNAME
$report.CollectedAtUtc = (Get-Date).ToUniversalTime().ToString('o')

function Write-Section($title) {
    Write-Host ""
    Write-Host "=== $title ===" -ForegroundColor Cyan
}

# ---------- Operating System ----------
Write-Section 'Operating System'
try {
    $os = Get-CimInstance Win32_OperatingSystem
    $cv = Get-ItemProperty 'HKLM:\Software\Microsoft\Windows NT\CurrentVersion'
    $report.OS = [ordered]@{
        Caption        = $os.Caption
        Version        = $os.Version
        BuildNumber    = $os.BuildNumber
        DisplayVersion = $cv.DisplayVersion
        UBR            = $cv.UBR
        Architecture   = $os.OSArchitecture
        InstallDateUtc = ([Management.ManagementDateTimeConverter]::ToDateTime($os.InstallDate)).ToUniversalTime().ToString('o')
    }
    $report.OS | Format-Table -AutoSize | Out-Host
} catch {
    Write-Warning "OS query failed: $_"
}

# ---------- CPU ----------
Write-Section 'Processor'
try {
    $cpus = Get-CimInstance Win32_Processor
    $report.CPU = $cpus | ForEach-Object {
        [ordered]@{
            Name              = $_.Name
            Manufacturer      = $_.Manufacturer
            NumberOfCores     = $_.NumberOfCores
            LogicalProcessors = $_.NumberOfLogicalProcessors
            MaxClockSpeedMHz  = $_.MaxClockSpeed
            Architecture      = $_.Architecture
        }
    }
    $report.CPU | ForEach-Object { [pscustomobject]$_ } | Format-Table -AutoSize | Out-Host
} catch {
    Write-Warning "CPU query failed: $_"
}

# ---------- Memory ----------
Write-Section 'Memory (RAM)'
try {
    $totalKb = $os.TotalVisibleMemorySize
    $freeKb  = $os.FreePhysicalMemory
    $report.Memory = [ordered]@{
        TotalGB       = [math]::Round($totalKb / 1MB, 2)
        FreeGB        = [math]::Round($freeKb / 1MB, 2)
        UsedGB        = [math]::Round(($totalKb - $freeKb) / 1MB, 2)
        UsedPercent   = [math]::Round((($totalKb - $freeKb) / $totalKb) * 100, 1)
    }
    $report.Memory | Format-Table -AutoSize | Out-Host

    # Per-stick info
    $sticks = Get-CimInstance Win32_PhysicalMemory
    $report.MemoryModules = $sticks | ForEach-Object {
        [ordered]@{
            CapacityGB = [math]::Round($_.Capacity / 1GB, 1)
            SpeedMHz   = $_.Speed
            Manufacturer = $_.Manufacturer
            PartNumber = ($_.PartNumber -replace '\s+',' ').Trim()
        }
    }
} catch {
    Write-Warning "Memory query failed: $_"
}

# ---------- Disk ----------
Write-Section 'Disk Space'
try {
    $disks = Get-CimInstance Win32_LogicalDisk -Filter 'DriveType=3'
    $report.Disks = $disks | ForEach-Object {
        [ordered]@{
            DeviceID    = $_.DeviceID
            FileSystem  = $_.FileSystem
            TotalGB     = [math]::Round($_.Size / 1GB, 1)
            FreeGB      = [math]::Round($_.FreeSpace / 1GB, 1)
            UsedPercent = if ($_.Size -gt 0) { [math]::Round((($_.Size - $_.FreeSpace) / $_.Size) * 100, 1) } else { 0 }
        }
    }
    $report.Disks | ForEach-Object { [pscustomobject]$_ } | Format-Table -AutoSize | Out-Host
} catch {
    Write-Warning "Disk query failed: $_"
}

# ---------- Display ----------
Write-Section 'Display'
try {
    $videos = Get-CimInstance Win32_VideoController
    $report.Display = $videos | ForEach-Object {
        [ordered]@{
            Name                      = $_.Name
            DriverVersion             = $_.DriverVersion
            CurrentHorizontalResolution = $_.CurrentHorizontalResolution
            CurrentVerticalResolution = $_.CurrentVerticalResolution
            CurrentRefreshRate        = $_.CurrentRefreshRate
            VideoModeDescription      = $_.VideoModeDescription
        }
    }
    $report.Display | ForEach-Object { [pscustomobject]$_ } | Format-Table -AutoSize | Out-Host
} catch {
    Write-Warning "Display query failed: $_"
}

# ---------- Network ----------
Write-Section 'Network Adapters'
try {
    $nics = Get-NetAdapter | Where-Object Status -eq 'Up'
    $report.NetworkAdapters = $nics | ForEach-Object {
        [ordered]@{
            Name            = $_.Name
            InterfaceDesc   = $_.InterfaceDescription
            LinkSpeed       = $_.LinkSpeed
            MacAddress      = $_.MacAddress
            MediaType       = $_.MediaType
        }
    }
    $report.NetworkAdapters | ForEach-Object { [pscustomobject]$_ } | Format-Table -AutoSize | Out-Host

    $ipconfigs = Get-NetIPConfiguration | Where-Object IPv4Address
    $report.IPConfiguration = $ipconfigs | ForEach-Object {
        [ordered]@{
            InterfaceAlias = $_.InterfaceAlias
            IPv4Address    = ($_.IPv4Address.IPAddress -join ',')
            PrefixLength   = ($_.IPv4Address.PrefixLength -join ',')
            DefaultGateway = ($_.IPv4DefaultGateway.NextHop -join ',')
            DnsServer      = ($_.DNSServer.ServerAddresses -join ',')
        }
    }
} catch {
    Write-Warning "Network query failed: $_"
}

# ---------- .NET runtimes ----------
Write-Section '.NET Runtimes Available'
try {
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($dotnet) {
        $runtimes = & dotnet --list-runtimes 2>$null
        $sdks     = & dotnet --list-sdks 2>$null
        $report.DotNet = [ordered]@{
            Version  = (& dotnet --version 2>$null)
            Runtimes = @($runtimes)
            SDKs     = @($sdks)
        }
        Write-Host ($runtimes -join "`n")
    } else {
        $report.DotNet = [ordered]@{
            Version  = $null
            Runtimes = @()
            SDKs     = @()
            Note     = 'dotnet CLI not on PATH; IRIS MSIs are self-contained so this is fine on lab PCs.'
        }
        Write-Host 'dotnet CLI not on PATH.' -ForegroundColor Yellow
    }
} catch {
    Write-Warning ".NET query failed: $_"
}

# ---------- Role-specific ----------

if ($Role -eq 'UI') {
    Write-Section 'IRIS UI — Process & Footprint'
    try {
        $uiProc = Get-Process IRIS.UI -ErrorAction SilentlyContinue
        if ($uiProc) {
            $report.IrisUI = $uiProc | ForEach-Object {
                [ordered]@{
                    Id                = $_.Id
                    StartTime         = $_.StartTime.ToString('o')
                    UptimeMinutes     = [math]::Round(((Get-Date) - $_.StartTime).TotalMinutes, 1)
                    SessionId         = $_.SessionId
                    WorkingSetMB      = [math]::Round($_.WorkingSet64 / 1MB, 1)
                    PrivateMemoryMB   = [math]::Round($_.PrivateMemorySize64 / 1MB, 1)
                    Path              = $_.Path
                }
            }
            $report.IrisUI | ForEach-Object { [pscustomobject]$_ } | Format-Table -AutoSize | Out-Host
        } else {
            Write-Host 'IRIS.UI is not currently running.' -ForegroundColor Yellow
            $report.IrisUI = @()
        }

        $uiInstall = 'C:\Program Files\IRIS\UI'
        if (Test-Path $uiInstall) {
            $size = (Get-ChildItem $uiInstall -Recurse -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
            $exe  = Get-Item (Join-Path $uiInstall 'IRIS.UI.exe') -ErrorAction SilentlyContinue
            $report.IrisUIInstall = [ordered]@{
                Path           = $uiInstall
                FootprintMB    = [math]::Round($size / 1MB, 1)
                ExeLastWriteUtc = if ($exe) { $exe.LastWriteTimeUtc.ToString('o') } else { $null }
                FileVersion    = if ($exe) { $exe.VersionInfo.FileVersion } else { $null }
            }
            $report.IrisUIInstall | Format-Table -AutoSize | Out-Host
        }

        $logPath = Join-Path $uiInstall 'snapshot-debug.log'
        if (Test-Path $logPath) {
            $log = Get-Item $logPath
            $report.SnapshotDebugLog = [ordered]@{
                Path        = $log.FullName
                SizeKB      = [math]::Round($log.Length / 1KB, 1)
                LastWriteUtc = $log.LastWriteTimeUtc.ToString('o')
            }
            $report.SnapshotDebugLog | Format-Table -AutoSize | Out-Host
        }
    } catch {
        Write-Warning "UI metrics failed: $_"
    }
}

if ($Role -eq 'Agent') {
    Write-Section 'IRIS Agent — Processes'
    try {
        $agentProcs = Get-Process IRIS.Agent -ErrorAction SilentlyContinue
        if ($agentProcs) {
            $report.IrisAgent = $agentProcs | ForEach-Object {
                [ordered]@{
                    Id                = $_.Id
                    StartTime         = $_.StartTime.ToString('o')
                    UptimeMinutes     = [math]::Round(((Get-Date) - $_.StartTime).TotalMinutes, 1)
                    SessionId         = $_.SessionId
                    WorkingSetMB      = [math]::Round($_.WorkingSet64 / 1MB, 1)
                    PrivateMemoryMB   = [math]::Round($_.PrivateMemorySize64 / 1MB, 1)
                    Path              = $_.Path
                }
            }
            $report.IrisAgent | ForEach-Object { [pscustomobject]$_ } | Format-Table -AutoSize | Out-Host
        } else {
            Write-Host 'IRIS.Agent is not currently running.' -ForegroundColor Yellow
            $report.IrisAgent = @()
        }

        $agentInstall = 'C:\Program Files\IRIS\Agent'
        if (Test-Path $agentInstall) {
            $size = (Get-ChildItem $agentInstall -Recurse -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
            $exe  = Get-Item (Join-Path $agentInstall 'IRIS.Agent.exe') -ErrorAction SilentlyContinue
            $report.IrisAgentInstall = [ordered]@{
                Path            = $agentInstall
                FootprintMB     = [math]::Round($size / 1MB, 1)
                ExeLastWriteUtc = if ($exe) { $exe.LastWriteTimeUtc.ToString('o') } else { $null }
                FileVersion     = if ($exe) { $exe.VersionInfo.FileVersion } else { $null }
            }
            $report.IrisAgentInstall | Format-Table -AutoSize | Out-Host
        }

        Write-Section 'Agent Logs'
        $logDir = 'C:\ProgramData\IRIS\Agent'
        if (Test-Path $logDir) {
            $logs = Get-ChildItem $logDir -File -ErrorAction SilentlyContinue
            $totalSize = ($logs | Measure-Object Length -Sum).Sum
            $report.AgentLogs = [ordered]@{
                Directory      = $logDir
                FileCount      = $logs.Count
                TotalSizeMB    = [math]::Round($totalSize / 1MB, 2)
                NewestFile     = ($logs | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1).Name
                OldestFile     = ($logs | Sort-Object LastWriteTimeUtc | Select-Object -First 1).Name
                RetentionDays  = [math]::Round(((Get-Date) - ($logs | Sort-Object LastWriteTimeUtc | Select-Object -First 1).LastWriteTimeUtc).TotalDays, 1)
            }
            $report.AgentLogs | Format-Table -AutoSize | Out-Host
        }

        Write-Section 'Listening Ports (Agent)'
        $ports = @(5057, 5065)
        $report.ListeningPorts = foreach ($p in $ports) {
            $tcp = Get-NetTCPConnection -LocalPort $p -State Listen -ErrorAction SilentlyContinue
            if ($tcp) {
                [ordered]@{
                    Port        = $p
                    State       = 'LISTENING'
                    OwnerPid    = $tcp[0].OwningProcess
                    ProcessName = (Get-Process -Id $tcp[0].OwningProcess -ErrorAction SilentlyContinue).ProcessName
                }
            } else {
                [ordered]@{ Port = $p; State = 'NOT LISTENING'; OwnerPid = $null; ProcessName = $null }
            }
        }
        $report.ListeningPorts | ForEach-Object { [pscustomobject]$_ } | Format-Table -AutoSize | Out-Host

        Write-Section 'Firewall Rules (IRIS Agent)'
        $rules = Get-NetFirewallRule -DisplayName "IRIS Agent*" -ErrorAction SilentlyContinue
        $report.FirewallRules = $rules | ForEach-Object {
            [ordered]@{
                DisplayName = $_.DisplayName
                Enabled     = $_.Enabled
                Direction   = $_.Direction
                Action      = $_.Action
                Profile     = $_.Profile
            }
        }
        $report.FirewallRules | ForEach-Object { [pscustomobject]$_ } | Format-Table -AutoSize | Out-Host
    } catch {
        Write-Warning "Agent metrics failed: $_"
    }
}

if ($Role -eq 'DB') {
    Write-Section 'PostgreSQL Connectivity'
    $tnc = Test-NetConnection -ComputerName $DbHost -Port $DbPort -WarningAction SilentlyContinue
    $report.DbConnectivity = [ordered]@{
        Host           = $DbHost
        Port           = $DbPort
        TcpReachable   = $tnc.TcpTestSucceeded
        SourceAddress  = $tnc.SourceAddress.IPAddress
    }
    $report.DbConnectivity | Format-Table -AutoSize | Out-Host

    Write-Section 'PostgreSQL Server Stats'
    $psql = Get-Command psql -ErrorAction SilentlyContinue
    if (-not $psql) {
        Write-Host 'psql not found on PATH. Install PostgreSQL client tools, or run these queries manually on the DB host:' -ForegroundColor Yellow
        Write-Host '  SHOW server_version;'
        Write-Host '  SHOW shared_buffers;'
        Write-Host "  SELECT pg_size_pretty(pg_database_size('$DbName'));"
        Write-Host "  SELECT relname, pg_size_pretty(pg_total_relation_size(C.oid)) FROM pg_class C JOIN pg_namespace N ON N.oid = C.relnamespace WHERE C.relkind='r' AND N.nspname='public' ORDER BY pg_total_relation_size(C.oid) DESC LIMIT 10;"
        $report.PostgresStats = [ordered]@{
            Available = $false
            Note      = 'psql not available; queries listed in console output for manual execution.'
        }
    } else {
        $env:PGPASSWORD = $DbPassword
        try {
            function Invoke-Psql([string]$query) {
                & psql -h $DbHost -p $DbPort -U $DbUser -d $DbName -t -A -c $query 2>&1
            }
            $version       = Invoke-Psql 'SHOW server_version;'
            $sharedBuffers = Invoke-Psql 'SHOW shared_buffers;'
            $maxConn       = Invoke-Psql 'SHOW max_connections;'
            $dbSize        = Invoke-Psql "SELECT pg_size_pretty(pg_database_size('$DbName'));"
            $tableTop = Invoke-Psql @"
SELECT relname || '|' || pg_size_pretty(pg_total_relation_size(C.oid))
FROM pg_class C JOIN pg_namespace N ON N.oid = C.relnamespace
WHERE C.relkind='r' AND N.nspname='public'
ORDER BY pg_total_relation_size(C.oid) DESC LIMIT 10;
"@

            $report.PostgresStats = [ordered]@{
                ServerVersion  = ($version | Select-Object -First 1).Trim()
                SharedBuffers  = ($sharedBuffers | Select-Object -First 1).Trim()
                MaxConnections = ($maxConn | Select-Object -First 1).Trim()
                DatabaseSize   = ($dbSize | Select-Object -First 1).Trim()
                TopTables      = $tableTop | Where-Object { $_ -match '\|' } | ForEach-Object {
                    $parts = $_ -split '\|'
                    [ordered]@{ Table = $parts[0]; Size = $parts[1] }
                }
            }
            $report.PostgresStats | Format-Table -AutoSize | Out-Host
            $report.PostgresStats.TopTables | ForEach-Object { [pscustomobject]$_ } | Format-Table -AutoSize | Out-Host
        } catch {
            Write-Warning "psql query failed: $_"
        } finally {
            Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
        }
    }
}

# ---------- Save report ----------
Write-Section 'Saving Report'
$timestamp = (Get-Date).ToString('yyyyMMdd-HHmmss')
$outFile = Join-Path $OutputDir "iris-requirements-$($Role.ToLower())-$($env:COMPUTERNAME)-$timestamp.json"
$report | ConvertTo-Json -Depth 8 | Out-File -FilePath $outFile -Encoding UTF8

Write-Host ""
Write-Host "Report saved to:" -ForegroundColor Green
Write-Host "  $outFile"
Write-Host ""
Write-Host "Attach this JSON file to the project report appendix. Combine outputs"
Write-Host "from one UI host, one Agent host, and the DB host to populate the"
Write-Host "Operator Workstation, Lab PC, and Database Server requirement tables."
