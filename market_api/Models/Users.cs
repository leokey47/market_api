using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace market_api.Models
{
    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("username")]
        public string Username { get; set; } = string.Empty;

        [BsonElement("email")]
        public string Email { get; set; } = string.Empty;

        [BsonElement("passwordHash")]
        public string PasswordHash { get; set; } = string.Empty;

        [BsonElement("role")]
        public string Role { get; set; } = "user"; // Роль пользователя

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // Дата создания

        [BsonElement("profileImageUrl")]
        public string? ProfileImageUrl { get; set; }

        // Бизнес-аккаунт поля
        [BsonElement("isBusiness")]
        public bool IsBusiness { get; set; } = false;

        [BsonElement("companyName")]
        public string? CompanyName { get; set; }

        [BsonElement("companyAvatar")]
        public string? CompanyAvatar { get; set; }

        [BsonElement("companyDescription")]
        public string? CompanyDescription { get; set; }

        // For backward compatibility, map the old UserId property to the new Id
        [BsonIgnore]
        public string UserId => Id ?? string.Empty;
    }
}