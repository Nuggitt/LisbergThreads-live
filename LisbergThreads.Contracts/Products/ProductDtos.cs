namespace LisbergThreads.Contracts.Products;

public sealed record ProductDto(
    Guid Id,
    string Name,
    string? Description,
    string? Size,
    decimal Price,
    int StockQuantity,
    string? ImageUrl,
    bool IsActive,
    string RowVersionBase64,
    List<string>? AdditionalImageUrls = null);

public sealed record CreateProductDto(
    string Name,
    string? Description,
    string? Size,
    decimal Price,
    int StockQuantity,
    string? ImageUrl,
    bool IsActive,
    List<string>? AdditionalImageUrls = null);

public sealed record UpdateProductDto(
    string Name,
    string? Description,
    string? Size,
    decimal Price,
    int StockQuantity,
    string? ImageUrl,
    bool IsActive,
    string RowVersionBase64,
    List<string>? AdditionalImageUrls = null);