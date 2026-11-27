using LisbergThreads.Api.Data;
using LisbergThreads.Api.Mapping;
using LisbergThreads.Api.Models;
using LisbergThreads.Api.Services.Errors;
using LisbergThreads.Contracts.Orders;
using LisbergThreads.Contracts.Products;
using Microsoft.EntityFrameworkCore;

namespace LisbergThreads.Api.Services.Products;

public sealed class ProductService(AppDbContext db)
{
    public Task<List<ProductDto>> GetActiveAsync() =>
        db.Products.Where(p => p.IsActive).Select(ProductMappings.Projection).ToListAsync();

    public Task<List<ProductDto>> GetAllIncludingInactiveAsync() =>
        db.Products.Select(ProductMappings.Projection).ToListAsync();

    public Task<ProductDto?> GetByIdAsync(Guid id) =>
        db.Products.Where(p => p.Id == id).Select(ProductMappings.Projection).SingleOrDefaultAsync();

    public async Task<HomeProductsDto> GetHomeProductsAsync(CancellationToken cancellationToken)
    {
        var newestIds = await db.Products
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .Select(p => p.Id)
            .Take(6)
            .ToListAsync(cancellationToken);

        var popularIds = await (
            from item in db.OrderItems
            join order in db.Orders on item.OrderId equals order.Id
            join product in db.Products on item.ProductId equals product.Id
            where product.IsActive
                && OrderStatuses.All.Contains(order.Status)
                && item.Quantity > 0
            group item by item.ProductId into productOrders
            orderby productOrders.Sum(item => (long)item.Quantity) descending, productOrders.Key
            select productOrders.Key)
            .Take(6)
            .ToListAsync(cancellationToken);

        var productIds = newestIds.Concat(popularIds).Distinct().ToList();
        var products = await db.Products
            .AsNoTracking()
            .Where(p => p.IsActive && productIds.Contains(p.Id))
            .Select(ProductMappings.Projection)
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        // Keep the ranking, and omit products deactivated between the queries.
        return new HomeProductsDto(
            newestIds.Where(products.ContainsKey).Select(id => products[id]).ToList(),
            popularIds.Where(products.ContainsKey).Select(id => products[id]).ToList());
    }

    public async Task<ProductDto> CreateAsync(CreateProductDto request)
    {
        var valid = ProductValidator.Validate(request.Name, request.Description, request.Size, request.Price,
            request.StockQuantity, request.ImageUrl, request.AdditionalImageUrls);
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = valid.Name,
            Description = valid.Description,
            Size = valid.Size,
            Price = valid.Price,
            StockQuantity = valid.StockQuantity,
            ImageUrl = valid.ImageUrl,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        product.Images = CreateImages(product.Id, valid.AdditionalImageUrls ?? []);

        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.ToDto();
    }

    public async Task<ProductDto> UpdateAsync(Guid id, UpdateProductDto request)
    {
        var valid = ProductValidator.Validate(request.Name, request.Description, request.Size, request.Price,
            request.StockQuantity, request.ImageUrl, request.AdditionalImageUrls);
        var product = await db.Products.Include(p => p.Images).SingleOrDefaultAsync(p => p.Id == id)
            ?? throw new ServiceException(ServiceError.NotFound);

        // Optimistic concurrency: the client sends the RowVersion from its latest GET.
        if (!string.IsNullOrWhiteSpace(request.RowVersionBase64))
            db.Entry(product).Property(p => p.RowVersion).OriginalValue = ParseRowVersion(request.RowVersionBase64);

        product.Name = valid.Name;
        product.Description = valid.Description;
        product.Size = valid.Size;
        product.Price = valid.Price;
        product.StockQuantity = valid.StockQuantity;
        product.ImageUrl = valid.ImageUrl;
        product.IsActive = request.IsActive;
        product.UpdatedAt = DateTime.UtcNow;

        // A missing list leaves the existing additional images unchanged.
        if (valid.AdditionalImageUrls is not null)
        {
            db.ProductImages.RemoveRange(product.Images);
            product.Images.Clear();
            foreach (var image in CreateImages(product.Id, valid.AdditionalImageUrls))
            {
                product.Images.Add(image);
                db.ProductImages.Add(image);
            }
        }

        await SaveProductAsync("Produktet er ændret af en anden. Genindlæs siden, og prøv igen.");
        return product.ToDto();
    }

    public async Task<ProductDto> UpdateFieldAsync(Guid id, UpdateProductFieldDto request,
        CancellationToken cancellationToken)
    {
        var version = ParseRowVersion(request.RowVersionBase64);
        var product = await db.Products.Include(p => p.Images)
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new ServiceException(ServiceError.NotFound);
        var value = request.Value?.Trim() ?? string.Empty;
        switch (request.Field)
        {
            case "Name":
                product.Name = ProductValidator.ValidateName(value);
                break;
            case "Description":
                product.Description = ProductValidator.ValidateDescription(value);
                break;
            case "Size":
                product.Size = ProductValidator.ValidateSize(value);
                break;
            case "Price":
                product.Price = ProductValidator.ParsePrice(value);
                break;
            case "StockQuantity":
                product.StockQuantity = ProductValidator.ParseStock(value);
                break;
            case "IsActive":
                if (!bool.TryParse(value, out var active))
                    throw new ServiceException(ServiceError.Invalid, "Ugyldig produktstatus.");
                product.IsActive = active;
                break;
            default:
                throw new ServiceException(ServiceError.Invalid, "Dette felt kan ikke redigeres.");
        }

        db.Entry(product).Property(p => p.RowVersion).OriginalValue = version;
        product.UpdatedAt = DateTime.UtcNow;
        await SaveProductAsync(null, cancellationToken);
        return product.ToDto();
    }

    // Soft delete: the product disappears from the shop but remains on orders.
    public async Task DeactivateAsync(Guid id)
    {
        var product = await db.Products.FindAsync(id) ?? throw new ServiceException(ServiceError.NotFound);
        product.IsActive = false;
        product.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task ActivateAsync(Guid id)
    {
        var product = await db.Products.FindAsync(id) ?? throw new ServiceException(ServiceError.NotFound);
        if (product.IsActive) return;
        product.IsActive = true;
        product.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task DeletePermanentlyAsync(Guid id)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable);
        var product = await db.Products.SingleOrDefaultAsync(p => p.Id == id)
            ?? throw new ServiceException(ServiceError.NotFound);
        if (product.IsActive)
            throw new ServiceException(ServiceError.Conflict,
                "Produktet skal deaktiveres, før det kan slettes permanent.");
        if (await db.OrderItems.AnyAsync(item => item.ProductId == id))
            throw new ServiceException(ServiceError.Conflict,
                "Produktet kan ikke slettes, fordi det bruges i en ordre eller kurv.");

        db.Products.Remove(product);
        await SaveProductAsync("Produktet er ændret i mellemtiden. Genindlæs siden, før du prøver igen.");
        await transaction.CommitAsync();
    }

    private async Task SaveProductAsync(string? conflictMessage, CancellationToken cancellationToken = default)
    {
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            throw new ServiceException(ServiceError.Conflict, conflictMessage);
        }
    }

    private static byte[] ParseRowVersion(string? base64)
    {
        try
        {
            var version = Convert.FromBase64String(base64 ?? string.Empty);
            if (version.Length == 8) return version;
        }
        catch (FormatException) { }
        throw new ServiceException(ServiceError.Invalid, "Ugyldig version. Genindlæs siden.");
    }

    private static List<ProductImage> CreateImages(Guid productId, List<string> imageUrls) =>
        imageUrls.Select((url, index) => new ProductImage
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            ImageUrl = url,
            SortOrder = index
        }).ToList();
}
