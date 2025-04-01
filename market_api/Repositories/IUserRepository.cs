using market_api.Models;

namespace market_api.Repositories
{
    public interface IUserRepository
    {
        User? GetUserById(int id);
    }
}
