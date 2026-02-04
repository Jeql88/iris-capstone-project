using IRIS.Core.Data;
using IRIS.Core.Models;
using IRIS.Core.Services.Contracts;
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

                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                    return null;

                if (username.Length > 100)
                    return null;



                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == username && u.IsActive)
                    .ConfigureAwait(false);

                if (user == null)
                    return null;

                if (!VerifyPassword(password, user.PasswordHash))
                    return null;

                // Update last login
                user.LastLoginAt = DateTime.UtcNow;
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (Exception saveEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to update last login: {saveEx.Message}");
                }

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
            try
            {
                if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
                    return false;

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return false;

                if (!VerifyPassword(currentPassword, user.PasswordHash))
                    return false;

                user.PasswordHash = HashPassword(newPassword);
                user.MustChangePassword = false;
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (Exception saveEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to save password change: {saveEx.Message}");
                    return false;
                }

                await LogUserActionAsync("Password Changed", $"User {user.Username} changed password");

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Password change error: {ex.Message}");
                return false;
            }
        }

        public async Task LogoutAsync()
        {
            if (_currentUser != null)
            {
                try
                {
                    await LogUserActionAsync("Logout", $"User {_currentUser.Username} logged out");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to log logout action: {ex.Message}");
                }
                finally
                {
                    _currentUser = null;
                }
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
            if (_currentUser == null || string.IsNullOrWhiteSpace(action))
                return;

            try
            {
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to log user action: {ex.Message}");
            }
        }

        private static string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        private static bool VerifyPassword(string password, string hash)
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(password, hash);
            }
            catch
            {
                return false;
            }
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get local IP address: {ex.Message}");
            }
            return "Unknown";
        }
    }
}