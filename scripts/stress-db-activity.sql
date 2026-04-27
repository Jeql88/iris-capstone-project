-- stress-db-activity.sql
-- Observation queries to run in a *separate* psql session WHILE the load test is running.
-- Open with:  psql -h <host> -U postgres -d iris_db
-- Then \i scripts/stress-db-activity.sql  (or paste blocks one at a time).

-- 1. Connection state breakdown (active / idle / idle in transaction).
SELECT state, COUNT(*) AS connections
  FROM pg_stat_activity
 WHERE datname = current_database()
 GROUP BY state
 ORDER BY connections DESC;

-- 2. Currently running queries with their start times (oldest first).
SELECT pid,
       now() - query_start AS running_for,
       state,
       wait_event_type,
       wait_event,
       LEFT(query, 120) AS query
  FROM pg_stat_activity
 WHERE datname = current_database()
   AND state <> 'idle'
 ORDER BY query_start ASC NULLS LAST;

-- 3. Anything blocked on a lock (should always be empty during a healthy test).
SELECT bl.pid                AS blocked_pid,
       bl.query              AS blocked_query,
       kl.pid                AS blocking_pid,
       kl.query              AS blocking_query,
       now() - bl.query_start AS waited_for
  FROM pg_stat_activity bl
  JOIN pg_stat_activity kl
    ON kl.pid = ANY(pg_blocking_pids(bl.pid))
 WHERE bl.wait_event_type = 'Lock';

-- 4. Top 10 queries by accumulated execution time (requires pg_stat_statements).
--    If this errors with "relation does not exist", run once as superuser:
--      CREATE EXTENSION IF NOT EXISTS pg_stat_statements;
--    and ensure 'pg_stat_statements' is in shared_preload_libraries.
SELECT calls,
       ROUND(total_exec_time::numeric, 1)  AS total_ms,
       ROUND(mean_exec_time::numeric, 2)   AS mean_ms,
       ROUND(stddev_exec_time::numeric, 2) AS stddev_ms,
       LEFT(query, 140) AS query
  FROM pg_stat_statements
 ORDER BY total_exec_time DESC
 LIMIT 10;

-- 5. Row counts for the high-write tables (run before & after to compute throughput).
SELECT 'PCs'              AS table_name, COUNT(*) FROM "PCs"
UNION ALL SELECT 'HardwareMetrics',      COUNT(*) FROM "HardwareMetrics"
UNION ALL SELECT 'NetworkMetrics',       COUNT(*) FROM "NetworkMetrics"
UNION ALL SELECT 'PendingCommands',      COUNT(*) FROM "PendingCommands"
UNION ALL SELECT 'Alerts',               COUNT(*) FROM "Alerts";
