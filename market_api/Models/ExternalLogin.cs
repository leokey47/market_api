using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace market_api.Models
{
    public class ExternalLogin
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("provider")]
        public string Provider { get; set; } = string.Empty; // "google", "facebook", "instagram"

        [BsonElement("providerKey")]
        public string ProviderKey { get; set; } = string.Empty; // The ID given by the provider

        [BsonElement("userId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string UserId { get; set; }

        // Navigation property
        [BsonIgnore]
        public User? User { get; set; }
    }
}