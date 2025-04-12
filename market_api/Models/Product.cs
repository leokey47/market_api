using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

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

        // Main image URL (for backward compatibility)
        public string ImageUrl { get; set; }

        [Required]
        [MaxLength(100)]
        public string Category { get; set; }

        // Navigation properties for related entities
        public virtual ICollection<ProductPhoto> Photos { get; set; } = new List<ProductPhoto>();

        public virtual ICollection<ProductSpecification> Specifications { get; set; } = new List<ProductSpecification>();
    }
}