using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LisbergThreads.Api.Models
{
    [Table("Products")]
    public class Product
    {
        public Guid Id { get; set; }
        [Required, MaxLength(200)]
        public string Name { get; set; } = null!;

        public string? Description { get; set; }

        public string? Size { get; set; }

        public decimal Price { get; set; }

        [Range(0, int.MaxValue)]
        public int StockQuantity { get; set; }

        [MaxLength(500)]
        public string? ImageUrl { get; set; }
        
        public ICollection<ProductImage> Images { get; set; }
            = new List<ProductImage>();

        public bool IsActive { get; set; } = true;

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

    }
}
