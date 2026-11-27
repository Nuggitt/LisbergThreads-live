using System.Linq.Expressions;
using LisbergThreads.Api.Models;
using LisbergThreads.Contracts.Products;

namespace LisbergThreads.Api.Mapping;

public static class ProductMappings
{
    // Used in database queries; EF Core translates it to SQL.
    public static readonly Expression<Func<Product, ProductDto>> Projection = p => new ProductDto(
        p.Id,
        p.Name,
        p.Description,
        p.Size,
        p.Price,
        p.StockQuantity,
        p.ImageUrl,
        p.IsActive,
        Convert.ToBase64String(p.RowVersion),
        p.Images.OrderBy(i => i.SortOrder).Select(i => i.ImageUrl).ToList());

    private static readonly Func<Product, ProductDto> Compiled = Projection.Compile();

    // For a loaded product; Images must be loaded.
    public static ProductDto ToDto(this Product product) => Compiled(product);
}
