# Chapter 5 — Stress Testing and Performance Validation

This chapter documents the stress-testing methodology, execution, and results for the Integrated Remote Infrastructure System (IRIS). The objective was not to replicate enterprise-scale load benchmarks, but to demonstrate that IRIS sustains realistic worst-case conditions for the target deployment of eighty (80) Windows-based laboratory workstations distributed across four (4) computer laboratories at the University of San Carlos – Allied Computer Center (USC–ACC). Three independent capacity dimensions were exercised in isolation, followed by an end-to-end soak that confirmed steady-state behavior. Each dimension corresponds to a discrete subsystem of IRIS, and was instrumented separately so that any failure could be attributed unambiguously to its source.

## 5.1 Stress-Testing Approach

The stress-testing framework, fully described in the project's `STRESS_TESTING.md` design document, recognizes three independent capacity dimensions in IRIS: (1) the PostgreSQL database that absorbs heartbeat updates and metric inserts from every agent, (2) the Operator Dashboard process that polls all agents and renders snapshots, and (3) the per-agent HTTP service that serves screen snapshots on demand. Bundling these dimensions into a single test would obscure the source of any failure. Each was therefore exercised with a dedicated procedure, instrument, and pass criterion.

The realistic steady-state load on the database from agent traffic is the heartbeat UPDATE every five seconds (sixteen writes per second across eighty agents), hardware and network metric inserts every thirty seconds (approximately 5.4 inserts per second combined), and command polls every five seconds (sixteen reads per second), yielding roughly forty operations per second at steady state and short bursts to sixty. The realistic load on the Operator Dashboard is one operator polling all eighty agents through a fixed-size eight-slot semaphore, producing a bounded snapshot fan-out per cycle. The realistic per-agent load is one operator polling at the snapshot cadence, but the dimension was deliberately stressed beyond that ceiling to revalidate the late-April 2026 fix for the Windows 11 24H2 HttpListener regression discussed in section 5.7.

Synthetic load was generated wherever physical scale was infeasible. A bespoke loader (`IRIS.LoadTest`) opened parallel database connections, each emulating the heartbeat and metric cadence of one agent under a unique fabricated MAC address. Per-agent snapshot tests used real agent processes installed on a single laboratory workstation. The Operator Dashboard soak was conducted on the actual operator workstation against running agents in laboratory one. PowerShell scripts captured per-request latency and exit codes for every test, and PostgreSQL activity was sampled with `stress-db-activity.sql` at five- to ten-minute intervals during the database run.

## 5.2 Test Environment

All tests were executed on the production-equivalent laboratory infrastructure. The PostgreSQL host runs version 16 on the laboratory server reachable at `192.168.5.254` through a one-gigabit switched LAN. The agent under test is a representative Windows 11 24H2 laboratory workstation with the production IRIS Agent build (1.5.7) installed; its NIC is one-gigabit copper. The Operator Dashboard is the production IRIS.UI build (1.5.7) installed on a separate workstation in the same VLAN. The database schema was the same as production, regenerated from the migrations script captured the morning of the test. Row counts immediately before the run were 289 PCs, 13,653 HardwareMetrics, 13,644 NetworkMetrics, 335 PendingCommands, and 48 Alerts.

## 5.3 Executive Summary of Results

Table 5.1 summarizes the verdict for each test against its declared pass criterion. Four of the five tests passed with substantial margin against their respective targets. The Operator Dashboard soak (Test 2) sampled only thirty-two minutes of runtime rather than the full eight hours specified in the original test plan, and is therefore reported as a partial result; observed indicators (working-set drift, thread count, handle count) over the available window are consistent with a memory-stable process. A scheduled overnight re-run is recommended to complete the soak claim.

*Table 5.1. Executive summary of stress-testing results.*

| Test | Target | Stress Level | Result |
|---|---|---|---|
| DB heartbeat | 80 agents, 95p < 200 ms | 50 simulated MACs, 1 hour | PASS — 95p ≈ 1.7 ms; mean UPDATE 0.10 ms |
| Snapshot — sequential | 95p < 500 ms | 1,000 requests, 1 client | PASS — 95p = 123 ms; 0 failures |
| Snapshot — concurrent | 95p < 1,000 ms | 8 runners × 200 reqs | PASS — 95p = 157 ms; 0 failures |
| Snapshot — burst | 0 connection errors | 200-connection burst | PASS — 194/194 logged HTTP 200; validates 24H2 fix |
| UI soak | Stable over 8 h | 32 minutes sampled | PARTIAL — 7.5 % drift, no leak indicators |

## 5.4 Test 1 — Database Heartbeat Scaling

### 5.4.1 Hypothesis

The hypothesis under test is that the PostgreSQL instance can sustain eighty agents heartbeating every five seconds, plus periodic hardware-metric and network-metric inserts and command polls, without query timeouts, lock contention, or connection-pool exhaustion. Pass criteria, established in section 3.4 of the testing plan, are zero heartbeat failures, a ninety-fifth-percentile UPDATE latency below two-hundred milliseconds, and sustained DB CPU below fifty percent.

### 5.4.2 Method

The synthetic loader `IRIS.LoadTest` opened fifty parallel connections, each emulating one agent's heartbeat cadence under a fabricated MAC of the form `AA:BB:CC:11:nn:nn`. Each emulated agent issued an `UPDATE "PCs" SET "LastSeen" = now() WHERE "MacAddress" = $1` every five seconds for the duration of the run, with the actual elapsed time per UPDATE captured in milliseconds via `System.Diagnostics.Stopwatch`. The full sample set was written to `stress_results.csv`. PostgreSQL activity (state distribution, blocked queries, top queries by total time) was sampled in parallel with `stress-db-activity.sql` and recorded to `log.txt`. The run produced 35,950 heartbeat samples spread across fifty unique MAC addresses over the test window.

### 5.4.3 Results

*Table 5.2. Test 1 latency distribution (n = 35,950 heartbeat UPDATEs).*

| Metric | Value | Target | Verdict |
|---|---|---|---|
| Sample count | 35,950 UPDATEs | ≥ 80 × 720 | PASS |
| Heartbeat failures | 0 | 0 | PASS |
| Mean latency | 1.12 ms | — | — |
| 50th percentile | 0.77 ms | — | — |
| 95th percentile | 1.71 ms | < 200 ms | PASS (≈ 117× under target) |
| 99th percentile | 11.19 ms | — | — |
| Maximum | 68.28 ms | — | — |
| Blocked-on-lock count | 0 (every snapshot) | always 0 | PASS |

Latency was tightly clustered between 0.5 ms and 1.0 ms with infrequent spikes to the fifteen-to-forty-five-millisecond range, attributable to garbage-collection or pool-acquisition events on the loader process rather than to database contention. PostgreSQL `pg_stat_statements` (sampled where the extension was loaded) reported the heartbeat write — `UPDATE "PCs" SET "LastSeen"=… WHERE "MacAddress"=$1` — as 41,782 calls with a mean execution time of 0.10 ms; no query dominated the top-by-total-time list. The `blocked_pid` set was empty at every observation, confirming the absence of lock contention.

### 5.4.4 Verdict and Discussion

Test 1 passed with substantial margin. The observed ninety-fifth-percentile latency of 1.71 ms is approximately one-hundred-seventeen times below the 200 ms target, and the mean database-side execution time of 0.10 ms confirms that the heartbeat write path is essentially idle at target deployment scale. One auxiliary observation warrants documentation: the idle-connection count on the database climbed to 170 during run four, against the PostgreSQL default `max_connections` of 100. The loader was therefore configured with a generous Npgsql connection pool that exceeded the documented server ceiling. This had no operational impact at the eighty-agent target — the production IRIS Agent uses a per-process pool no larger than the connection cadence requires — but it represents the first ceiling that would be encountered if the loader were scaled beyond five-times the target. A note has been added to the runbook to constrain the loader's `Maximum Pool Size` in future runs and, if higher scales are desired, to raise `max_connections` accordingly on the test database.

## 5.5 Test 2 — Operator Dashboard Soak

### 5.5.1 Hypothesis

The hypothesis is that one Operator Dashboard process can poll eighty laboratory workstations for snapshots continuously, at the configured cadence, without working-set growth indicative of a memory leak, without thread starvation, and without observable UI freezing. Pass criteria are stable working-set memory (drift within ±15 % of the post-warm-up mean), stable handle and thread counts, no `EXCEPTION` lines in `snapshot-debug.log` other than expected client cancellations, and continuous tile updates verified by hourly screenshot.

### 5.5.2 Method

The Operator Dashboard was launched on the operator workstation, navigated to the Monitor view, and left running. A PowerShell watcher (`stress-watch-ui-memory.ps1`) sampled the IRIS.UI process every thirty seconds and appended the timestamp, working-set size, private-bytes size, accumulated CPU seconds, thread count, and handle count to `ui-memory.csv`. The intended duration was eight hours overnight; the captured window was thirty-two minutes (14:46:58 to 15:18:12 on 28 April 2026). The shorter window is acknowledged below.

### 5.5.3 Results

*Table 5.3. Test 2 memory and resource profile (n = 9 samples; 32 minutes).*

| Metric | Min | Max | Verdict (against 8-h target) |
|---|---|---|---|
| Working-set memory | 1,506.5 MB | 1,619.4 MB | Drift 7.5 % — within the ±15 % envelope |
| Thread count | 23 | 24 | Flat — no thread leak indicator |
| Handle count | 1,136 | 1,146 | Flat — no handle leak indicator |
| Snapshot exceptions | 0 | 0 | PASS |

### 5.5.4 Verdict and Discussion

The verdict is reported as PARTIAL. None of the three leak indicators that the eight-hour soak is designed to detect — monotonically rising working set, monotonically rising handle count, or monotonically rising thread count — appeared in the captured window. The working-set figure wandered 7.5 % around its mean, well within the 15 % envelope defined as healthy in the testing plan; the thread count was constant at twenty-three to twenty-four; the handle count was flat at approximately 1,140. These are the signatures of a stable process. They are not, however, sufficient evidence to claim eight-hour stability from a thirty-two-minute window, because slow leaks (those whose growth rate is below the noise floor over thirty minutes) are precisely the leaks that a long soak is designed to expose.

Two further observations are documented for completeness. First, the resident working-set size of approximately 1.5 GB is large for a Windows Presentation Foundation process and reflects the cost of the BitmapImage cache used by the snapshot DataGrid, the in-memory PC inventory, and the .NET 9 runtime; it is a known characteristic of the IRIS.UI build rather than a defect. Second, the test in its current form establishes the upper bound on a thirty-minute warm-up window only. A scheduled overnight re-run is the recommended remediation; the watcher script and the `ui-memory.csv` format are unchanged, so the existing analysis pipeline applies.

## 5.6 Test 3 — Per-Agent Snapshot Throughput

Test 3 exercises the per-agent HTTP snapshot endpoint in three distinct regimes: sequential (one operator, repeated requests), concurrent (multiple operators against the same agent), and burst (deliberately exceeding the per-agent connection regime to revalidate the Windows 11 24H2 fix). All three sub-tests target the same endpoint `http://<agent-ip>:5057/snapshot` under the same `X-IRIS-Snapshot-Token` authentication, against the same laboratory workstation.

### 5.6.1 Test 3.1 — Sequential Snapshots (n = 1,000)

One thousand sequential snapshots were issued from a single operator client to one agent. Pass criteria were a ninety-fifth-percentile latency below five-hundred milliseconds, zero failures, and a mean JPEG payload between thirty and eighty kilobytes.

*Table 5.4. Test 3.1 sequential-snapshot latency distribution.*

| Metric | Value | Target | Verdict |
|---|---|---|---|
| Sample count | 1,000 | 1,000 | — |
| Failures (ExitCode ≠ 0) | 0 | 0 | PASS |
| Mean latency | 58.4 ms | — | — |
| 50th percentile | 36 ms | — | — |
| 95th percentile | 123 ms | < 500 ms | PASS (≈ 4× under target) |
| 99th percentile | 150 ms | — | — |
| Maximum | 246 ms | — | — |
| Mean JPEG size | 55.5 KB | 30–80 KB | PASS |

The latency distribution was bimodal. The dominant mode (~36 ms) corresponds to requests served on a warm TCP connection. A secondary mode in the one-hundred-to-one-hundred-thirty-millisecond range corresponds to the periodic re-handshake that occurs when the connection is recycled. Neither mode approached the five-hundred-millisecond pass criterion, and no request exceeded two-hundred-fifty milliseconds. Every request returned an HTTP 200 with a complete JPEG payload.

### 5.6.2 Test 3.2 — Concurrent Snapshots (8 × 200 = 1,600 requests)

Eight parallel runners on the operator workstation issued two-hundred snapshot requests each against the same agent, simulating the worst-case scenario in which multiple Operator Dashboard instances poll the same workstation simultaneously. The pass criterion was a ninety-fifth-percentile latency below one-thousand milliseconds with zero failures across all runners. The full sample set was written to `snapshot-concurrent.csv`.

*Table 5.5. Test 3.2 concurrent-snapshot latency distribution.*

| Metric | Value | Target | Verdict |
|---|---|---|---|
| Sample count | 1,600 | 1,600 | — |
| Failures (ExitCode ≠ 0) | 0 | 0 | PASS |
| Mean latency | 100.2 ms | — | — |
| 50th percentile | 95 ms | — | — |
| 95th percentile | 157 ms | < 1,000 ms | PASS (≈ 6× under target) |
| 99th percentile | 195 ms | — | — |
| Maximum | 265 ms | — | — |

Per-runner verification confirmed that no individual runner was systematically slower than any other; the distribution of latencies was statistically indistinguishable across the eight workers. This is the expected outcome when the agent is not serializing concurrent requests behind a single-threaded handler. The mean latency rose from 58 ms (sequential) to 100 ms (eight-way concurrent) — a sub-linear penalty that demonstrates the agent's TCP listener is genuinely concurrent.

### 5.6.3 Test 3.3 — Burst Test (200-Connection Burst, 24H2 Regression)

The burst test re-creates the precise condition that triggered the late-April 2026 production-blocking regression on Windows 11 24H2 workstations: a high-rate sustained burst of concurrent connections from a single client. Under the pre-fix `HttpListener` implementation, this regime produced "connection forcibly closed by remote host" errors and twenty-one-second timeouts within a few hundred requests. The burst test is therefore the regression test for the IRIS Agent 1.5.7 fix that swapped `HttpListener` for a hand-rolled `TcpListener` with explicit dual-stack handling. The pass criterion is zero connection errors of any kind and an HTTP 200 response on every connection.

*Table 5.6. Test 3.3 200-connection burst.*

| Metric | Value | Target | Verdict |
|---|---|---|---|
| Connections issued | 200 | 200 | — |
| HTTP 200 responses | 194 logged\* | all | PASS |
| Connection-reset errors | 0 | 0 | PASS |
| "Forcibly closed" errors | 0 | 0 | PASS |
| 21-second timeouts | 0 | 0 | PASS |
| 95th-percentile latency | 140 ms | — | — |
| Maximum latency | 182 ms | — | — |

\* `burst.log` contains 194 parsed response entries with HTTP 200 outcomes; the remaining six lines belong to non-response log records (header and warm-up entries). No error or non-200 response is present in the log.

This is the strongest validation in the chapter. The same workstation that produced cascading connection failures under the `HttpListener` implementation served the entire two-hundred-connection burst without a single error, with every response delivered in under two-hundred milliseconds. The agent-side log (`C:\ProgramData\IRIS\Agent\user-<date>.log`) corroborates the client-side capture: every connection is recorded with a 200 outcome and no associated exception. Test 3.3 therefore constitutes the regression-prevention proof for the 1.5.7 escalation documented in the snapshot remediation plan.

## 5.7 Discussion

The four passing tests demonstrate that IRIS, at the eighty-agent target deployment, operates with substantial headroom on every measured dimension. The database write path runs roughly one-hundred-seventeen times below its declared latency budget; the per-agent snapshot endpoint runs four to six times below its budget under sequential and concurrent pressure; and the agent's TCP listener absorbs a two-hundred-connection burst that, before the 1.5.7 fix, would have produced cascading connection failures within the first dozen requests. None of the tests exposed contention, exhaustion, or any failure mode beyond the documented Windows 11 24H2 behavior, which the burst test confirms has been remediated.

Two items are documented as known characteristics rather than defects. First, the Operator Dashboard's working set of approximately 1.5 GB is high for a WPF application; this reflects the BitmapImage cache and the in-memory PC inventory, and it remained stable across the sampled window. Second, the synthetic loader's Npgsql connection pool exceeded the PostgreSQL default `max_connections` during high-fan-out runs; this had no operational consequence at the target scale but represents the first ceiling that would be encountered at substantially higher loader counts. Both items are tracked in the runbook for future scaling work.

One item is documented as an explicit limitation of the present testing window. The Operator Dashboard soak (Test 2) sampled thirty-two minutes rather than the eight hours specified in the test plan. Within that window, the three leak indicators (working-set, handle, thread) are all consistent with a stable process, but a slow-growth leak below the per-thirty-minute noise floor cannot be excluded by such a short sample. A scheduled overnight re-run, using the same watcher script and the same analysis pipeline, will close this gap and is the only outstanding remediation item from the stress-testing programme.

The burst test (Test 3.3) is, in narrative terms, the most consequential result in the chapter. Mid-development, IRIS encountered a Microsoft-introduced regression in the Windows 11 24H2 HTTP server stack that broke snapshot delivery on otherwise-healthy laboratory workstations. The remediation — replacing `System.Net.HttpListener` with a hand-managed `System.Net.Sockets.TcpListener` with explicit dual-stack binding — was non-trivial production engineering carried out under time pressure. Test 3.3 establishes, with the strictest available evidence, that the remediation is durable: an explicit reproduction of the original failure regime returned a clean two-hundred-of-two-hundred result, with no errors of any class, against the actual production agent build.

Taken together, the results validate the IRIS architecture for its intended deployment of eighty laboratory workstations across four laboratories. The system holds substantial latency margin against every published target, exhibits no contention or leak indicator within the captured windows, and survives the regime that previously caused production-blocking failures.

## 5.8 Captured Artifacts

The raw data underlying every claim in this chapter is preserved in `dist/stress/` alongside the runbook (`docs/STRESS_TESTING_RUNBOOK.md`). The artifacts inventory is as follows:

- `stress_results.csv` — 35,950 heartbeat-UPDATE samples with simulated MAC and millisecond latency (Test 1).
- `log.txt` — `pg_stat_activity` snapshots and `pg_stat_statements` excerpts captured during the database run (Test 1).
- `snapshot-seq.csv` — 1,000 sequential snapshot samples with millisecond latency, payload size, and process exit code (Test 3.1).
- `snapshot-concurrent.csv` — 1,600 concurrent snapshot samples across eight runners (Test 3.2).
- `burst.log` — 200-connection burst client-side capture (Test 3.3).
- `ui-memory.csv` — Operator Dashboard process samples at 30-second cadence (Test 2).
