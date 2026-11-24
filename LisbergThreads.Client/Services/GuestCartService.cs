using LisbergThreads.Contracts.Orders;
using Microsoft.JSInterop;
using System.Text.Json;
using System.Net.Http.Json;
using LisbergThreads.Contracts.Products;

namespace LisbergThreads.Client.Services;

public class GuestCartService
{
    private const string StorageKey = "guestCart";

    private readonly IJSRuntime _js;
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public GuestCartService(IJSRuntime js, HttpClient http)
    {
        _js = js;
        _http = http;
    }

    public async Task<CartDto> GetCartAsync()
    {
        var json = await _js.InvokeAsync<string?>(
            "localStorage.getItem",
            StorageKey);

        if (string.IsNullOrWhiteSpace(json))
        {
            return EmptyCart();
        }

        var cart = JsonSerializer.Deserialize<CartDto>(json);

        return cart ?? EmptyCart();
    }

    public async Task<string?> AddItemAsync(CartItemDto item)
    {
        await _gate.WaitAsync();
        try
        {
            var cart = await GetCartAsync();

            var existingItem = cart.Items
                .FirstOrDefault(x => x.ProductId == item.ProductId);

            var error = await ValidateStockAsync(item.ProductId,
                (long)(existingItem?.Quantity ?? 0) + item.Quantity);
            if (item.Quantity < 1)
                return "Antal skal være mindst 1.";
            if (error is not null)
                return error;

            if (existingItem is null)
            {
                cart.Items.Add(item);
            }
            else
            {
                var index = cart.Items.IndexOf(existingItem);

                cart.Items[index] = existingItem with
                {
                    Quantity = existingItem.Quantity + item.Quantity,
                    LineTotal = existingItem.UnitPrice *
                                (existingItem.Quantity + item.Quantity)
                };
            }

            await SaveCartAsync(cart);
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string?> UpdateQuantityAsync(Guid productId, int quantity)
    {
        if (quantity < 1)
        {
            return "Antal skal være mindst 1.";
        }

        await _gate.WaitAsync();
        try
        {
            var cart = await GetCartAsync();

            var item = cart.Items
                .FirstOrDefault(x => x.ProductId == productId);

            if (item is null)
            {
                return "Varen findes ikke i kurven.";
            }

            var error = await ValidateStockAsync(productId, quantity);
            if (error is not null)
                return error;

            var index = cart.Items.IndexOf(item);

            cart.Items[index] = item with
            {
                Quantity = quantity,
                LineTotal = item.UnitPrice * quantity
            };

            await SaveCartAsync(cart);
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<string?> ValidateStockAsync(Guid productId, long quantity)
    {
        try
        {
            using var response = await _http.GetAsync($"api/products/{productId}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return "Produktet er ikke længere tilgængeligt.";
            response.EnsureSuccessStatusCode();
            var product = await response.Content.ReadFromJsonAsync<ProductDto>();
            if (product is null || !product.IsActive)
                return "Produktet er ikke længere tilgængeligt.";
            if (product.StockQuantity == 0)
                return $"{product.Name} er udsolgt.";
            return quantity > product.StockQuantity
                ? $"Der er kun {product.StockQuantity} stk. af {product.Name} på lager. Antallet i kurven er medregnet."
                : null;
        }
        catch (HttpRequestException)
        {
            return "Lageret kunne ikke kontrolleres. Prøv igen om lidt.";
        }
        catch (TaskCanceledException)
        {
            return "Lagerkontrollen tog for lang tid. Prøv igen.";
        }
    }

    public async Task RemoveItemAsync(Guid productId)
    {
        var cart = await GetCartAsync();

        cart.Items.RemoveAll(x => x.ProductId == productId);

        await SaveCartAsync(cart);
    }

    public async Task RemovePurchasedItemsAsync(IEnumerable<CheckoutItemDto> purchasedItems)
    {
        await _gate.WaitAsync();
        try
        {
            var cart = await GetCartAsync();
            foreach (var purchased in purchasedItems)
            {
                var index = cart.Items.FindIndex(i => i.ProductId == purchased.ProductId);
                if (index < 0) continue;
                var current = cart.Items[index];
                var remaining = current.Quantity - purchased.Quantity;
                if (remaining <= 0) cart.Items.RemoveAt(index);
                else cart.Items[index] = current with { Quantity = remaining, LineTotal = remaining * current.UnitPrice };
            }
            await SaveCartAsync(cart);
        }
        finally { _gate.Release(); }
    }

    public async Task ClearAsync()
    {
        await _js.InvokeVoidAsync(
            "localStorage.removeItem",
            StorageKey);
    }

    private async Task SaveCartAsync(CartDto cart)
    {
        var total = cart.Items.Sum(x => x.LineTotal);

        var updatedCart = cart with
        {
            Total = total
        };

        var json = JsonSerializer.Serialize(updatedCart);

        await _js.InvokeVoidAsync(
            "localStorage.setItem",
            StorageKey,
            json);
    }

    private static CartDto EmptyCart()
    {
        return new CartDto(
            Guid.Empty,
            new List<CartItemDto>(),
            0);
    }
}
