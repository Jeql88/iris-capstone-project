-- stress-db-seed.sql
-- Seeds N synthetic PCs into the database for the heartbeat-scaling stress test.
-- Run with:  psql -h <host> -U postgres -d iris_db -v count=240 -v room=1 -f stress-db-seed.sql
--
-- Synthetic rows are tagged with hostname prefix 'LOAD_PC_' so the cleanup script
-- can remove them safely without touching real PCs.

\if :{?count}
\else
  \set count 240
\endif

\if :{?room}
\else
  \set room 1
\endif

DO $$
DECLARE
    target_count int := :count;
    target_room  int := :room;
    i int;
    mac text;
    host text;
BEGIN
    -- Make sure the target room exists; if not, fall back to the first room.
    IF NOT EXISTS (SELECT 1 FROM "Rooms" WHERE "Id" = target_room) THEN
        SELECT "Id" INTO target_room FROM "Rooms" ORDER BY "Id" LIMIT 1;
        IF target_room IS NULL THEN
            RAISE EXCEPTION 'No rooms exist in the database. Seed a room first.';
        END IF;
    END IF;

    FOR i IN 0..(target_count - 1) LOOP
        mac  := format('AA:BB:CC:%s:%s:%s',
                       lpad(to_hex( i        & 255), 2, '0'),
                       lpad(to_hex((i >>  8) & 255), 2, '0'),
                       lpad(to_hex((i >> 16) & 255), 2, '0'));
        host := format('LOAD_PC_%s', lpad(i::text, 4, '0'));

        INSERT INTO "PCs" ("MacAddress", "IpAddress", "RoomId", "Hostname",
                           "OperatingSystem", "Status", "LastSeen", "CreatedAt")
        VALUES (mac, '10.0.0.' || ((i % 250) + 1)::text, target_room, host,
                'Windows 11 Pro 24H2 (synthetic)', 1, now(), now())
        ON CONFLICT ("MacAddress") DO NOTHING;
    END LOOP;

    RAISE NOTICE 'Seeded % synthetic PCs into RoomId=%', target_count, target_room;
END $$;
