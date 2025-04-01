using market_api.Models;

namespace market_api.Services
{
    public interface IUserService
    {
        User? GetUserById(int id);
    }
}
