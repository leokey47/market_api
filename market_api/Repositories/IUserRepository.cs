using market_api.Models;

namespace market_api.Repositories
{
    public interface IUserRepository
    {
        Task<User?> GetUserByIdAsync(string id);
        Task<User?> GetUserByEmailAsync(string email);
        Task<User?> GetUserByUsernameAsync(string username);
        Task<List<User>> GetAllUsersAsync();
        Task<User> CreateUserAsync(User user);
        Task<bool> UpdateUserAsync(User user);
        Task<bool> DeleteUserAsync(string id);
        Task<bool> UserExistsAsync(string id);
        Task<bool> EmailExistsAsync(string email, string? excludeUserId = null);
        Task<bool> UsernameExistsAsync(string username, string? excludeUserId = null);
    }
}