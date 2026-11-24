using System.ComponentModel.DataAnnotations;

namespace LisbergThreads.Contracts.Products;

public sealed record UpdateProductFieldDto(
    [Required] string Field,
    [StringLength(10000)] string? Value,
    [Required] string RowVersionBase64);
