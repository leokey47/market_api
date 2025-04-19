using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace market_api.Models
{
    public class ExternalLogin
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Provider { get; set; } // "google", "facebook", "instagram"

        [Required]
        [MaxLength(255)]
        public string ProviderKey { get; set; } // The ID given by the provider

        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User User { get; set; }
    }
}