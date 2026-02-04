using IRIS.Core.Models;

namespace IRIS.Core.Services.Contracts
{
    public interface IUserManagementService
    {
        Task<User> CreateUserAsync(string username, string password, UserRole role, string? fullName = null);
        Task<bool> UpdateUserAsync(int userId, string? username = null, string? fullName = null, UserRole? role = null);
        Task<bool> DeleteUserAsync(int userId);
        Task<User?> GetUserByIdAsync(int userId);
        Task<IEnumerable<User>> GetAllUsersAsync();
        Task<PaginatedResult<User>> GetUsersAsync(int pageNumber = 1, int pageSize = 10, string? search = null, UserRole? role = null, bool? isActive = null);
        Task<IEnumerable<User>> GetUsersByRoleAsync(UserRole role);
        Task<bool> UsernameExistsAsync(string username, int? excludeUserId = null);
    }
}
