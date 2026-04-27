# Requirements — Data Collection Plan

This document tells you exactly what to measure or read in order to fill
each row of the System Requirements tables in the
[Installation Manual](INSTALLATION_MANUAL.md). Use this as a checklist:
before defense, walk down each row and replace any "estimated" value with
a measured one. Every row maps to a concrete file, command, or test.

The output of running this checklist is a defensible requirements section
where every cell can be backed up with "we measured this on date X with
command Y".

---

## How to use

For each row in each table:

1. Run the **Source/Command** column.
2. Record the result with a timestamp in your defense notes.
3. Set the **Minimum** column to a number that, in your testing, the system
   actually requires.
4. Set the **Recommended** column to the value at which the system was tested
   stable for the duration listed in `STRESS_TESTING.md`.

If a row says "Estimate (project scope)", that means it's a deployment-target
decision, not a measurement. Document the deployment-target decision in the
report's introduction.

---

## Table 1 — Operator Workstation (runs IRIS UI)

| Row | Data point you need | Source / Command | Acceptance threshold |
|---|---|---|---|
| Operating System | Windows version + build | On a working test PC: `[Environment]::OSVersion.Version`, `(Get-CimInstance Win32_OperatingSystem).BuildNumber`, `Get-ItemProperty 'HKLM:\Software\Microsoft\Windows NT\CurrentVersion' \| Select DisplayVersion, UBR` | Lowest OS build you've successfully tested IRIS.UI on |
| .NET runtime | What the project targets | Read `IRIS.UI/IRIS.UI.csproj` `<TargetFramework>` (currently `net9.0-windows`) | The TFM string itself; if MSI is self-contained, target PC needs nothing |
| Processor | Min CPU IRIS.UI is usable on | Run UI on the slowest dev PC available, navigate Monitor view with 16 visible PCs polling, observe responsiveness. `Get-CimInstance Win32_Processor \| Select Name, NumberOfCores, MaxClockSpeed` to record the model | UI tile updates appear within 1 polling cycle, no visible lag |
| RAM | Steady-state working set | Run UI for ≥30 minutes on Monitor view. `Get-Process IRIS.UI \| Select WorkingSet64, PrivateMemorySize64` repeatedly. Take 95th percentile | Recommend 2× steady-state, minimum = steady-state + Windows baseline (2 GB) |
| Disk space | UI install footprint + log/cache growth | After install: `Get-ChildItem 'C:\Program Files\IRIS\UI' -Recurse \| Measure Length -Sum`. Plus run UI for 24h and check `<UI install dir>\snapshot-debug.log` size | Report installed footprint + 24h log delta, multiply ×7 for log retention |
| Display | Smallest resolution UI renders cleanly on | Set the operator PC to 1366×768, then 1920×1080. Walk through Monitor / Usage Metrics / Alerts views. Note any tiles that get clipped or buttons that overflow | Lowest resolution where every page renders without horizontal scrolling |
| Network speed | Sustained snapshot bandwidth from UI's perspective | Open Monitor view with N visible PCs. Measure with `Get-NetAdapter \| Get-NetAdapterStatistics` before and after a 5-minute window; divide bytes/sec | Recommend 10× observed sustained throughput |
| Privileges | Whether normal user can run after install | Install via MSI as admin. Log out, log in as a non-admin user, launch IRIS UI. If it loads and connects to DB, runtime privileges = standard user | "Local admin only at install time" if the test passes |

---

## Table 2 — Lab PC (runs IRIS Agent)

| Row | Data point you need | Source / Command | Acceptance threshold |
|---|---|---|---|
| Operating System | Lowest Windows build you've shipped to | Read `IRIS.Agent/IRIS.Agent.csproj` `<SupportedOSPlatformVersion>` (currently `10.0.19041.0` = Windows 10 May 2020 Update). Verify on your oldest test lab PC | Whichever is *higher*: the csproj minimum, or the oldest OS in your actual lab fleet |
| Processor | Agent CPU usage at idle and at peak | On a test lab PC, `Get-Counter '\Process(IRIS.Agent*)\% Processor Time' -Continuous -SampleInterval 5` for 1 hour. Capture mean and p95 | Recommend a CPU where p95 stays under 10% |
| RAM | Both agent processes' resident set | `Get-Process IRIS.Agent \| Where-Object SessionId -eq 0`, same for SessionId -ne 0. Sum WorkingSet64 over 8h soak | Minimum = 2 GB (Windows baseline) + measured agent total + 1 GB headroom for actual user activity (browser, classroom apps) |
| Disk space | Installed agent + 7 days rolling logs | `Get-Item "C:\Program Files\IRIS\Agent\IRIS.Agent.exe"` size + `Get-ChildItem C:\ProgramData\IRIS\Agent\ \| Measure Length -Sum` after 7 days of runtime | Add a 50% safety margin for log spikes during incidents |
| .NET runtime | Confirm self-contained MSI works on a clean PC | On a freshly imaged Windows install with no .NET runtime, install the agent MSI; verify it runs | "Bundled, none required" only after this test passes |
| Network speed | Outbound to DB + inbound snapshot | Same `Get-NetAdapterStatistics` method as the UI table, but on a lab PC during peak operator polling | Recommend 100× observed mean usage (very low utilization is realistic in a LAN) |
| Privileges | Agent works with no elevated processes after install | Confirm the user-mode agent (`--background`) runs in the console session under the logged-in user, not SYSTEM. `Get-Process IRIS.Agent \| Select SessionId, UserName` | "Standard user at runtime, admin only at install" if this passes |

---

## Table 3 — Database Server (runs PostgreSQL)

| Row | Data point you need | Source / Command | Acceptance threshold |
|---|---|---|---|
| Operating System | What you actually deployed PostgreSQL on | Pick one supported OS (Ubuntu Server LTS or Windows Server) and document the exact version you tested with | The OS your defense demo runs on |
| Processor | DB CPU during peak heartbeat load | On the DB host during a stress test: `top -bn1 \| grep postgres` (Linux) or Task Manager (Windows). Record p95 CPU | Recommend a CPU where p95 stays under 30% during 80-PC heartbeat |
| RAM | PostgreSQL `shared_buffers` + working memory | `SHOW shared_buffers;` in psql, plus `SELECT pg_size_pretty(pg_total_relation_size('"PCs"'));` to size the largest table | Minimum = `shared_buffers` × 2 + OS baseline. Recommended = 25% of total dataset size + 4 GB |
| Disk space | Current DB size + projected growth | `SELECT pg_size_pretty(pg_database_size('iris_db'));` taken now, then 24h later. Multiply daily delta × academic year days | Reserve 3× projected for backups + WAL + ad hoc workspace |
| PostgreSQL version | EF/Npgsql version compatibility | Read `IRIS.Core/IRIS.Core.csproj` `Npgsql.EntityFrameworkCore.PostgreSQL` package version. Cross-reference Npgsql release notes for supported PG versions | Lowest PG version where Npgsql package supports it — ours is 9.0.4, supports PG 14+ |
| Network speed | Heartbeat write throughput at scale | Run `IRIS.LoadTest` per `STRESS_TESTING.md` §3 with 240 simulated agents. Observe `pg_stat_statements` mean exec time of UPDATE PCs | Recommended bandwidth = observed throughput × 5 |
| Listener port | Confirm reachable from lab subnet | `Test-NetConnection -ComputerName <db-host-ip> -Port 5432` from a lab PC and from the operator PC | Both must return `TcpTestSucceeded : True` |

---

## Table 4 — Network (ports / subnet)

| Row | Data point you need | Source / Command | Acceptance threshold |
|---|---|---|---|
| Agent → DB port | Confirm 5432 is the actual port used | Read `IRIS.Agent/appsettings.json` connection string `Port=` value | Match what the table claims |
| UI → DB port | Same | Read `IRIS.UI/appsettings.json` connection string | Match |
| UI → Agent snapshot port | Confirm 5057 is what's bound | On a lab PC: `netstat -ano \| findstr :5057` should show LISTENING; cross-reference `IRIS.Agent/appsettings.json` `ScreenStreamPort` | Both must agree |
| UI → Agent file API port | Same for 5065 | `netstat -ano \| findstr :5065`; `IRIS.Agent/appsettings.json` `FileApiPort` | Match |
| UI → Agent RDP port | Confirm Microsoft RDP default | `IRIS.Agent/appsettings.json` `RemoteDesktopPort` (3389). Verify with `Get-NetTCPConnection -LocalPort 3389 -State Listen` | Must show LISTENING after `EnableRemoteDesktopSetup` runs |
| Lab subnet CIDR | What range the agents and DB share | `ipconfig /all` on a lab PC, read IP and subnet mask | Single contiguous CIDR; document it as `192.168.5.0/21` if matching |
| Bandwidth (recommended) | Per-PC peak snapshot bytes/sec | Tail agent log on a lab PC during operator polling: lines like `Snapshot req ... → 200 <N> bytes`; sum N over 60 seconds | Recommend 10× peak bytes/sec |
| Latency (max acceptable) | UI poll round-trip time | On UI host: `curl.exe -o nul -w "%{time_total}\n" -H "X-IRIS-Snapshot-Token: ..." http://<lab-pc>:5057/snapshot` 100 times; take p95 | Document this as the typical end-to-end latency |

---

## Table 5 — Capacity / scale numbers

| Row | Data point you need | Source / Command | Acceptance threshold |
|---|---|---|---|
| Target PC count | Project scope | The capstone proposal / endorsement document from USC ACC | The number stated in your project charter |
| Tested-to PC count | Result of stress test | `STRESS_TESTING.md` §3.4 outcome | Highest N where 1-hour run had zero heartbeat failures |
| Number of rooms | Project scope | Same as above | 4 (USC ACC labs) |
| Snapshot poll cadence | What the UI actually does | Read `IRIS.UI/ViewModels/MonitorViewModel.cs` snapshot timer interval | Document this verbatim |
| Heartbeat cadence | What the agent actually does | Read `IRIS.Agent/appsettings.json` `HeartbeatIntervalSeconds` (and confirm via log: time between consecutive `Heartbeat sent for PC` lines) | Match config value |
| Metrics cadence | Same for hardware/network metrics | `IRIS.Agent/appsettings.json` `MetricsIntervalSeconds` | Match |

---

## Defense-day evidence packet

Before defense, produce a one-page printout titled **"How we measured each
requirement"** containing:

1. **One screenshot per measurement** — `Get-Process`, `pg_database_size`,
   `netstat`, `Get-NetAdapterStatistics`. Stamp with date and which PC ran it.
2. **A side-by-side table**: "Manual claim" vs "Measured value" for every row
   in every requirements table.
3. **The csproj excerpt** showing `<SupportedOSPlatformVersion>` and the
   Npgsql package version, so you can point at the actual source line that
   determines the OS / DB minimum.
4. **A copy of `appsettings.json`** with the four port numbers highlighted,
   so any "why these ports?" question has an immediate textual answer.

This packet is what separates "we estimated" from "we measured" — the panel
will reward the latter.

---

## Quick checklist

- [ ] Run `Get-Process IRIS.Agent` on a lab PC after 1+ hour uptime; record memory.
- [ ] Run `Get-Process IRIS.UI` on operator PC after 1+ hour uptime; record memory.
- [ ] Run `pg_database_size` 24 hours apart; record daily delta.
- [ ] Run `netstat -ano | findstr :5057` on a lab PC; record PID is `4` (http.sys-style) or the agent process — either is fine, just document.
- [ ] Run `Test-NetConnection` from operator host to one lab PC on port 5057; record the latency.
- [ ] Read `IRIS.Agent/IRIS.Agent.csproj` and copy the `<TargetFramework>` and `<SupportedOSPlatformVersion>` lines into the appendix.
- [ ] Read both `appsettings.json` files and copy the port values into the network requirements table.
- [ ] Walk the UI on a 1366×768 display; confirm no horizontal scroll on each tab.
- [ ] Run the stress test from `STRESS_TESTING.md` §3 at 80 simulated agents for 1 hour; record max heartbeat 95p latency and DB CPU.

When every box is checked, every cell in every requirements table has a
measured value behind it.
