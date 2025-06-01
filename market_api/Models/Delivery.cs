using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace market_api.Models
{
    public class Delivery
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("orderId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string OrderId { get; set; }

        [BsonElement("deliveryMethod")]
        public string DeliveryMethod { get; set; } = string.Empty; // "NovaPoshta", "UkrPoshta", "Meest"

        [BsonElement("deliveryType")]
        public string DeliveryType { get; set; } = string.Empty; // "Warehouse", "Courier", "PostOffice"

        [BsonElement("recipientFullName")]
        public string RecipientFullName { get; set; } = string.Empty;

        [BsonElement("recipientPhone")]
        public string RecipientPhone { get; set; } = string.Empty;

        [BsonElement("cityRef")]
        public string? CityRef { get; set; }

        [BsonElement("cityName")]
        public string? CityName { get; set; }

        [BsonElement("warehouseRef")]
        public string? WarehouseRef { get; set; }

        [BsonElement("warehouseAddress")]
        public string? WarehouseAddress { get; set; }

        [BsonElement("deliveryAddress")]
        public string? DeliveryAddress { get; set; }

        [BsonElement("trackingNumber")]
        public string? TrackingNumber { get; set; }

        [BsonElement("deliveryCost")]
        [BsonRepresentation(BsonType.Decimal128)]
        public decimal DeliveryCost { get; set; }

        [BsonElement("estimatedDeliveryDate")]
        public DateTime? EstimatedDeliveryDate { get; set; }

        [BsonElement("deliveryStatus")]
        public string DeliveryStatus { get; set; } = "Pending"; // Pending, InTransit, Delivered, Failed

        [BsonElement("deliveryData")]
        public string? DeliveryData { get; set; } // JSON data

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updatedAt")]
        public DateTime? UpdatedAt { get; set; }

        // Navigation property
        [BsonIgnore]
        public Order? Order { get; set; }
    }
}