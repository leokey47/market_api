// Review.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace market_api.Models
{
    public class Review
    {
        [Key]
        public int ReviewId { get; set; }

        public int UserId { get; set; }
        [ForeignKey("UserId")]
        public User User { get; set; }

        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public Product Product { get; set; }

        public int OrderId { get; set; }
        [ForeignKey("OrderId")]
        public Order Order { get; set; }

        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }

        [Required]
        [MaxLength(1000)]
        public string Text { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}