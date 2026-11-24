namespace LisbergThreads.Contracts.Products;

public sealed record HomeProductsDto(
    List<ProductDto> NewProducts,
    List<ProductDto> PopularProducts);
