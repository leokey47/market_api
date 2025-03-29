using System.ComponentModel.DataAnnotations;
namespace market_api.Models
{

    public class Product
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string Name { get; set; }

        public string Description { get; set; }

        [Required]
        public decimal Price { get; set; }

        public string ImageUrl { get; set; }

        [Required]
        [MaxLength(100)]
        public string Category { get; set; }
    }
}