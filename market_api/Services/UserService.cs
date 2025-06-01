using market_api.Models;
using market_api.Repositories;
using market_api.Data;

namespace market_api.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly MongoDbContext _context;

        public UserService(IUserRepository userRepository, MongoDbContext context)
        {
            _userRepository = userRepository;
            _context = context;
        }

        public async Task<User?> GetUserByIdAsync(string id)
        {
            return await _userRepository.GetUserByIdAsync(id);
        }

        public async Task<bool> UpdateUserAsync(string id, string username, string email)
        {
            var user = await _userRepository.GetUserByIdAsync(id);
            if (user == null)
            {
                return false;
            }

            // Check if username is taken
            if (!string.IsNullOrEmpty(username) && username != user.Username)
            {
                var usernameExists = await _userRepository.UsernameExistsAsync(username, id);
                if (usernameExists)
                {
                    return false;
                }
                user.Username = username;
            }

            // Check if email is taken
            if (!string.IsNullOrEmpty(email) && email != user.Email)
            {
                var emailExists = await _userRepository.EmailExistsAsync(email, id);
                if (emailExists)
                {
                    return false;
                }
                user.Email = email;
            }

            return await _userRepository.UpdateUserAsync(user);
        }

        public async Task<bool> UpdateAvatarAsync(string id, string imageUrl)
        {
            var user = await _userRepository.GetUserByIdAsync(id);
            if (user == null)
            {
                return false;
            }

            user.ProfileImageUrl = imageUrl;
            return await _userRepository.UpdateUserAsync(user);
        }

        // Legacy methods for backward compatibility
        public User? GetUserById(int id)
        {
            // This is a legacy method, you should use GetUserByIdAsync instead
            throw new NotSupportedException("Use GetUserByIdAsync with string ID instead");
        }

        public bool UpdateUser(int id, string username, string email)
        {
            // This is a legacy method, you should use UpdateUserAsync instead
            throw new NotSupportedException("Use UpdateUserAsync with string ID instead");
        }

        public bool UpdateAvatar(int id, string imageUrl)
        {
            // This is a legacy method, you should use UpdateAvatarAsync instead
            throw new NotSupportedException("Use UpdateAvatarAsync with string ID instead");
        }
    }
}