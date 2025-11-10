string password = "admin";

// Generate 3 different hashes (BCrypt generates unique hash each time due to random salt)
string hash1 = BCrypt.Net.BCrypt.HashPassword(password);
string hash2 = BCrypt.Net.BCrypt.HashPassword(password);
string hash3 = BCrypt.Net.BCrypt.HashPassword(password);

Console.WriteLine("BCrypt hashes for password 'admin':");
Console.WriteLine($"Hash 1 (admin user): {hash1}");
Console.WriteLine($"Hash 2 (itperson user): {hash2}");
Console.WriteLine($"Hash 3 (faculty user): {hash3}");

// Verify they all work
Console.WriteLine($"\nVerify hash1: {BCrypt.Net.BCrypt.Verify(password, hash1)}");
Console.WriteLine($"Verify hash2: {BCrypt.Net.BCrypt.Verify(password, hash2)}");
Console.WriteLine($"Verify hash3: {BCrypt.Net.BCrypt.Verify(password, hash3)}");
