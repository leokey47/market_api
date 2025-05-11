using Microsoft.EntityFrameworkCore;
using market_api.Models;

namespace market_api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<WishlistItem> WishlistItems { get; set; }
        public DbSet<ProductPhoto> ProductPhotos { get; set; }
        public DbSet<ProductSpecification> ProductSpecifications { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<Delivery> Deliveries { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<ExternalLogin> ExternalLogins { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure your entities here if needed
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            // Configure CartItem relationships
            modelBuilder.Entity<CartItem>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CartItem>()
                .HasOne(c => c.Product)
                .WithMany()
                .HasForeignKey(c => c.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure WishlistItem relationships
            modelBuilder.Entity<WishlistItem>()
                .HasOne(w => w.User)
                .WithMany()
                .HasForeignKey(w => w.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<WishlistItem>()
                .HasOne(w => w.Product)
                .WithMany()
                .HasForeignKey(w => w.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure the Product photos relationship
            modelBuilder.Entity<Product>()
                .HasMany(p => p.Photos)
                .WithOne(p => p.Product)
                .HasForeignKey(p => p.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure the Product specifications relationship
            modelBuilder.Entity<Product>()
                .HasMany(p => p.Specifications)
                .WithOne(p => p.Product)
                .HasForeignKey(p => p.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Order relationships
            modelBuilder.Entity<Order>()
                .HasOne(o => o.User)
                .WithMany()
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Changed this from Restrict to Cascade to allow product deletion
            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // Changed this from Restrict to Cascade to allow product deletion
            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Product)
                .WithMany()
                .HasForeignKey(oi => oi.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure ExternalLogin relationships
            modelBuilder.Entity<ExternalLogin>()
                .HasOne(el => el.User)
                .WithMany()
                .HasForeignKey(el => el.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Create unique index for provider + providerKey combination
            modelBuilder.Entity<ExternalLogin>()
                .HasIndex(el => new { el.Provider, el.ProviderKey })
                .IsUnique();

            modelBuilder.Entity<Delivery>()
            .HasOne(d => d.Order)
            .WithOne()
            .HasForeignKey<Delivery>(d => d.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        }
    }
}