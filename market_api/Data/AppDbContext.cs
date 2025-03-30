using market_api.Models;
using Microsoft.EntityFrameworkCore;

namespace market_api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<User> Users { get; set; }
    }
}
