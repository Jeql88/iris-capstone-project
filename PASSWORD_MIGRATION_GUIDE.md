# Password Migration Guide: SHA256 to BCrypt

## Overview
The authentication system has been upgraded from SHA256 (insecure) to BCrypt (secure with automatic salting) for password hashing.

## What Changed

### Security Improvements
- **BCrypt with automatic salting**: Each password gets a unique salt
- **Adaptive hashing**: Configurable work factor (default: 11)
- **Rainbow table resistant**: Salting prevents precomputed hash attacks
- **Brute-force resistant**: Slow hashing algorithm makes attacks impractical

### Code Changes
1. Added `BCrypt.Net-Next` package to IRIS.Core
2. Updated `HashPassword()` method to use BCrypt
3. Updated `VerifyPassword()` method to use BCrypt verification
4. Updated database seed data with BCrypt hashes

## Migration Steps

### Option 1: Fresh Database (Recommended for Development)
If you can recreate the database:

```bash
# Drop existing database
dotnet ef database drop --project IRIS.Core --startup-project IRIS.UI

# Apply all migrations including the new one
dotnet ef database update --project IRIS.Core --startup-project IRIS.UI
```

### Option 2: Update Existing Database
If you need to preserve existing data:

#### Step 1: Apply the migration
```bash
cd IRIS.UI
dotnet ef database update --project ..\IRIS.Core
```

#### Step 2: Update existing passwords
Run the provided C# utility or SQL script:

**Using C# Utility (Recommended):**
```csharp
using IRIS.Core.Data;
using IRIS.Core.Utilities;

// In your startup or migration code
await PasswordMigrationUtility.MigratePasswordsAsync(dbContext);
```

**Using SQL Script:**
```bash
# Connect to your PostgreSQL database and run:
psql -U postgres -d iris_db -f UpdatePasswordsToBCrypt.sql
```

## Test Credentials

After migration, all seeded users have the password: **admin**

- Username: `admin` / Password: `admin`
- Username: `itperson` / Password: `admin`
- Username: `faculty` / Password: `admin`

**IMPORTANT**: Users should change their passwords immediately after first login!

## Verification

Test login with the new BCrypt hashing:

```csharp
// The password "admin" should now authenticate successfully
var user = await authService.AuthenticateAsync("admin", "admin");
```

## BCrypt Hash Format

BCrypt hashes look like this:
```
$2a$11$8EqYytf5J07NnC6me1jaAOGPnPfXqXV3Ue6qVnvqZJxqjqjqjqjqm
```

Format breakdown:
- `$2a$` - BCrypt algorithm identifier
- `11$` - Work factor (2^11 iterations)
- Remaining characters - Salt + hash

## Rollback (Emergency Only)

If you need to rollback:

```bash
# Remove the migration
dotnet ef migrations remove --project IRIS.Core --startup-project IRIS.UI

# Restore old code from git
git checkout HEAD -- IRIS.Core/Services/AuthenticationService.cs
git checkout HEAD -- IRIS.Core/Data/IRISDbContext.cs
```

## Security Notes

1. **Never store plaintext passwords** - BCrypt handles this automatically
2. **Work factor**: Default is 11, can be increased for more security (slower)
3. **Password policies**: Consider implementing minimum length, complexity requirements
4. **Force password change**: Require users to change default passwords on first login
5. **Password history**: Consider preventing password reuse

## Troubleshooting

### "Invalid hash" errors
- Ensure all passwords in database are BCrypt format (start with `$2a$`)
- Run the migration utility to convert any remaining SHA256 hashes

### Authentication fails
- Verify BCrypt.Net-Next package is installed
- Check that VerifyPassword is using BCrypt.Verify, not string comparison
- Ensure database has been updated with new hashes

## Additional Resources

- [BCrypt.Net-Next Documentation](https://github.com/BcryptNet/bcrypt.net)
- [OWASP Password Storage Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html)
