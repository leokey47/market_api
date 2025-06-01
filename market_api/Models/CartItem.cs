using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace market_api.Models
{
    public class CartItem
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("userId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string UserId { get; set; }

        [BsonElement("productId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string ProductId { get; set; }

        [BsonElement("quantity")]
        public int Quantity { get; set; }

        [BsonElement("addedAt")]
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties (will be populated manually in services)
        [BsonIgnore]
        public User? User { get; set; }

        [BsonIgnore]
        public Product? Product { get; set; }
    }
}