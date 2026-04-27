-- stress-db-cleanup.sql
-- Removes synthetic PCs (and their dependent rows) created by stress-db-seed.sql.
-- Run with:  psql -h <host> -U postgres -d iris_db -f stress-db-cleanup.sql

BEGIN;

DELETE FROM "HardwareMetrics"
 WHERE "PCId" IN (SELECT "Id" FROM "PCs" WHERE "Hostname" LIKE 'LOAD_PC_%');

DELETE FROM "NetworkMetrics"
 WHERE "PCId" IN (SELECT "Id" FROM "PCs" WHERE "Hostname" LIKE 'LOAD_PC_%');

DELETE FROM "PendingCommands"
 WHERE "MacAddress" IN (SELECT "MacAddress" FROM "PCs" WHERE "Hostname" LIKE 'LOAD_PC_%');

DELETE FROM "PCs" WHERE "Hostname" LIKE 'LOAD_PC_%';

COMMIT;

SELECT COUNT(*) AS remaining_synthetic_pcs
  FROM "PCs" WHERE "Hostname" LIKE 'LOAD_PC_%';
