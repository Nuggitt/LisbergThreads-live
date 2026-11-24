using System.ComponentModel.DataAnnotations;

namespace LisbergThreads.Api.Models;

public class ProductImage
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    
    [Required, MaxLength(500)]
    public string ImageUrl { get; set; } = string.Empty;
    
    public int SortOrder { get; set; }
    
    public Product? Product { get; set; }
}