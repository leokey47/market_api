using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace market_api.Models
{
    public class Review
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

        [BsonElement("orderId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string OrderId { get; set; }

        [BsonElement("rating")]
        public int Rating { get; set; } // 1-5

        [BsonElement("text")]
        public string Text { get; set; } = string.Empty;

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [BsonIgnore]
        public User? User { get; set; }

        [BsonIgnore]
        public Product? Product { get; set; }

        [BsonIgnore]
        public Order? Order { get; set; }
    }
}