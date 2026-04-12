#!/usr/bin/env bash
set -euo pipefail

# IRIS Database Setup Script for Linux
# Prerequisites: psql (PostgreSQL client) installed and accessible
# Usage: ./setup-database-linux.sh [--host HOST] [--port PORT] [--user USER] [--password PASSWORD] [--database DB]

# Defaults matching appsettings.json
DB_HOST="${IRIS_DB_HOST:-localhost}"
DB_PORT="${IRIS_DB_PORT:-5432}"
DB_USER="${IRIS_DB_USER:-postgres}"
DB_PASSWORD="${IRIS_DB_PASSWORD:-postgres}"
DB_NAME="${IRIS_DB_NAME:-iris_db}"

# Parse CLI arguments (override env vars)
while [[ $# -gt 0 ]]; do
    case "$1" in
        --host)     DB_HOST="$2";     shift 2 ;;
        --port)     DB_PORT="$2";     shift 2 ;;
        --user)     DB_USER="$2";     shift 2 ;;
        --password) DB_PASSWORD="$2"; shift 2 ;;
        --database) DB_NAME="$2";     shift 2 ;;
        --help|-h)
            echo "Usage: $0 [--host HOST] [--port PORT] [--user USER] [--password PASSWORD] [--database DB]"
            echo ""
            echo "Environment variables: IRIS_DB_HOST, IRIS_DB_PORT, IRIS_DB_USER, IRIS_DB_PASSWORD, IRIS_DB_NAME"
            echo "Defaults: localhost:5432, postgres/postgres, iris_db"
            exit 0 ;;
        *) echo "Unknown argument: $1"; exit 1 ;;
    esac
done

export PGPASSWORD="$DB_PASSWORD"
PSQL="psql -h $DB_HOST -p $DB_PORT -U $DB_USER"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MIGRATIONS_SQL="$SCRIPT_DIR/../docs/migrations.sql"

# Step 0: Verify prerequisites
echo "=== IRIS Database Setup ==="
echo ""

if ! command -v psql &>/dev/null; then
    echo "ERROR: psql is not installed or not in PATH."
    echo "Install it with: sudo apt install postgresql-client"
    exit 1
fi

if [[ ! -f "$MIGRATIONS_SQL" ]]; then
    echo "ERROR: Migration script not found at $MIGRATIONS_SQL"
    echo "Generate it on a dev machine with:"
    echo "  dotnet ef migrations script --idempotent --project IRIS.Core --output docs/migrations.sql"
    exit 1
fi

# Step 1: Test connection to PostgreSQL server
echo "Testing connection to PostgreSQL at $DB_HOST:$DB_PORT..."
if ! $PSQL -d postgres -c "SELECT 1;" &>/dev/null; then
    echo "ERROR: Cannot connect to PostgreSQL server at $DB_HOST:$DB_PORT as $DB_USER"
    exit 1
fi
echo "Connection successful."
echo ""

# Step 2: Create database if it does not exist
echo "Checking if database '$DB_NAME' exists..."
DB_EXISTS=$($PSQL -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname = '$DB_NAME';")
if [[ "$DB_EXISTS" == "1" ]]; then
    echo "Database '$DB_NAME' already exists."
else
    echo "Creating database '$DB_NAME' (using template0 to avoid collation issues)..."
    $PSQL -d postgres -c "CREATE DATABASE \"$DB_NAME\" TEMPLATE template0;"
    echo "Database '$DB_NAME' created."
fi
echo ""

# Step 3: Run idempotent migrations
echo "Applying EF Core migrations..."
$PSQL -d "$DB_NAME" -f "$MIGRATIONS_SQL"
echo "Migrations applied successfully."
echo ""

# Step 4: Verify
echo "=== Verification ==="
echo "Tables in $DB_NAME:"
$PSQL -d "$DB_NAME" -c "\dt"
echo ""
echo "Migration history:"
$PSQL -d "$DB_NAME" -c 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";'
echo ""
MIGRATION_COUNT=$($PSQL -d "$DB_NAME" -tAc 'SELECT COUNT(*) FROM "__EFMigrationsHistory";')
echo "Total migrations applied: $MIGRATION_COUNT"
echo ""
echo "Database setup complete."

unset PGPASSWORD
