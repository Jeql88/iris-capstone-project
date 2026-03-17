using IRIS.Core.Data;
using IRIS.Core.Models;
using IRIS.Core.Services.Contracts;
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
                IsActive = true,
                MustChangePassword = true
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            await _authService.LogUserActionAsync("User Created", $"Created user {username} with role {role}");

            return user;
        }

        public async Task<bool> UpdateUserAsync(int userId, string? username = null, string? fullName = null, UserRole? role = null)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return false;

            if (username != null && username != user.Username)
            {
                if (await UsernameExistsAsync(username, userId))
                    throw new InvalidOperationException("Username already exists");
                user.Username = username;
            }

            if (fullName != null)
                user.FullName = fullName;

            if (role.HasValue)
                user.Role = role.Value;

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
                .Where(u => u.IsActive)
                .OrderBy(u => u.Username)
                .ToListAsync();
        }

        public async Task<PaginatedResult<User>> GetUsersAsync(int pageNumber = 1, int pageSize = 10, string? search = null, UserRole? role = null)
        {
            var query = _context.Users
                .Where(u => u.IsActive)
                .AsQueryable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                var normalizedSearch = search.Trim().ToLower();
                query = query.Where(u => u.Username.ToLower().Contains(normalizedSearch) ||
                                        (u.FullName != null && u.FullName.ToLower().Contains(normalizedSearch)));
            }

            // Apply role filter
            if (role.HasValue)
            {
                query = query.Where(u => u.Role == role.Value);
            }

            var totalCount = await query.CountAsync();

            var users = await query
                .OrderBy(u => u.Username)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PaginatedResult<User>
            {
                Items = users,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<IEnumerable<User>> GetUsersByRoleAsync(UserRole role)
        {
            return await _context.Users
                .Where(u => u.Role == role && u.IsActive)
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

        //Use Bcrypt for password hashing instead of SHA256
        private static string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }
    }
}