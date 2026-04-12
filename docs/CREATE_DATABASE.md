# Fix PostgreSQL Collation Issue and Create Database

## The Problem
PostgreSQL template1 database has a collation version mismatch, preventing database creation.

## Solution

### Option 1: Use pgAdmin or PostgreSQL GUI
1. Open pgAdmin
2. Connect to PostgreSQL server
3. Right-click on "Databases" → "Create" → "Database"
4. Name: `iris_db`
5. Template: Select `template0` (not template1)
6. Click "Save"

### Option 2: Use Command Line (if psql is in PATH)
```bash
# Windows (PowerShell)
& "C:\Program Files\PostgreSQL\16\bin\psql.exe" -U postgres -c "CREATE DATABASE iris_db TEMPLATE template0;"

# Or find your PostgreSQL installation
where.exe psql
```

### Option 3: Fix template1 collation (requires admin)
```sql
-- Connect to template1 database
\c template1

-- Refresh collation version
ALTER DATABASE template1 REFRESH COLLATION VERSION;

-- Then create database normally
\c postgres
CREATE DATABASE iris_db;
```

## After Creating Database

Run the migrations:
```bash
cd IRIS.UI
dotnet ef database update --project ..\IRIS.Core
```

## Verify Database Creation
```bash
# List databases
psql -U postgres -l

# Or in psql
\l
```

## Linux Setup

### Prerequisites
- PostgreSQL client tools (`psql`): `sudo apt install postgresql-client`
- PostgreSQL server accessible from the Linux machine

### Quick Setup
```bash
# From the project root directory
chmod +x scripts/setup-database-linux.sh
./scripts/setup-database-linux.sh
```

### Custom Configuration
```bash
./scripts/setup-database-linux.sh \
    --host 192.168.1.100 \
    --port 5432 \
    --user postgres \
    --password mypassword \
    --database iris_db
```

Or use environment variables:
```bash
export IRIS_DB_HOST=192.168.1.100
export IRIS_DB_PASSWORD=mypassword
./scripts/setup-database-linux.sh
```

### Regenerating Migration Script
When new EF Core migrations are added, regenerate the SQL script on a dev machine:
```bash
dotnet ef migrations script --idempotent --project IRIS.Core --output docs/migrations.sql
```
Commit the updated `docs/migrations.sql` to the repository.
