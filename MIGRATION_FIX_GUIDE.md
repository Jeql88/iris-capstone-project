# IRIS Migration Troubleshooting Guide

## ? What Was Fixed

### Problem 1: Duplicate User Seeding
**Issue:** Three migrations tried to insert the same users (IDs 1, 2, 3):
- `UpdatePasswordsToBCrypt` - attempted INSERT
- `SyncModel` - attempted INSERT with old SHA256 hashes
- `FixBCryptHashes` - attempted UPDATE

**Solution:** Removed all duplicate migrations and consolidated user seeding into `InitialCreate`.

### Problem 2: Migration Order Conflict
**Issue:** Migrations executed in wrong order, causing primary key violations.

**Solution:** Single `InitialCreate` migration now handles all schema creation and user seeding.

### Problem 3: Password Hash Inconsistency
**Issue:** `SyncModel` used SHA256 hashes instead of BCrypt.

**Solution:** All users now use BCrypt hashes with work factor 11.

---

## ?? Quick Start: Reset Your Database

### Option 1: Use PowerShell Script (Recommended)
```powershell
# From project root directory
.\RESET_DATABASE.ps1
```

### Option 2: Manual Steps
```powershell
# 1. Drop existing database
& "C:\Program Files\PostgreSQL\16\bin\psql.exe" -U postgres -c "DROP DATABASE IF EXISTS iris_db;"

# 2. Create fresh database
& "C:\Program Files\PostgreSQL\16\bin\psql.exe" -U postgres -c "CREATE DATABASE iris_db TEMPLATE template0;"

# 3. Apply migrations
cd IRIS.UI
dotnet ef database update --project ..\IRIS.Core
cd ..
```

---

## ?? Verify Migration Status

### Check Applied Migrations
```powershell
cd IRIS.UI
dotnet ef migrations list --project ..\IRIS.Core
```

**Expected Output:**
```
20251014161408_InitialCreate (Applied)
```

### Check Database Tables
```powershell
& "C:\Program Files\PostgreSQL\16\bin\psql.exe" -U postgres -d iris_db -c "\dt"
```

**Expected Tables:**
- Alerts
- HardwareMetrics
- NetworkMetrics
- PCHardwareConfigs
- PCs
- Policies
- Rooms
- Software
- SoftwareInstalled
- SoftwareRequests
- SoftwareUsageHistory
- UserLogs
- Users
- WebsiteUsageHistory
- __EFMigrationsHistory

### Verify Seed Data
```powershell
& "C:\Program Files\PostgreSQL\16\bin\psql.exe" -U postgres -d iris_db -c "SELECT \"Id\", \"Username\", \"Role\" FROM \"Users\";"
```

**Expected Output:**
```
 Id | Username  |        Role           
----+-----------+----------------------
  1 | admin     | SystemAdministrator
  2 | itperson  | ITPersonnel
  3 | faculty   | Faculty
```

---

## ??? Common Issues & Solutions

### Error: "Database does not exist"
```powershell
# Solution: Create the database first
& "C:\Program Files\PostgreSQL\16\bin\psql.exe" -U postgres -c "CREATE DATABASE iris_db TEMPLATE template0;"
```

### Error: "Password authentication failed"
```powershell
# Solution: Ensure PostgreSQL is running and password is correct
# You may need to set PGPASSWORD environment variable
$env:PGPASSWORD="your_postgres_password"
```

### Error: "Collation version mismatch"
```powershell
# Solution: Use template0 instead of template1
& "C:\Program Files\PostgreSQL\16\bin\psql.exe" -U postgres -c "CREATE DATABASE iris_db TEMPLATE template0;"
```

### Error: "Duplicate key value violates unique constraint"
**This should no longer occur after applying the fixes.**

If it does:
1. Drop the database completely
2. Run `RESET_DATABASE.ps1`
3. Verify no old migration files exist in `IRIS.Core/Migrations/` except:
   - `20251014161408_InitialCreate.cs`
   - `20251014161408_InitialCreate.Designer.cs`
   - `IRISDbContextModelSnapshot.cs`

---

## ?? Migration File Checklist

### ? Files That Should Exist
- `IRIS.Core/Migrations/20251014161408_InitialCreate.cs`
- `IRIS.Core/Migrations/20251014161408_InitialCreate.Designer.cs`
- `IRIS.Core/Migrations/IRISDbContextModelSnapshot.cs`

### ? Files That Should NOT Exist
- ~~`20251110112050_UpdatePasswordsToBCrypt.cs`~~ (REMOVED)
- ~~`20251110113434_FixBCryptHashes.cs`~~ (REMOVED)
- ~~`20251110120226_SyncModel.cs`~~ (REMOVED)

---

## ?? Default Credentials

All users have the password: `admin`

| Username | Password | Role |
|----------|----------|------|
| admin | admin | System Administrator |
| itperson | admin | IT Personnel |
| faculty | admin | Faculty |

**Security Note:** These are BCrypt hashes with work factor 11. Change passwords after deployment!

---

## ?? Testing Your Fix

### 1. Clean Build
```powershell
dotnet clean
dotnet build
```

### 2. Reset Database
```powershell
.\RESET_DATABASE.ps1
```

### 3. Run Application
```powershell
cd IRIS.UI
dotnet run
```

### 4. Test Login
- Launch the application
- Login with `admin/admin`
- Verify dashboard loads

---

## ?? Still Having Issues?

### Enable EF Core Logging
Edit `IRIS.UI/appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

### Check Migration History
```powershell
& "C:\Program Files\PostgreSQL\16\bin\psql.exe" -U postgres -d iris_db -c "SELECT * FROM \"__EFMigrationsHistory\";"
```

Should show only:
```
MigrationId               | ProductVersion
--------------------------+---------------
20251014161408_InitialCreate | 9.0.9
```

### Nuclear Option: Complete Reset
```powershell
# 1. Delete all migrations except Designer files
Remove-Item IRIS.Core\Migrations\*.cs

# 2. Drop database
& "C:\Program Files\PostgreSQL\16\bin\psql.exe" -U postgres -c "DROP DATABASE IF EXISTS iris_db;"

# 3. Recreate InitialCreate migration
cd IRIS.UI
dotnet ef migrations add InitialCreate --project ..\IRIS.Core

# 4. Apply migration
dotnet ef database update --project ..\IRIS.Core
```

---

## ?? Contact Development Team

If issues persist, contact:
- **Josh Lui** (Backend/Scrum Master) - joshedlui4@gmail.com
- **Jansen Choi** (Product Owner) - jansenchoikx@gmail.com

---

**Last Updated:** January 2025  
**Fix Version:** 1.0.0  
**Status:** Migration conflicts resolved ?
