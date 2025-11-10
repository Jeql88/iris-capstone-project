-- Fix PostgreSQL collation version mismatch
-- Run this in psql or pgAdmin as postgres user

-- Connect to template1 database
\c template1

-- Refresh collation version
ALTER DATABASE template1 REFRESH COLLATION VERSION;

-- Alternatively, if above doesn't work, reindex all objects
REINDEX DATABASE template1;

-- Now you can create the iris_db database
\c postgres
CREATE DATABASE iris_db TEMPLATE template0;
