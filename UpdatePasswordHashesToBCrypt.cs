using Microsoft.EntityFrameworkCore;
using IRIS.Core.Data;

namespace IRIS.Core.Utilities
{
    /// <summary>
    /// Utility to migrate existing SHA256 password hashes to BCrypt
    /// Run this once after deploying the BCrypt changes
    /// </summary>
    public class PasswordMigrationUtility
    {
        public static async Task MigratePasswordsAsync(IRISDbContext context)
        {
            // Get all users with old SHA256 hash format (64 hex characters)
            var users = await context.Users
                .Where(u => u.PasswordHash.Length == 64)
                .ToListAsync();

            foreach (var user in users)
            {
                // Default password is "admin" for all seeded users
                // Users should change their passwords after first login
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin");
            }

            await context.SaveChangesAsync();
            
            Console.WriteLine($"Migrated {users.Count} user passwords to BCrypt");
        }
    }
}
