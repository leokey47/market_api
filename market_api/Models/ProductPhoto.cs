using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace market_api.Models
{
    public class ProductPhoto
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string ImageUrl { get; set; }

        // Foreign key relationship with Product
        public int ProductId { get; set; }

        [ForeignKey("ProductId")]
        public Product Product { get; set; }

        // Order priority for display (1-5)
        public int DisplayOrder { get; set; }
    }
}