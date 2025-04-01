using market_api.Models;
using market_api.Repositories;

namespace market_api.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;

        public UserService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public User? GetUserById(int id)
        {
            return _userRepository.GetUserById(id);
        }
    }
}
