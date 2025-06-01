using MongoDB.Driver;
using market_api.Models;

namespace market_api.Data
{
    public class MongoDbContext
    {
        private readonly IMongoDatabase _database;

        public MongoDbContext(IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("MongoConnection");
            var databaseName = configuration.GetValue<string>("DatabaseName") ?? "market_db";

            var client = new MongoClient(connectionString);
            _database = client.GetDatabase(databaseName);

            // Create indexes for better performance
            CreateIndexes();
        }

        // Collections
        public IMongoCollection<User> Users => _database.GetCollection<User>("users");
        public IMongoCollection<Product> Products => _database.GetCollection<Product>("products");
        public IMongoCollection<CartItem> CartItems => _database.GetCollection<CartItem>("cartItems");
        public IMongoCollection<WishlistItem> WishlistItems => _database.GetCollection<WishlistItem>("wishlistItems");
        public IMongoCollection<ProductPhoto> ProductPhotos => _database.GetCollection<ProductPhoto>("productPhotos");
        public IMongoCollection<ProductSpecification> ProductSpecifications => _database.GetCollection<ProductSpecification>("productSpecifications");
        public IMongoCollection<Order> Orders => _database.GetCollection<Order>("orders");
        public IMongoCollection<OrderItem> OrderItems => _database.GetCollection<OrderItem>("orderItems");
        public IMongoCollection<ExternalLogin> ExternalLogins => _database.GetCollection<ExternalLogin>("externalLogins");
        public IMongoCollection<Review> Reviews => _database.GetCollection<Review>("reviews");
        public IMongoCollection<Delivery> Deliveries => _database.GetCollection<Delivery>("deliveries");

        private void CreateIndexes()
        {
            // User indexes
            var userEmailIndex = Builders<User>.IndexKeys.Ascending(u => u.Email);
            var userUsernameIndex = Builders<User>.IndexKeys.Ascending(u => u.Username);
            Users.Indexes.CreateOne(new CreateIndexModel<User>(userEmailIndex, new CreateIndexOptions { Unique = true }));
            Users.Indexes.CreateOne(new CreateIndexModel<User>(userUsernameIndex, new CreateIndexOptions { Unique = true }));

            // Product indexes
            var productCategoryIndex = Builders<Product>.IndexKeys.Ascending(p => p.Category);
            var productNameIndex = Builders<Product>.IndexKeys.Text(p => p.Name).Text(p => p.Description);
            Products.Indexes.CreateOne(new CreateIndexModel<Product>(productCategoryIndex));
            Products.Indexes.CreateOne(new CreateIndexModel<Product>(productNameIndex));

            // CartItem indexes
            var cartUserIndex = Builders<CartItem>.IndexKeys.Ascending(c => c.UserId);
            var cartProductIndex = Builders<CartItem>.IndexKeys.Ascending(c => c.ProductId);
            var cartUserProductIndex = Builders<CartItem>.IndexKeys.Ascending(c => c.UserId).Ascending(c => c.ProductId);
            CartItems.Indexes.CreateOne(new CreateIndexModel<CartItem>(cartUserIndex));
            CartItems.Indexes.CreateOne(new CreateIndexModel<CartItem>(cartProductIndex));
            CartItems.Indexes.CreateOne(new CreateIndexModel<CartItem>(cartUserProductIndex, new CreateIndexOptions { Unique = true }));

            // WishlistItem indexes
            var wishlistUserIndex = Builders<WishlistItem>.IndexKeys.Ascending(w => w.UserId);
            var wishlistProductIndex = Builders<WishlistItem>.IndexKeys.Ascending(w => w.ProductId);
            var wishlistUserProductIndex = Builders<WishlistItem>.IndexKeys.Ascending(w => w.UserId).Ascending(w => w.ProductId);
            WishlistItems.Indexes.CreateOne(new CreateIndexModel<WishlistItem>(wishlistUserIndex));
            WishlistItems.Indexes.CreateOne(new CreateIndexModel<WishlistItem>(wishlistProductIndex));
            WishlistItems.Indexes.CreateOne(new CreateIndexModel<WishlistItem>(wishlistUserProductIndex, new CreateIndexOptions { Unique = true }));

            // Order indexes
            var orderUserIndex = Builders<Order>.IndexKeys.Ascending(o => o.UserId);
            var orderStatusIndex = Builders<Order>.IndexKeys.Ascending(o => o.Status);
            var orderCreatedIndex = Builders<Order>.IndexKeys.Descending(o => o.CreatedAt);
            Orders.Indexes.CreateOne(new CreateIndexModel<Order>(orderUserIndex));
            Orders.Indexes.CreateOne(new CreateIndexModel<Order>(orderStatusIndex));
            Orders.Indexes.CreateOne(new CreateIndexModel<Order>(orderCreatedIndex));

            // OrderItem indexes
            var orderItemOrderIndex = Builders<OrderItem>.IndexKeys.Ascending(oi => oi.OrderId);
            var orderItemProductIndex = Builders<OrderItem>.IndexKeys.Ascending(oi => oi.ProductId);
            OrderItems.Indexes.CreateOne(new CreateIndexModel<OrderItem>(orderItemOrderIndex));
            OrderItems.Indexes.CreateOne(new CreateIndexModel<OrderItem>(orderItemProductIndex));

            // ProductPhoto indexes
            var photoProductIndex = Builders<ProductPhoto>.IndexKeys.Ascending(pp => pp.ProductId);
            var photoDisplayOrderIndex = Builders<ProductPhoto>.IndexKeys.Ascending(pp => pp.ProductId).Ascending(pp => pp.DisplayOrder);
            ProductPhotos.Indexes.CreateOne(new CreateIndexModel<ProductPhoto>(photoProductIndex));
            ProductPhotos.Indexes.CreateOne(new CreateIndexModel<ProductPhoto>(photoDisplayOrderIndex));

            // ProductSpecification indexes
            var specProductIndex = Builders<ProductSpecification>.IndexKeys.Ascending(ps => ps.ProductId);
            ProductSpecifications.Indexes.CreateOne(new CreateIndexModel<ProductSpecification>(specProductIndex));

            // ExternalLogin indexes
            var externalLoginUserIndex = Builders<ExternalLogin>.IndexKeys.Ascending(el => el.UserId);
            var externalLoginProviderIndex = Builders<ExternalLogin>.IndexKeys.Ascending(el => el.Provider).Ascending(el => el.ProviderKey);
            ExternalLogins.Indexes.CreateOne(new CreateIndexModel<ExternalLogin>(externalLoginUserIndex));
            ExternalLogins.Indexes.CreateOne(new CreateIndexModel<ExternalLogin>(externalLoginProviderIndex, new CreateIndexOptions { Unique = true }));

            // Review indexes
            var reviewUserIndex = Builders<Review>.IndexKeys.Ascending(r => r.UserId);
            var reviewProductIndex = Builders<Review>.IndexKeys.Ascending(r => r.ProductId);
            var reviewOrderIndex = Builders<Review>.IndexKeys.Ascending(r => r.OrderId);
            Reviews.Indexes.CreateOne(new CreateIndexModel<Review>(reviewUserIndex));
            Reviews.Indexes.CreateOne(new CreateIndexModel<Review>(reviewProductIndex));
            Reviews.Indexes.CreateOne(new CreateIndexModel<Review>(reviewOrderIndex));

            // Delivery indexes
            var deliveryOrderIndex = Builders<Delivery>.IndexKeys.Ascending(d => d.OrderId);
            var deliveryStatusIndex = Builders<Delivery>.IndexKeys.Ascending(d => d.DeliveryStatus);
            Deliveries.Indexes.CreateOne(new CreateIndexModel<Delivery>(deliveryOrderIndex));
            Deliveries.Indexes.CreateOne(new CreateIndexModel<Delivery>(deliveryStatusIndex));
        }
    }
}