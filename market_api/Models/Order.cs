using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace market_api.Models
{
    public class Order
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("userId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string UserId { get; set; }

        [BsonElement("total")]
        [BsonRepresentation(BsonType.Decimal128)]
        public decimal Total { get; set; }

        [BsonElement("status")]
        public string? Status { get; set; }

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("completedAt")]
        public DateTime? CompletedAt { get; set; }

        [BsonElement("paymentId")]
        public string? PaymentId { get; set; }

        [BsonElement("paymentUrl")]
        public string? PaymentUrl { get; set; }

        [BsonElement("paymentCurrency")]
        public string? PaymentCurrency { get; set; }

        // Navigation properties
        [BsonIgnore]
        public User? User { get; set; }

        [BsonIgnore]
        public List<OrderItem> Items { get; set; } = new List<OrderItem>();
    }
}