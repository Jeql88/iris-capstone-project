using System.Security.Cryptography;
using System.Text;
using IRIS.Core.Data;
using IRIS.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace IRIS.Core.Services
{
    public class AuthenticationService : IAuthenticationService
    {
        private readonly IRISDbContext _context;
        private User? _currentUser;

        public AuthenticationService(IRISDbContext context)
        {
            _context = context;
        }

        public async Task<User?> AuthenticateAsync(string username, string password)
        {
            try
            {
                if (_context == null)
                    throw new InvalidOperationException("Database context is not available. Please ensure the database is properly configured and running.");

                // Ensure database is created
                await _context.Database.EnsureCreatedAsync();

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == username && u.IsActive);

                if (user == null)
                    return null;

                if (!VerifyPassword(password, user.PasswordHash))
                    return null;

                // Update last login
                user.LastLoginAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _currentUser = user;

                // Log successful login
                await LogUserActionAsync("Login", $"User {username} logged in");

                return user;
            }
            catch (Exception ex)
            {
                // Log the error for debugging
                System.Diagnostics.Debug.WriteLine($"Authentication error: {ex.Message}");
                throw new InvalidOperationException($"Authentication failed: {ex.Message}", ex);
            }
        }

        public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return false;

            if (!VerifyPassword(currentPassword, user.PasswordHash))
                return false;

            user.PasswordHash = HashPassword(newPassword);
            await _context.SaveChangesAsync();

            await LogUserActionAsync("Password Changed", $"User {user.Username} changed password");

            return true;
        }

        public async Task LogoutAsync()
        {
            if (_currentUser != null)
            {
                await LogUserActionAsync("Logout", $"User {_currentUser.Username} logged out");
                _currentUser = null;
            }
        }

        public User? GetCurrentUser() => _currentUser;

        public bool IsAuthenticated => _currentUser != null;

        public bool HasPermission(UserRole requiredRole)
        {
            if (!IsAuthenticated)
                return false;

            // Role hierarchy: SystemAdministrator > ITPersonnel > Faculty
            return _currentUser!.Role switch
            {
                UserRole.SystemAdministrator => true,
                UserRole.ITPersonnel => requiredRole == UserRole.ITPersonnel || requiredRole == UserRole.Faculty,
                UserRole.Faculty => requiredRole == UserRole.Faculty,
                _ => false
            };
        }

        public async Task LogUserActionAsync(string action, string? details = null, int? pcId = null)
        {
            if (_currentUser == null)
                return;

            var log = new UserLog
            {
                UserId = _currentUser.Id,
                PCId = pcId,
                Action = action,
                Details = details,
                IpAddress = GetLocalIPAddress()
            };

            _context.UserLogs.Add(log);
            await _context.SaveChangesAsync();
        }

        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToHexString(hashedBytes).ToLowerInvariant();
        }

        private static bool VerifyPassword(string password, string hash)
        {
            // Hash the input password and compare with stored hash
            return HashPassword(password) == hash;
        }

        private static string GetLocalIPAddress()
        {
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }
            }
            catch
            {
                // Ignore exceptions
            }
            return "Unknown";
        }
    }
}