using market_api.Models;

namespace market_api.Services
{
    public interface IUserService
    {
        // New MongoDB methods
        Task<User?> GetUserByIdAsync(string id);
        Task<bool> UpdateUserAsync(string id, string username, string email);
        Task<bool> UpdateAvatarAsync(string id, string imageUrl);

        // Legacy methods for backward compatibility (will throw NotSupportedException)
        User? GetUserById(int id);
        bool UpdateUser(int id, string username, string email);
        bool UpdateAvatar(int id, string imageUrl);
    }
}