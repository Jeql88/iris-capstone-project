using Npgsql;
using BCrypt.Net;

// Connection string from appsettings.json
var connectionString = "Host=localhost;Port=5432;Database=iris_db;Username=postgres;Password=password";

Console.WriteLine("=== IRIS Password Verification & Fix Tool ===\n");

try
{
    using var conn = new NpgsqlConnection(connectionString);
    await conn.OpenAsync();
    Console.WriteLine("✓ Connected to database successfully!\n");

    // Get all users and their password hashes
    Console.WriteLine("Current users in database:");
    Console.WriteLine(new string('-', 80));
    
    using var cmd = new NpgsqlCommand("SELECT \"Id\", \"Username\", \"PasswordHash\", \"IsActive\" FROM \"Users\" ORDER BY \"Id\"", conn);
    using var reader = await cmd.ExecuteReaderAsync();
    
    var users = new List<(int Id, string Username, string Hash, bool IsActive)>();
    
    while (await reader.ReadAsync())
    {
        var id = reader.GetInt32(0);
        var username = reader.GetString(1);
        var hash = reader.GetString(2);
        var isActive = reader.GetBoolean(3);
        
        users.Add((id, username, hash, isActive));
        
        // Check if hash is BCrypt format
        var isBCrypt = hash.StartsWith("$2a$") || hash.StartsWith("$2b$") || hash.StartsWith("$2y$");
        
        Console.WriteLine($"ID: {id}, Username: {username}");
        Console.WriteLine($"   Hash: {hash.Substring(0, Math.Min(hash.Length, 60))}...");
        Console.WriteLine($"   Is BCrypt: {isBCrypt}, Active: {isActive}");
        
        // Test with common passwords
        if (isBCrypt)
        {
            var testPasswords = new[] { "admin", "password", "123456", username };
            foreach (var testPwd in testPasswords)
            {
                try
                {
                    if (BCrypt.Net.BCrypt.Verify(testPwd, hash))
                    {
                        Console.WriteLine($"   ✓ Password matches: '{testPwd}'");
                        break;
                    }
                }
                catch { }
            }
        }
        Console.WriteLine();
    }
    
    await reader.CloseAsync();
    
    // Ask if user wants to fix passwords
    Console.WriteLine("\n=== Password Fix ===");
    Console.WriteLine("Would you like to reset all users to password 'admin'? (y/n)");
    var response = Console.ReadLine();
    
    if (response?.ToLower() == "y")
    {
        var newHash = BCrypt.Net.BCrypt.HashPassword("admin");
        Console.WriteLine($"\nNew BCrypt hash for 'admin': {newHash}");
        
        using var updateCmd = new NpgsqlCommand(
            "UPDATE \"Users\" SET \"PasswordHash\" = @hash, \"MustChangePassword\" = true", conn);
        updateCmd.Parameters.AddWithValue("hash", newHash);
        
        var rowsAffected = await updateCmd.ExecuteNonQueryAsync();
        Console.WriteLine($"\n✓ Updated {rowsAffected} user(s) with new password hash.");
        Console.WriteLine("All users can now log in with password: admin");
        Console.WriteLine("They will be required to change their password on first login.");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"\n✗ Error: {ex.Message}");
    Console.WriteLine($"\nFull exception:\n{ex}");
}

Console.WriteLine("\nPress any key to exit...");
Console.ReadKey();
