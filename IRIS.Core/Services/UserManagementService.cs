using System.Security.Cryptography;
using System.Text;
using IRIS.Core.Data;
using IRIS.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace IRIS.Core.Services
{
    public class UserManagementService : IUserManagementService
    {
        private readonly IRISDbContext _context;
        private readonly IAuthenticationService _authService;

        public UserManagementService(IRISDbContext context, IAuthenticationService authService)
        {
            _context = context;
            _authService = authService;
        }

        public async Task<User> CreateUserAsync(string username, string password, UserRole role, string? fullName = null)
        {
            if (await UsernameExistsAsync(username))
                throw new InvalidOperationException("Username already exists");

            var user = new User
            {
                Username = username,
                PasswordHash = HashPassword(password),
                Role = role,
                FullName = fullName,
                IsActive = true
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            await _authService.LogUserActionAsync("User Created", $"Created user {username} with role {role}");

            return user;
        }

        public async Task<bool> UpdateUserAsync(int userId, string? fullName = null, bool? isActive = null)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return false;

            if (fullName != null)
                user.FullName = fullName;

            if (isActive.HasValue)
                user.IsActive = isActive.Value;

            await _context.SaveChangesAsync();

            await _authService.LogUserActionAsync("User Updated", $"Updated user {user.Username}");

            return true;
        }

        public async Task<bool> DeleteUserAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return false;

            // Soft delete by deactivating
            user.IsActive = false;
            await _context.SaveChangesAsync();

            await _authService.LogUserActionAsync("User Deleted", $"Deactivated user {user.Username}");

            return true;
        }

        public async Task<User?> GetUserByIdAsync(int userId)
        {
            return await _context.Users.FindAsync(userId);
        }

        public async Task<IEnumerable<User>> GetAllUsersAsync()
        {
            return await _context.Users
                .OrderBy(u => u.Username)
                .ToListAsync();
        }

        public async Task<IEnumerable<User>> GetUsersByRoleAsync(UserRole role)
        {
            return await _context.Users
                .Where(u => u.Role == role)
                .OrderBy(u => u.Username)
                .ToListAsync();
        }

        public async Task<bool> UsernameExistsAsync(string username, int? excludeUserId = null)
        {
            var query = _context.Users.Where(u => u.Username == username);

            if (excludeUserId.HasValue)
                query = query.Where(u => u.Id != excludeUserId.Value);

            return await query.AnyAsync();
        }

        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }
    }
}