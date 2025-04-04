using market_api.Models;
using market_api.Repositories;
using market_api.Data;

namespace market_api.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly AppDbContext _context;

        public UserService(IUserRepository userRepository, AppDbContext context)
        {
            _userRepository = userRepository;
            _context = context;
        }

        public User? GetUserById(int id)
        {
            return _userRepository.GetUserById(id);
        }

        public bool UpdateUser(int id, string username, string email)
        {
            var user = _context.Users.Find(id);
            if (user == null)
            {
                return false;
            }

            // Check if username is taken
            if (!string.IsNullOrEmpty(username) && username != user.Username)
            {
                var existingUser = _context.Users.FirstOrDefault(u => u.Username == username);
                if (existingUser != null)
                {
                    return false;
                }
                user.Username = username;
            }

            // Check if email is taken
            if (!string.IsNullOrEmpty(email) && email != user.Email)
            {
                var existingUser = _context.Users.FirstOrDefault(u => u.Email == email);
                if (existingUser != null)
                {
                    return false;
                }
                user.Email = email;
            }

            _context.SaveChanges();
            return true;
        }

        public bool UpdateAvatar(int id, string imageUrl)
        {
            var user = _context.Users.Find(id);
            if (user == null)
            {
                return false;
            }

            user.ProfileImageUrl = imageUrl;
            _context.SaveChanges();
            return true;
        }
    }
}