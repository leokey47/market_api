using MongoDB.Driver;
using market_api.Data;
using market_api.Models;

namespace market_api.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly MongoDbContext _context;

        public UserRepository(MongoDbContext context)
        {
            _context = context;
        }

        public async Task<User?> GetUserByIdAsync(string id)
        {
            try
            {
                return await _context.Users.Find(u => u.Id == id).FirstOrDefaultAsync();
            }
            catch
            {
                return null;
            }
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            try
            {
                return await _context.Users.Find(u => u.Email == email).FirstOrDefaultAsync();
            }
            catch
            {
                return null;
            }
        }

        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            try
            {
                return await _context.Users.Find(u => u.Username == username).FirstOrDefaultAsync();
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<User>> GetAllUsersAsync()
        {
            try
            {
                return await _context.Users.Find(_ => true).ToListAsync();
            }
            catch
            {
                return new List<User>();
            }
        }

        public async Task<User> CreateUserAsync(User user)
        {
            await _context.Users.InsertOneAsync(user);
            return user;
        }

        public async Task<bool> UpdateUserAsync(User user)
        {
            try
            {
                var result = await _context.Users.ReplaceOneAsync(u => u.Id == user.Id, user);
                return result.ModifiedCount > 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> DeleteUserAsync(string id)
        {
            try
            {
                var result = await _context.Users.DeleteOneAsync(u => u.Id == id);
                return result.DeletedCount > 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UserExistsAsync(string id)
        {
            try
            {
                var count = await _context.Users.CountDocumentsAsync(u => u.Id == id);
                return count > 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> EmailExistsAsync(string email, string? excludeUserId = null)
        {
            try
            {
                var filter = Builders<User>.Filter.Eq(u => u.Email, email);
                if (!string.IsNullOrEmpty(excludeUserId))
                {
                    filter = filter & Builders<User>.Filter.Ne(u => u.Id, excludeUserId);
                }
                var count = await _context.Users.CountDocumentsAsync(filter);
                return count > 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UsernameExistsAsync(string username, string? excludeUserId = null)
        {
            try
            {
                var filter = Builders<User>.Filter.Eq(u => u.Username, username);
                if (!string.IsNullOrEmpty(excludeUserId))
                {
                    filter = filter & Builders<User>.Filter.Ne(u => u.Id, excludeUserId);
                }
                var count = await _context.Users.CountDocumentsAsync(filter);
                return count > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}