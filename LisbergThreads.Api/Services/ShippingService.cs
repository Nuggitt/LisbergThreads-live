using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using LisbergThreads.Contracts.Orders;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace LisbergThreads.Api.Services;

public sealed class ShippingService(IHttpClientFactory clients, IOptions<ShippingOptions> options) : IDisposable
{
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 256 });

    public void Dispose() => cache.Dispose();

    private static readonly (string Id, string Name, string Carrier, bool RequiresServicePoint)[] Methods =
    [
        ("dao-parcelshop", "dao · Pakkeshop", "dao", true),
        ("dao-home", "dao · Hjemmelevering", "dao", false),
        ("postnord-parcelshop", "PostNord · Pakkeshop", "PostNord", true),
        ("postnord-home", "PostNord · Hjemmelevering", "PostNord", false),
        ("gls-parcelshop", "GLS · Pakkeshop", "GLS", true),
        ("gls-home", "GLS · Hjemmelevering", "GLS", false)
    ];

    public IReadOnlyList<ShippingMethodDto> GetMethods() => Methods.Select(method =>
    {
        options.Value.Methods.TryGetValue(method.Id, out var settings);
        var validPrice = settings?.Price is >= 0 and <= 10000
            && settings.Price * 100 == decimal.Truncate(settings.Price.Value * 100);
        var available = options.Value.Enabled && validPrice
            && !string.IsNullOrWhiteSpace(options.Value.DeliveryCheckoutKey)
            && Regex.IsMatch(settings?.ProductCode ?? "", "^[A-Z0-9_]{1,60}$");
        return new ShippingMethodDto(method.Id, method.Name, method.Carrier, method.RequiresServicePoint,
            validPrice ? settings!.Price : null, available);
    }).ToList();

    public bool IsAvailable => GetMethods().Any(method => method.IsAvailable);

    public async Task<IReadOnlyList<ShippingServicePointDto>> GetServicePointsAsync(string methodId,
        string postalCode, string address, string city, CancellationToken cancellationToken, bool refresh = false)
    {
        var method = RequireMethod(methodId);
        if (!method.RequiresServicePoint)
            throw new CheckoutRejectedException(400, "Denne leveringsform bruger ikke en pakkeshop.");
        ValidateAddress(postalCode, address, city);
        postalCode = postalCode.Trim(); address = address.Trim(); city = city.Trim();
        var productCode = options.Value.Methods[method.Id].ProductCode;
        var cacheKey = ("shipping", productCode, postalCode, address, city);
        if (!refresh && cache.TryGetValue(cacheKey, out IReadOnlyList<ShippingServicePointDto>? cached))
            return cached!;

        // Shipmondo requires its restricted Delivery Checkout key in the query string.
        // Logging is disabled for this named HttpClient so the key and customer address are not logged.
        var query = new Dictionary<string, string>
        {
            ["frontend_key"] = options.Value.DeliveryCheckoutKey,
            ["product_code"] = productCode, ["country_code"] = "DK", ["zipcode"] = postalCode,
            ["address"] = address, ["city"] = city, ["quantity"] = "20"
        };
        var url = "service_point/service_points?" + string.Join("&", query.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        try
        {
            using var response = await clients.CreateClient("Shipmondo").GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new CheckoutRejectedException(503, "Pakkeshops kan ikke hentes lige nu. Prøv igen senere eller vælg hjemmelevering.");
            var points = await response.Content.ReadFromJsonAsync<List<ShipmondoServicePoint>>(cancellationToken)
                ?? throw new JsonException();
            IReadOnlyList<ShippingServicePointDto> result = points
                .Where(point => point.PickupAllowed && point.Country == "DK"
                    && ValidText(point.Id, 100) && ValidText(point.Name, 254) && ValidText(point.Address, 254)
                    && (point.Address2 is null || point.Address2.Length <= 254)
                    && Regex.IsMatch(point.Zipcode ?? "", "^[0-9]{4}$") && ValidText(point.City, 254))
                .Take(20).Select(point => new ShippingServicePointDto(point.Id!, point.Name!, point.Address!,
                    point.Address2, point.Zipcode!, point.City!)).ToList();
            cache.Set(cacheKey, result, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5), Size = 1
            });
            return result;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // Do not propagate the upstream exception: it may contain the private query string.
            throw new CheckoutRejectedException(503, "Pakkeshops kan ikke hentes lige nu. Prøv igen senere eller vælg hjemmelevering.");
        }
    }

    public async Task<OrderShippingDto> ResolveAsync(ShippingSelectionDto? selection, string postalCode,
        string address, string city, CancellationToken cancellationToken)
    {
        if (selection is null)
            throw new CheckoutRejectedException(400, "Vælg levering, før du fortsætter til betaling.");
        var method = RequireMethod(selection.MethodId);
        ValidateAddress(postalCode, address, city);
        if (selection.ExpectedPrice != method.Price)
            throw new CheckoutRejectedException(409, "Fragtprisen er ændret. Genindlæs checkout og kontrollér beløbet.");
        ShippingServicePointDto? point = null;
        if (method.RequiresServicePoint)
        {
            if (!ValidText(selection.ServicePointId, 100))
                throw new CheckoutRejectedException(400, "Vælg en pakkeshop.");
            // Recheck with Shipmondo before starting the database transaction and payment.
            var points = await GetServicePointsAsync(method.Id, postalCode, address, city, cancellationToken, refresh: true);
            point = points.FirstOrDefault(point => point.Id == selection.ServicePointId)
                ?? throw new CheckoutRejectedException(409, "Pakkeshoppen kan ikke længere vælges. Søg og vælg en pakkeshop igen.");
        }
        else if (!string.IsNullOrWhiteSpace(selection.ServicePointId))
            throw new CheckoutRejectedException(400, "Hjemmelevering kan ikke have en pakkeshop.");
        return new OrderShippingDto(method.Id, method.Name, method.Carrier, method.Price!.Value, point);
    }

    private ShippingMethodDto RequireMethod(string? methodId)
    {
        var method = GetMethods().FirstOrDefault(method => method.Id == methodId)
            ?? throw new CheckoutRejectedException(400, "Vælg en gyldig leveringsform.");
        if (!method.IsAvailable)
            throw new CheckoutRejectedException(503, "Denne leveringsform er ikke tilgængelig lige nu.");
        return method;
    }

    private static void ValidateAddress(string? postalCode, string? address, string? city)
    {
        if (!Regex.IsMatch(postalCode?.Trim() ?? "", "^[0-9]{4}$") || !ValidText(address, 254) || !ValidText(city, 254))
            throw new CheckoutRejectedException(400, "Udfyld adresse, dansk postnummer og by. Vi leverer kun i Danmark.");
    }

    private static bool ValidText(string? value, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maxLength && !value.Any(char.IsControl);

    private sealed class ShipmondoServicePoint
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Address { get; set; }
        public string? Address2 { get; set; }
        public string? Zipcode { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        [JsonPropertyName("pickup_allowed")] public bool PickupAllowed { get; set; }
    }
}
