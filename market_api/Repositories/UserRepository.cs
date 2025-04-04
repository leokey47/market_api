using market_api.Data;
using market_api.Models;
using System.Linq;

namespace market_api.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _context;

        public UserRepository(AppDbContext context)
        {
            _context = context;
        }

        public User? GetUserById(int id)
        {
            return _context.Users.FirstOrDefault(u => u.UserId == id);
        }

        public bool UpdateUser(User user)
        {
            try
            {
                _context.Users.Update(user);
                _context.SaveChanges();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}