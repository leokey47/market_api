using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace market_api.Models
{
    public class ProductPhoto
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("imageUrl")]
        public string ImageUrl { get; set; } = string.Empty;

        [BsonElement("productId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string ProductId { get; set; }

        [BsonElement("displayOrder")]
        public int DisplayOrder { get; set; } // Order priority for display (1-5)

        // Navigation property
        [BsonIgnore]
        public Product? Product { get; set; }
    }
}