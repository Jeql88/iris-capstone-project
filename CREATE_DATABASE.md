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
