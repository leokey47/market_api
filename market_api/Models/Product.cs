using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace market_api.Models
{
    public class Product
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("name")]
        public string Name { get; set; } = string.Empty;

        [BsonElement("description")]
        public string Description { get; set; } = string.Empty;

        [BsonElement("price")]
        [BsonRepresentation(BsonType.Decimal128)]
        public decimal Price { get; set; }

        [BsonElement("imageUrl")]
        public string ImageUrl { get; set; } = string.Empty; // Main image URL (for backward compatibility)

        [BsonElement("category")]
        public string Category { get; set; } = string.Empty;

        [BsonElement("businessOwnerId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? BusinessOwnerId { get; set; } // ID владельца бизнес-аккаунта

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updatedAt")]
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties (will be populated manually in services)
        [BsonIgnore]
        public User? BusinessOwner { get; set; }

        [BsonIgnore]
        public List<ProductPhoto> Photos { get; set; } = new List<ProductPhoto>();

        [BsonIgnore]
        public List<ProductSpecification> Specifications { get; set; } = new List<ProductSpecification>();
    }
}