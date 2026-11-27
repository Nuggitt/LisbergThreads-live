using System.Globalization;
using LisbergThreads.Api.Services.Errors;

namespace LisbergThreads.Api.Services.Products;

public sealed record ValidProduct(
    string Name,
    string? Description,
    string? Size,
    decimal Price,
    int StockQuantity,
    string? ImageUrl,
    List<string>? AdditionalImageUrls);

// The single set of product rules, used by create, update and single-field edits.
public static class ProductValidator
{
    private const int NameMaxLength = 200; // Product.Name [MaxLength(200)]
    private const int DescriptionMaxLength = 10000;
    private const int SizeMaxLength = 100;
    private const int ImageUrlMaxLength = 500; // ProductImages.ImageUrl HasMaxLength(500)
    private const int MaxAdditionalImages = 5;
    private const decimal MaxPrice = 9999999999999999.99m; // decimal(18,2)
    private const string PriceMessage = "Prisen skal være positiv eller 0 og have højst to decimaler.";
    private const string StockMessage = "Lagerantal skal være et helt tal på mindst 0.";

    // Returns trimmed values. AdditionalImageUrls stays null when the request did not send the list.
    public static ValidProduct Validate(string? name, string? description, string? size, decimal price,
        int stockQuantity, string? imageUrl, List<string>? additionalImageUrls)
    {
        var validName = ValidateName(name);
        var validDescription = ValidateDescription(description);
        var validSize = ValidateSize(size);
        ValidatePrice(price);
        ValidateStock(stockQuantity);
        var mainImage = imageUrl?.Trim();
        if (!string.IsNullOrEmpty(mainImage) && !IsValidImageUrl(mainImage))
            throw Invalid("Hovedbilledet skal være en uploadet billedadresse "
                + "eller en gyldig HTTP/HTTPS-adresse på højst 500 tegn.");
        if (additionalImageUrls?.Count > MaxAdditionalImages)
            throw Invalid("Et produkt må højst have 5 ekstra billeder.");
        if (additionalImageUrls?.Any(url => !IsValidImageUrl(url?.Trim())) == true)
            throw Invalid("Ekstra billeder skal have gyldige billedadresser på højst 500 tegn.");

        var additionalImages = additionalImageUrls?
            .Select(url => url.Trim())
            .Where(url => !string.Equals(url, mainImage, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return new ValidProduct(validName, validDescription, validSize, price, stockQuantity, mainImage,
            additionalImages);
    }

    public static string ValidateName(string? name)
    {
        var value = name?.Trim() ?? string.Empty;
        if (value.Length is < 1 or > NameMaxLength)
            throw Invalid("Navnet skal indeholde 1–200 tegn.");
        return value;
    }

    public static string? ValidateDescription(string? description)
    {
        var value = description?.Trim();
        if (value?.Length > DescriptionMaxLength)
            throw Invalid("Beskrivelsen må højst være 10000 tegn.");
        return value;
    }

    public static string? ValidateSize(string? size)
    {
        var value = size?.Trim();
        if (value?.Length > SizeMaxLength)
            throw Invalid("Størrelsen må højst være 100 tegn.");
        return value;
    }

    public static decimal ParsePrice(string value)
    {
        if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var price))
            throw Invalid(PriceMessage);
        ValidatePrice(price);
        return price;
    }

    public static int ParseStock(string value)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var stock))
            throw Invalid(StockMessage);
        ValidateStock(stock);
        return stock;
    }

    private static void ValidatePrice(decimal price)
    {
        if (price < 0 || price > MaxPrice || decimal.Round(price, 2) != price)
            throw Invalid(PriceMessage);
    }

    private static void ValidateStock(int stock)
    {
        if (stock < 0) throw Invalid(StockMessage);
    }

    private static bool IsValidImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl) || imageUrl.Length > ImageUrlMaxLength) return false;
        if (imageUrl.StartsWith("/uploads/products/", StringComparison.OrdinalIgnoreCase))
            return !imageUrl.Contains("..") && !imageUrl.Contains('\\');
        return Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static ServiceException Invalid(string message) => new(ServiceError.Invalid, message);
}
