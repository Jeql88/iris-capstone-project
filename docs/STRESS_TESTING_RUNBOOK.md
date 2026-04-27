# IRIS Stress Test - Execution Runbook

Companion to [STRESS_TESTING.md](STRESS_TESTING.md). This is the day-of cheat
sheet: copy/paste commands, tick boxes, capture artifacts.

All scripts live under [scripts/](../scripts/). Outputs go to `dist/stress/`.

---

## 0. Pre-flight (do once, the night before)

- [ ] **PowerShell 7+** installed (`pwsh -v`). Required for `ForEach-Object -Parallel`.
- [ ] **PostgreSQL** reachable from the loader machine; superuser password ready.
- [ ] **`pg_stat_statements` extension enabled** on the DB host:
      ```sql
      -- in postgresql.conf
      shared_preload_libraries = 'pg_stat_statements'
      -- restart postgres, then:
      CREATE EXTENSION IF NOT EXISTS pg_stat_statements;
      ```
- [ ] **Build IRIS.Core once** so `Npgsql.dll` is on disk for the loader script:
      ```bash
      dotnet build IRIS.Core/IRIS.Core.csproj
      ```
- [ ] **Snapshot token** copied from the agent's `appsettings.json`
      (`ScreenStream:Token` or equivalent).
- [ ] **Baseline numbers recorded** per [STRESS_TESTING.md §2](STRESS_TESTING.md):
      hardware specs, NIC, switch, DB row counts. Save to a `baseline.txt`.

---

## 1. Test 1 - DB heartbeat scaling (§3)

### 1a. Seed synthetic PCs (1x = 80, 3x = 240 per the doc's pass criterion)

```bash
psql -h <db-host> -U postgres -d iris_db -v count=240 -v room=1 \
     -f scripts/stress-db-seed.sql
```

> Replace `room=1` with the RoomId you want the synthetic PCs assigned to.
> The script idempotently inserts `LOAD_PC_0000`..`LOAD_PC_NNNN`.

### 1b. Open an observation session in another terminal

```bash
psql -h <db-host> -U postgres -d iris_db
\i scripts/stress-db-activity.sql
```

Re-run `\i scripts/stress-db-activity.sql` every 5-10 minutes during the run.
Watch for: blocked queries (should be empty), CPU usage, top queries by total time.

### 1c. Run the loader

Three runs back-to-back (each 60 minutes; doc §3.4):

```powershell
# Target deployment
pwsh -File scripts/stress-db-loader.ps1 -AgentCount 80  -DurationSeconds 3600

# 2x headroom
pwsh -File scripts/stress-db-loader.ps1 -AgentCount 160 -DurationSeconds 3600

# 3x headroom (this is the pass criterion)
pwsh -File scripts/stress-db-loader.ps1 -AgentCount 240 -DurationSeconds 3600
```

The loader prints a summary at the end and writes per-heartbeat samples to
`dist/stress/db-loader-<timestamp>-<count>.csv`. Capture in your report:

| Metric | Where it comes from | Target |
|---|---|---|
| Heartbeats sent / errors | Loader summary | 0 errors at 240x |
| 95p heartbeat latency | Loader summary | < 200 ms |
| DB CPU during run | Task Manager / `top` on DB host | < 50 % sustained |
| Blocked-on-lock count | `stress-db-activity.sql` query 3 | always 0 |

### 1d. Cleanup

```bash
psql -h <db-host> -U postgres -d iris_db -f scripts/stress-db-cleanup.sql
```

---

## 2. Test 2 - Operator UI 8h soak (§4)

On the operator PC:

1. Start IRIS.UI normally and navigate to the Monitor view.
2. In another `pwsh` window, start the memory watcher:
   ```powershell
   pwsh -File scripts/stress-watch-ui-memory.ps1
   ```
   (Defaults: every 30 minutes, appends to `dist/stress/ui-memory.csv`.)
3. Leave both running for **8 hours overnight**.
4. Take screenshots of the Monitor view every hour (phone camera is fine).
5. Tail `snapshot-debug.log` after the run: search for `EXCEPTION:`.

Pass criteria:

- Memory CSV charted in Excel: line is flat (within +/- 15 % of stable mean).
- No `EXCEPTION:` lines in `snapshot-debug.log` other than client cancellations.
- Hourly screenshots show tiles still updating.

---

## 3. Test 3 - Per-agent snapshot throughput (§5)

Pick one healthy lab PC as the target. Get its IP and the snapshot token.

### 3a. Sequential 1000 requests (§5.1)

```powershell
pwsh -File scripts/stress-snapshot-sequential.ps1 `
     -AgentIp 10.0.0.21 -Token "<token>"
```

Pass: 0 failures, 95p < 500 ms, mean JPEG 30-80 KB.

### 3b. 8-runner concurrency (§5.2)

```powershell
pwsh -File scripts/stress-snapshot-concurrent.ps1 `
     -AgentIp 10.0.0.21 -Token "<token>"
```

Pass: every runner >= 99 % success, 95p < 1 s.

### 3c. 200-concurrent burst (§5.3 - 24H2 regression test)

```powershell
pwsh -File scripts/stress-snapshot-burst.ps1 `
     -AgentIp 10.0.0.21 -Token "<token>"
```

Pass: 200x HTTP 200, zero `reset`/`forcibly closed`/`timeout` rows.
Then on the **agent PC**, tail:

```
C:\ProgramData\IRIS\Agent\user-<date>.log
```

and confirm every request is logged as `-> 200`. Save the relevant excerpt.

---

## 4. Test 4 - End-to-end soak (§6)

24 hours. Every 4 hours run the full UX round-trip from the doc:
view a screen, send a message, freeze/unfreeze, open Usage Metrics, dismiss
an alert, schedule a remote shutdown. Document each round with timestamps +
screenshots in the report.

No script needed - just do it manually and write down what you observed.

---

## 5. Outputs to keep for the capstone report

```
dist/stress/
  baseline.txt                          # from §0
  db-loader-<ts>-80.csv                 # from 1c
  db-loader-<ts>-160.csv
  db-loader-<ts>-240.csv
  db-activity-<ts>.txt                  # paste from 1b
  ui-memory.csv                         # from §2
  snapshot-seq-<ts>/snapshot-seq.csv    # from 3a
  snapshot-conc-<ts>/snapshot-concurrent.csv  # from 3b
  snapshot-burst-<ts>/burst.csv         # from 3c
  snapshot-burst-<ts>/burst.log
  e2e-soak-roundtrip-<ts>.md            # written by hand from §4
```

Chart the latency CSVs in Excel; embed in the report per
[STRESS_TESTING.md §7](STRESS_TESTING.md).
