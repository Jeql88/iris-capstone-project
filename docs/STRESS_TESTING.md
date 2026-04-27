# IRIS Stress-Testing Guide

This guide walks through how to stress-test IRIS as a capstone deliverable. The
goal is not to replicate a Microsoft-style load-test rig but to **demonstrate
that the system holds up under realistic worst-case conditions for a
University of San Carlos ACC computer-laboratory deployment** — 80 lab PCs,
4 rooms, one operator dashboard, sustained for hours.

This is what to write up, what to measure, and how to interpret the numbers.

---

## 1. What you are actually testing

Three independent capacity dimensions, each with its own ceiling:

| # | System | Capacity dimension | Failure mode if exceeded |
|---|---|---|---|
| 1 | PostgreSQL | concurrent agent connections, write throughput | Connection pool exhausted; heartbeat queries time out; UI sees PCs go offline |
| 2 | Operator UI | concurrent snapshot polls, DataGrid binding load | UI freezes; CPU pegged; snapshots stall |
| 3 | Per-agent serving | snapshot HTTP throughput per PC | Snapshots time out from operator's perspective |

Each gets its own test. Don't bundle them — when something fails you want to
know which subsystem broke.

---

## 2. Environment baselines

Record these numbers before any test starts. They are the "known good" you
compare your stressed runs against.

```
PostgreSQL host:        IP, CPU model, RAM, disk type
Agent PC sample:        CPU model, RAM, OS build, NIC link speed
Operator PC:            CPU model, RAM, OS build, NIC link speed
Network:                LAN switch model, port speed (1G/10G), VLAN
IRIS versions:          Agent MSI version, UI MSI version
                        (same version on every PC for a clean run)
Database state:         Row counts in PCs, HardwareMetrics, NetworkMetrics,
                        Alerts, SoftwareUsageHistory at start of test
```

Take a `dotnet ef migrations script` snapshot of the schema on the day of the
test so the report is reproducible.

---

## 3. Test 1 — Database / heartbeat scaling

**Hypothesis:** the system can sustain 80 agents heartbeating every 5 seconds,
plus full hardware/network metrics every 30 s, plus periodic alert evaluation,
without query timeouts or connection-pool exhaustion.

### 3.1 What "stress" means here

The realistic max load on the DB from agent-side traffic is:

- 80 × heartbeat `UPDATE PCs SET LastSeen=...` every 5 s = 16 writes/sec
- 80 × hardware metric INSERT every 30 s = ~2.7 inserts/sec
- 80 × network metric INSERT every 30 s = ~2.7 inserts/sec
- 80 × command poll `SELECT … PendingCommands` every 5 s = 16 reads/sec
- ~5 dashboard refreshes/sec from one operator (cached)

Total ~40 ops/sec at steady state, peaks ~60. PostgreSQL handles this
trivially on adequate hardware, but the test demonstrates it.

### 3.2 How to run it without 80 physical PCs

You won't have 80 PCs sitting around for testing. Use a **synthetic agent
loader** — a small console program that opens N parallel connections, each
emulating one agent's heartbeat + metrics cadence, with a unique fake MAC.

Add a project `IRIS.LoadTest` (one-off, not shipped) that does this:

```csharp
// Pseudocode — single console app, N tasks in parallel
var simulatedAgents = Enumerable.Range(0, count)
    .Select(i => new SimulatedAgent(
        macAddress: $"AA:BB:CC:{i:X2}:{(i>>8):X2}:{(i>>16):X2}",
        hostname: $"LOAD_PC_{i:000}",
        connectionString: connStr));

await Task.WhenAll(simulatedAgents.Select(a => a.RunAsync(durationSeconds)));

// Each SimulatedAgent.RunAsync():
//   while not cancelled:
//     UPDATE PCs SET LastSeen = now() WHERE MacAddress = @mac
//     every 30s: INSERT into HardwareMetrics + NetworkMetrics
//     every 5s:  SELECT FROM PendingCommands WHERE MacAddress = @mac
//     sleep 5s
```

Run with N = 80, 160, 240, 320 (1×, 2×, 3×, 4× target). For each:

- Run for 60 minutes minimum.
- Capture the metrics in section 3.3.
- Stop at the first run that fails the success criteria.

### 3.3 What to measure

| Metric | How to capture | Target |
|---|---|---|
| Heartbeat success rate | Count UPDATEs that completed vs ones that errored or timed out | ≥ 99.9% |
| 95p heartbeat latency | Stopwatch around the UPDATE; report 50p / 95p / 99p / max | 95p < 200 ms |
| DB connection-pool wait time | Postgres `pg_stat_activity` queries blocked on `Lock` state | Should never appear |
| PostgreSQL CPU | `top` or Task Manager on DB host | < 50% sustained |
| Postgres `pg_stat_statements` | Top 10 by total time | None should dominate |

```sql
-- Run during the load test for snapshot of activity
SELECT state, COUNT(*) FROM pg_stat_activity GROUP BY state;
SELECT pid, query_start, state, query
  FROM pg_stat_activity WHERE state = 'active' ORDER BY query_start;

-- Top queries by accumulated time (requires pg_stat_statements extension)
SELECT calls, total_exec_time, mean_exec_time, query
  FROM pg_stat_statements
  ORDER BY total_exec_time DESC
  LIMIT 10;
```

### 3.4 Pass criteria

- 80 agents, 1 hour: zero heartbeat failures, 95p < 200 ms, DB CPU < 30% sustained.
- 240 agents (3× target), 1 hour: zero heartbeat failures (your target deployment is 80 — 3× headroom is generous).

If 240 fails, document where it fails and what fixed it (raising
`Npgsql` `Maximum Pool Size`, adding an index, etc.). That's a strong
section in your capstone report.

---

## 4. Test 2 — Operator UI (snapshot polling under load)

**Hypothesis:** one operator dashboard can poll 80 PCs for snapshots
continuously (default cadence) without UI freezing or memory growth.

### 4.1 How to run

Run real agents on test PCs (or virtualized — Hyper-V / VMware / VirtualBox
clones each running the agent against a separate fake MAC). 16–24 actual
agents is enough; the UI's poll behavior at 80 is an arithmetic extrapolation
of 16.

If you can only get 16 real agents, declare that and explain why your
extrapolation holds (the per-PC TCP request is ~50 KB and ~100 ms; 80 of
those run inside the existing `SemaphoreSlim(8)` so the upper bound is
fully bounded by the semaphore).

Steady-state run:

- Open Monitor view.
- Leave it on the dashboard for **8 hours overnight**.
- Capture screenshots every hour to verify tiles still update.

### 4.2 What to measure

| Metric | How to capture | Target |
|---|---|---|
| UI process Working Set | Task Manager → IRIS.UI memory column over 8h | Should plateau, not climb monotonically |
| UI process CPU | Task Manager average over 1-min windows | < 15% sustained |
| Snapshot success rate from UI's perspective | Tail `snapshot-debug.log` and grep for `OK,` vs `EXCEPTION:` | ≥ 99% |
| Tile-refresh latency | Observe with stopwatch: change something visible on a lab PC, time until tile updates | < 6 s 95p |
| Frame drops / thread starvation | Watch tile snapshot animation; freezes are a fail | None |

### 4.3 Pass criteria

- UI memory stable (< 10% drift over 8 h after a 30-minute warm-up).
- No `EXCEPTION:` lines in `snapshot-debug.log` other than expected client
  cancellations.
- Operator can navigate between Monitor / Usage Metrics / Alerts pages with
  no perceptible lag.

### 4.4 Memory-leak smoke test

Easiest way to spot a leak: take a `Process.PrivateMemorySize64` reading
every 30 minutes via PowerShell and chart it.

```powershell
while ($true) {
  $p = Get-Process IRIS.UI
  "{0:yyyy-MM-dd HH:mm:ss},{1},{2}" -f (Get-Date), $p.Id, $p.PrivateMemorySize64 |
    Out-File -Append C:\temp\ui-memory.csv
  Start-Sleep -Seconds 1800
}
```

A monotonically rising line = leak. A noisy line that wanders within ±15%
of a stable mean = healthy.

---

## 5. Test 3 — Per-agent snapshot throughput

**Hypothesis:** one agent can serve `/snapshot` continuously to one operator
without dropping requests, even on Windows 11 24H2 PCs (which had the
http.sys / dual-stack regressions documented in the snapshot remediation
plan).

### 5.1 How to run

On the operator PC, against one target agent's IP:

```powershell
$token = "<screen stream token from appsettings>"
$results = @()
1..1000 | ForEach-Object {
  $sw = [System.Diagnostics.Stopwatch]::StartNew()
  $exit = (curl.exe -s -o "C:\temp\stress\snap_$_.jpg" `
    -H "X-IRIS-Snapshot-Token: $token" `
    "http://<agent-ip>:5057/snapshot")
  $sw.Stop()
  $size = (Get-Item "C:\temp\stress\snap_$_.jpg" -ErrorAction SilentlyContinue).Length
  $results += [pscustomobject]@{ N = $_; Ms = $sw.ElapsedMilliseconds; Bytes = $size }
}
$results | Export-Csv -Path C:\temp\stress\one-agent-1k.csv -NoTypeInformation
```

That's 1000 sequential snapshots from one agent. Then plot the latency
distribution and verify:

| Metric | Target |
|---|---|
| 95p latency | < 500 ms |
| Failures (Bytes = null or 0) | 0 |
| Mean JPEG size | 30–80 KB (depends on display content) |

### 5.2 Concurrency test

Same script with 8 parallel runners against the same agent (simulating multiple
UI views polling the same PC). The agent should still serve all of them.

```powershell
1..8 | ForEach-Object -ThrottleLimit 8 -Parallel {
  # ... same body as above with $_ as the runner index
}
```

Pass criterion: every runner gets ≥ 99% success and 95p latency < 1 s.

### 5.3 Burst test (regression test for the 24H2 fixes)

The bug we fought in late April reproduced under high request rate from one
client. Re-create that condition explicitly:

```powershell
# Hammer with 200 concurrent connections briefly
1..200 | ForEach-Object -ThrottleLimit 200 -Parallel {
  curl.exe -s -o nul -H "X-IRIS-Snapshot-Token: $using:token" `
    "http://<agent-ip>:5057/snapshot" 2>&1
}
```

Pass criterion: no `forcibly closed`, `connection reset`, or 21-second timeout
errors. Tail `C:\ProgramData\IRIS\Agent\user-<date>.log` and verify the agent
serves all 200 with `→ 200` outcomes.

---

## 6. Test 4 — End-to-end soak

The "whole system" test. Less interesting numerically than Tests 1–3 but
critical for the capstone story.

- 4 rooms, all actual PCs you can find (or VMs).
- Operator dashboard open on a separate machine.
- Run for 24 hours.
- Every 4 hours, do a "full UX round trip":
  - View one PC's screen
  - Send a message to a PC, verify it pops up
  - Freeze a PC, verify the overlay
  - Unfreeze it
  - Open Usage Metrics, verify data is fresh
  - Open Alerts panel, dismiss something
  - Schedule a remote shutdown via the UI, verify it executed

Document each round-trip in the capstone report with timestamps + screenshots.
This is what your panel actually wants to see — not the latency histograms.

---

## 7. What to put in the capstone report

For each test, the report should have:

1. **Hypothesis** (what would success look like)
2. **Method** (the script or procedure, attached as appendix)
3. **Environment** (the baseline numbers from §2)
4. **Results table** (raw numbers)
5. **Latency chart** (simple line chart, time on X axis, latency on Y)
6. **Pass/fail verdict** against the targets in §3.4 / §4.3 / §5
7. **Discussion** — what you'd change if it failed, or what's the next ceiling
   if it passed

Add an **executive summary table** up front:

| Test | Target deployment | Stress level run | Result |
|---|---|---|---|
| DB heartbeat scaling | 80 agents | 240 agents (3×) | ✅ pass at 95p < N ms |
| UI snapshot polling | 80 PCs / 1 operator | 8h soak | ✅ pass, memory stable |
| Per-agent burst | 1 client / 8 concurrent | 200 concurrent burst | ✅ pass after 1.5.7 |
| End-to-end soak | 4 rooms / 24h | 24h continuous | ✅ pass |

The 1.5.7 escalation history (snapshot fix, dual-stack regression on
24H2 Windows 11) is itself a strong narrative for the capstone — you
discovered a Microsoft regression mid-development and engineered around it.
Write a sidebar in the report: **"What 24H2 broke and how IRIS works around
it"** — covers `CopyFromScreen` / `PrintWindow` / WinRT capture ladder + the
`HttpListener` → raw `TcpListener` swap. Both are concrete examples of
production engineering judgment under pressure, which is exactly what the
panel is grading.

---

## 8. Tools you'll need

- **PowerShell 7+** for the parallel `ForEach-Object -Parallel` syntax used in
  the burst test (Windows PowerShell 5.1 doesn't support it).
- **`pg_stat_statements`** PostgreSQL extension enabled. Add to
  `postgresql.conf`: `shared_preload_libraries = 'pg_stat_statements'` and
  restart Postgres; then `CREATE EXTENSION pg_stat_statements;` in the DB.
- **A spreadsheet** (Excel / Numbers / Google Sheets) for charting the CSVs.
  Don't waste time learning Grafana for this — capstone graders care about
  the data and the verdict, not the dashboarding tooling.
- **`IRIS.LoadTest`** — the synthetic-agent project described in §3.2.
  Keep it out of the production solution; it's a one-off.

## 9. What to skip (and why)

- **JMeter / Gatling / k6.** These tools are excellent for HTTP-load tests
  but IRIS isn't an HTTP service. Heartbeat goes via raw EF Core to Postgres;
  snapshots go via raw TCP. The synthetic agent in §3.2 is a better fit.
- **Chaos engineering** (random pod kills, fault injection). Out of scope
  for a capstone. You already proved fault tolerance during snapshot
  remediation; just write that up.
- **Long performance tuning loops.** If a target deployment of 80 PCs
  passes at 240 (3×), don't burn three weeks chasing a 1000-PC ceiling.
  Capstone time is finite; ship.
