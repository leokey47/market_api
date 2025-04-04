using market_api.Models;

namespace market_api.Services
{
    public interface IUserService
    {
        User? GetUserById(int id);
        bool UpdateUser(int id, string username, string email);
        bool UpdateAvatar(int id, string imageUrl);
    }
}