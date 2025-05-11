using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace market_api.Models
{
    public class Delivery
    {
        [Key]
        public int DeliveryId { get; set; }

        // ID заказа, к которому привязана доставка
        public int OrderId { get; set; }
        [ForeignKey("OrderId")]
        public Order Order { get; set; }

        // Метод доставки: "NovaPoshta", "UkrPoshta", "Meest" и т.д.
        [Required]
        [MaxLength(50)]
        public string DeliveryMethod { get; set; }

        // Способ доставки: "Warehouse", "Courier", "PostOffice" и т.д.
        [Required]
        [MaxLength(50)]
        public string DeliveryType { get; set; }

        // Данные получателя
        [Required]
        [MaxLength(100)]
        public string RecipientFullName { get; set; }

        [Required]
        [MaxLength(20)]
        public string RecipientPhone { get; set; }

        // Для отделений Новой почты
        [MaxLength(36)]
        public string CityRef { get; set; }

        [MaxLength(100)]
        public string CityName { get; set; }

        [MaxLength(36)]
        public string WarehouseRef { get; set; }

        [MaxLength(255)]
        public string WarehouseAddress { get; set; }

        // Для адресной доставки
        [MaxLength(255)]
        public string DeliveryAddress { get; set; }

        // Для трекинга посылки
        [MaxLength(50)]
        public string TrackingNumber { get; set; }

        // Стоимость доставки
        [Column(TypeName = "decimal(18,2)")]
        public decimal DeliveryCost { get; set; }

        // Ожидаемая дата доставки
        public DateTime? EstimatedDeliveryDate { get; set; }

        // Статус доставки
        [MaxLength(50)]
        public string DeliveryStatus { get; set; } = "Pending"; // Pending, InTransit, Delivered, Failed и т.д.

        // Дополнительные данные в JSON формате (для хранения ответов API)
        public string DeliveryData { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}