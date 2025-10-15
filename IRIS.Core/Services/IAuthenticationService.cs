using IRIS.Core.Models;

namespace IRIS.Core.Services
{
    public interface IAuthenticationService
    {
        Task<User?> AuthenticateAsync(string username, string password);
        Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword);
        Task LogoutAsync();
        User? GetCurrentUser();
        bool IsAuthenticated { get; }
        bool HasPermission(UserRole requiredRole);
        Task LogUserActionAsync(string action, string? details = null, int? pcId = null);
    }
}