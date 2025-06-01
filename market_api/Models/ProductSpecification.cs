using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace market_api.Models
{
    public class ProductSpecification
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("name")]
        public string Name { get; set; } = string.Empty;

        [BsonElement("value")]
        public string Value { get; set; } = string.Empty;

        [BsonElement("productId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string ProductId { get; set; }

        // Navigation property
        [BsonIgnore]
        public Product? Product { get; set; }
    }
}