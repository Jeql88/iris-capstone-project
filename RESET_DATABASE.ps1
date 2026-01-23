# IRIS Database Reset Script
# This script drops and recreates the iris_db database, then applies migrations

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "IRIS Database Reset Utility" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$ErrorActionPreference = "Stop"

# Configuration
$PostgresPath = "C:\Program Files\PostgreSQL\17\bin\psql.exe"
$DatabaseName = "iris_db"
$PostgresUser = "postgres"

# Check if PostgreSQL is installed
if (-not (Test-Path $PostgresPath)) {
    Write-Host "Error: PostgreSQL not found at $PostgresPath" -ForegroundColor Red
    Write-Host "Please update the PostgresPath variable in this script." -ForegroundColor Yellow
    exit 1
}

Write-Host "Step 1: Dropping existing database..." -ForegroundColor Yellow
try {
    & $PostgresPath -U $PostgresUser -c "DROP DATABASE IF EXISTS $DatabaseName;"
    Write-Host "✓ Database dropped successfully" -ForegroundColor Green
} catch {
    Write-Host "⚠ Warning: Could not drop database (it may not exist)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Step 2: Creating fresh database..." -ForegroundColor Yellow
try {
    & $PostgresPath -U $PostgresUser -c "CREATE DATABASE $DatabaseName TEMPLATE template0;"
    Write-Host "✓ Database created successfully" -ForegroundColor Green
} catch {
    Write-Host "✗ Error: Failed to create database" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Step 3: Applying migrations..." -ForegroundColor Yellow
try {
    Set-Location -Path "IRIS.UI"
    dotnet ef database update --project ..\IRIS.Core
    Write-Host "✓ Migrations applied successfully" -ForegroundColor Green
} catch {
    Write-Host "✗ Error: Failed to apply migrations" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
} finally {
    Set-Location -Path ".."
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Database Reset Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Default Users Created:" -ForegroundColor Yellow
Write-Host "  - admin/admin (System Administrator)" -ForegroundColor White
Write-Host "  - itperson/admin (IT Personnel)" -ForegroundColor White
Write-Host "  - faculty/admin (Faculty)" -ForegroundColor White
Write-Host ""
