using market_api.Models;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

public class Order
{
    [Key]
    public int OrderId { get; set; }

    public int UserId { get; set; }
    [ForeignKey("UserId")]
    public User User { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Total { get; set; }

    [MaxLength(50)]
    public string? Status { get; set; }  // Добавляем nullable

    [Required]
    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    [MaxLength(255)]
    public string? PaymentId { get; set; }  // Делаем nullable

    [MaxLength(500)]
    public string? PaymentUrl { get; set; }  // Делаем nullable

    [MaxLength(10)]
    public string? PaymentCurrency { get; set; }  // Делаем nullable

    // Navigation property
    public virtual ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}