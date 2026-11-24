using System.Globalization;
using LisbergThreads.Api.Data;
using LisbergThreads.Api.Models;
using LisbergThreads.Contracts.Products;
using LisbergThreads.Contracts.Orders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace LisbergThreads.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProductsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IEnumerable<ProductDto>> GetAll()
    {
        var products = await _db.Products
            .Where(p => p.IsActive)
            .Select(p => new ProductDto(
                p.Id,
                p.Name,
                p.Description,
                p.Size,
                p.Price,
                p.StockQuantity,
                p.ImageUrl,
                p.IsActive,
                Convert.ToBase64String(p.RowVersion),
                p.Images
                    .OrderBy(i => i.SortOrder)
                    .Select(i => i.ImageUrl)
                    .ToList()
            ))
            .ToListAsync();

        return products;
    }

    [HttpGet("home")]
    public async Task<HomeProductsDto> GetHomeProducts(CancellationToken cancellationToken)
    {
        var newestIds = await _db.Products
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .Select(p => p.Id)
            .Take(6)
            .ToListAsync(cancellationToken);

        var popularIds = await (
            from item in _db.OrderItems
            join order in _db.Orders on item.OrderId equals order.Id
            join product in _db.Products on item.ProductId equals product.Id
            where product.IsActive
                && OrderStatuses.All.Contains(order.Status)
                && item.Quantity > 0
            group item by item.ProductId into productOrders
            orderby productOrders.Sum(item => (long)item.Quantity) descending, productOrders.Key
            select productOrders.Key)
            .Take(6)
            .ToListAsync(cancellationToken);

        var productIds = newestIds.Concat(popularIds).Distinct().ToList();
        var products = await _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive && productIds.Contains(p.Id))
            .Select(p => new ProductDto(
                p.Id, p.Name, p.Description, p.Size, p.Price,
                p.StockQuantity, p.ImageUrl, p.IsActive,
                Convert.ToBase64String(p.RowVersion),
                p.Images.OrderBy(i => i.SortOrder).Select(i => i.ImageUrl).ToList()))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        // Keep the ranking, and omit products deactivated between the queries.
        return new HomeProductsDto(
            newestIds.Where(products.ContainsKey).Select(id => products[id]).ToList(),
            popularIds.Where(products.ContainsKey).Select(id => products[id]).ToList());
    }

    [HttpGet("all")]
    [Authorize(Roles = "Admin")]
    public async Task<IEnumerable<ProductDto>> GetAllIncludingInactive()
    {
        var products = await _db.Products
            .Select(p => new ProductDto(
                p.Id,
                p.Name,
                p.Description,
                p.Size,
                p.Price,
                p.StockQuantity,
                p.ImageUrl,
                p.IsActive,
                Convert.ToBase64String(p.RowVersion),
                p.Images
                    .OrderBy(i => i.SortOrder)
                    .Select(i => i.ImageUrl)
                    .ToList()
            ))
            .ToListAsync();

        return products;
    }


    [HttpPatch("{id:guid}/field")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ProductDto>> UpdateField(
        Guid id, UpdateProductFieldDto request, CancellationToken cancellationToken)
    {
        byte[] version;
        try { version = Convert.FromBase64String(request.RowVersionBase64); }
        catch (FormatException) { return BadRequest("Ugyldig version. Genindlæs siden."); }
        if (version.Length != 8) return BadRequest("Ugyldig version. Genindlæs siden.");

        var product = await _db.Products.Include(p => p.Images)
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null) return NotFound();
        var value = request.Value?.Trim() ?? string.Empty;
        switch (request.Field)
        {
            case "Name":
                if (value.Length is < 1 or > 200) return BadRequest("Navnet skal indeholde 1–200 tegn.");
                product.Name = value;
                break;
            case "Description":
                if (value.Length > 10000) return BadRequest("Beskrivelsen må højst være 10000 tegn.");
                product.Description = value;
                break;
            case "Size":
                if (value.Length > 100) return BadRequest("Størrelsen må højst være 100 tegn.");
                product.Size = value;
                break;
            case "Price":
                if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var price)
                    || price < 0 || price > 9999999999999999.99m || decimal.Round(price, 2) != price)
                    return BadRequest("Prisen skal være positiv eller 0 og have højst to decimaler.");
                product.Price = price;
                break;
            case "StockQuantity":
                if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var stock) || stock < 0)
                    return BadRequest("Lagerantal skal være et helt tal på mindst 0.");
                product.StockQuantity = stock;
                break;
            case "IsActive":
                if (!bool.TryParse(value, out var active)) return BadRequest("Ugyldig produktstatus.");
                product.IsActive = active;
                break;
            default:
                return BadRequest("Dette felt kan ikke redigeres.");
        }

        _db.Entry(product).Property(p => p.RowVersion).OriginalValue = version;
        product.UpdatedAt = DateTime.UtcNow;
        try { await _db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Conflict(); }

        return Ok(new ProductDto(product.Id, product.Name, product.Description, product.Size,
            product.Price, product.StockQuantity, product.ImageUrl, product.IsActive,
            Convert.ToBase64String(product.RowVersion),
            product.Images.OrderBy(i => i.SortOrder).Select(i => i.ImageUrl).ToList()));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product is null)
        {
            return NotFound();
        }

        product.IsActive = false;
        product.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}/permanent")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeletePermanently(Guid id)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable);
        var product = await _db.Products.SingleOrDefaultAsync(p => p.Id == id);
        if (product is null) return NotFound();
        if (product.IsActive)
            return Conflict("Produktet skal deaktiveres, før det kan slettes permanent.");
        if (await _db.OrderItems.AnyAsync(item => item.ProductId == id))
            return Conflict("Produktet kan ikke slettes, fordi det bruges i en ordre eller kurv.");

        _db.Products.Remove(product);
        try
        {
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict("Produktet er ændret i mellemtiden. Genindlæs siden, før du prøver igen.");
        }
        return NoContent();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var product = await _db.Products
        .Where(p => p.Id == id)
        .Select(p => new ProductDto(
        p.Id,
        p.Name,
        p.Description,
        p.Size,
        p.Price,
        p.StockQuantity,
        p.ImageUrl,
        p.IsActive,
        Convert.ToBase64String(p.RowVersion),
        p.Images
            .OrderBy(i => i.SortOrder)
            .Select(i => i.ImageUrl)
            .ToList()
        ))
        .SingleOrDefaultAsync();

        if (product is null)
        {
            return NotFound();
        }

        return Ok(product);

    }

    [Authorize(Roles = "Admin")]
    [HttpPatch("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id)
    {
        var product = await _db.Products.FindAsync(id);

        if (product is null)
        {
            return NotFound();
        }

        if (product.IsActive)
        {
            return NoContent();
        }

        product.IsActive = true;
        product.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ProductDto>> Create([FromBody] CreateProductDto request)
    {
        if (request.StockQuantity < 0)
            return BadRequest("Lagerbeholdningen må ikke være negativ.");

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Name is required.");
        }
        
        var imageError = ValidateImages(
            request.ImageUrl,
            request.AdditionalImageUrls);

        if (imageError is not null)
        {
            return BadRequest(imageError);
        }

        var entity = new Product
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Size = request.Size?.Trim(),
            Price = request.Price,
            StockQuantity = request.StockQuantity,
            ImageUrl = request.ImageUrl?.Trim(),
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        var imageUrls = NormalizeAdditionalImages(
            request.AdditionalImageUrls ?? new List<string>(),
            entity.ImageUrl);

        entity.Images = imageUrls
            .Select((url, index) => new ProductImage
            {
                Id = Guid.NewGuid(),
                ProductId = entity.Id,
                ImageUrl = url,
                SortOrder = index
            })
            .ToList();
        
        _db.Products.Add(entity);
        await _db.SaveChangesAsync();

        var dto = new ProductDto(
            entity.Id,
            entity.Name,
            entity.Description,
            entity.Size,
            entity.Price,
            entity.StockQuantity,
            entity.ImageUrl,
            entity.IsActive,
            Convert.ToBase64String(entity.RowVersion),
            entity.Images
                .OrderBy(i => i.SortOrder)
                .Select(i => i.ImageUrl)
                .ToList()
        );

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ProductDto>> Update(Guid id, [FromBody] UpdateProductDto request)
    {
        if (request.StockQuantity < 0)
            return BadRequest("Lagerbeholdningen må ikke være negativ.");

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Name is required.");
        }
        
        var imageError = ValidateImages(
            request.ImageUrl,
            request.AdditionalImageUrls);

        if (imageError is not null)
        {
            return BadRequest(imageError);
        }

        var entity = await _db.Products
            .Include(p => p.Images)
            .SingleOrDefaultAsync(p => p.Id == id);
        if (entity is null)
        {
            return NotFound();
        }

        // 1) Concurrency: klienten sender RowVersion fra sidste GET
        if (!string.IsNullOrWhiteSpace(request.RowVersionBase64))
        {
            var clientRowVersion = Convert.FromBase64String(request.RowVersionBase64);
            _db.Entry(entity).Property(p => p.RowVersion).OriginalValue = clientRowVersion;
        }

        // 2) "Map" request -> entity (det er entity vi gemmer)
        entity.Name = request.Name.Trim();
        entity.Description = request.Description?.Trim();
        entity.Size = request.Size?.Trim();
        entity.Price = request.Price;
        entity.StockQuantity = request.StockQuantity;
        entity.ImageUrl = request.ImageUrl?.Trim();
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        
        if (request.AdditionalImageUrls is not null)
        {
            var imageUrls = NormalizeAdditionalImages(
                request.AdditionalImageUrls,
                entity.ImageUrl);

            _db.ProductImages.RemoveRange(entity.Images);
            entity.Images.Clear();

            foreach (var item in imageUrls.Select((url, index) => new
                     {
                         Url = url,
                         Index = index
                     }))
            {
                var image = new ProductImage
                {
                    Id = Guid.NewGuid(),
                    ProductId = entity.Id,
                    ImageUrl = item.Url,
                    SortOrder = item.Index
                };

                entity.Images.Add(image);
                _db.ProductImages.Add(image);
            }
        }

        // 3) Gem (kan kaste concurrency exception hvis RowVersion ikke matcher)
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict("The product was updated by someone else. Reload and try again.");
        }

        // 4) Returnér ny DTO inkl. ny RowVersion
        return Ok(new ProductDto(
            entity.Id,
            entity.Name,
            entity.Description,
            entity.Size,
            entity.Price,
            entity.StockQuantity,
            entity.ImageUrl,
            entity.IsActive,
            Convert.ToBase64String(entity.RowVersion),
            entity.Images
                .OrderBy(i => i.SortOrder)
                .Select(i => i.ImageUrl)
                .ToList()
        ));
    }
    
    private static bool IsValidImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl) || imageUrl.Length > 500)
        {
            return false;
        }

        if (imageUrl.StartsWith(
                "/uploads/products/",
                StringComparison.OrdinalIgnoreCase))
        {
            return !imageUrl.Contains("..")
                   && !imageUrl.Contains('\\');
        }

        return Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp
                   || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static string? ValidateImages(
        string? mainImageUrl,
        List<string>? additionalImageUrls)
    {
        if (!string.IsNullOrWhiteSpace(mainImageUrl)
            && !IsValidImageUrl(mainImageUrl.Trim()))
        {
            return "Hovedbilledet skal være en uploadet billedadresse "
                   + "eller en gyldig HTTP/HTTPS-adresse på højst 500 tegn.";
        }

        if (additionalImageUrls is null)
        {
            return null;
        }

        if (additionalImageUrls.Count > 5)
        {
            return "Et produkt må højst have 5 ekstra billeder.";
        }

        if (additionalImageUrls.Any(url =>
                !IsValidImageUrl(url?.Trim())))
        {
            return "Ekstra billeder skal have gyldige billedadresser "
                   + "på højst 500 tegn.";
        }

        return null;
    }

    private static List<string> NormalizeAdditionalImages(
        List<string> imageUrls,
        string? mainImageUrl)
    {
        return imageUrls
            .Select(url => url.Trim())
            .Where(url => !string.Equals(
                url,
                mainImageUrl?.Trim(),
                StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}
