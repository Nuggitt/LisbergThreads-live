using System.Globalization;
using System.Net;
using LisbergThreads.Client.Services;
using LisbergThreads.Contracts.Orders;
using LisbergThreads.Contracts.Products;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace LisbergThreads.Client.Pages.Products;

public partial class Details : ComponentBase
{
    [Parameter]
    public Guid id { get; set; }

    [Inject]
    public HttpClient Http { get; set; } = null!;

    [Inject]
    public CartStateService CartState { get; set; } = null!;

    [Inject]
    public GuestCartService GuestCart { get; set; } = null!;

    [Inject]
    public AuthenticationStateProvider AuthenticationStateProvider { get; set; } = null!;

    protected ProductDto? Product { get; set; }
    protected string? ProductImageUrl =>
    Product is null || string.IsNullOrWhiteSpace(Product.ImageUrl)
        ? null
        : new Uri(Http.BaseAddress!, Product.ImageUrl.TrimStart('/')).ToString();


    protected string? message;
    private bool messageIsError = true;
    private bool isLoading;
    private string? loadError;
    // Bevar input som tekst, så delvist indtastede tal ikke omskrives under indtastning.
    private string? selectedQuantity = "1";

    protected override async Task OnParametersSetAsync()
    {
        selectedQuantity = "1";
        message = null;
        productSaved = null;
        await LoadProduct();
    }

    private async Task LoadProduct()
    {
        var productId = id;
        isLoading = true;
        loadError = null;
        Product = null;
        try
        {
            using var response = await Http.GetAsync($"api/products/{productId}");
            if (id != productId) return;
            if (!response.IsSuccessStatusCode)
            {
                loadError = ProductFeedback.Error(response, "Produktet kunne ikke hentes. Prøv igen om lidt.");
                return;
            }
            var product = await response.Content.ReadFromJsonAsync<ProductDto>();
            if (id != productId) return;
            Product = product;
            if (Product is null) loadError = "Produktet kunne ikke hentes. Prøv igen om lidt.";
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or Microsoft.JSInterop.JSException)
        {
            if (id == productId) loadError = "Produktet kunne ikke hentes. Kontrollér forbindelsen og prøv igen.";
        }
        finally { if (id == productId) isLoading = false; }
    }

    private string? productSaved;
    private bool savingProduct;

    private async Task<string?> SaveProductFieldAsync(string field, string value)
    {
        if (Product is null || savingProduct) return "Vent, til den igangværende ændring er gemt.";
        var productId = Product.Id;
        if (field == "Price")
        {
            if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                    CultureInfo.CurrentCulture, out var price) || price < 0 || decimal.Round(price, 2) != price)
                return "Skriv en pris på mindst 0 med højst to decimaler og uden valutasymbol.";
            value = price.ToString(CultureInfo.InvariantCulture);
        }
        else if (field == "IsActive")
        {
            if (value.Trim().Equals("Ja", StringComparison.OrdinalIgnoreCase)) value = "true";
            else if (value.Trim().Equals("Nej", StringComparison.OrdinalIgnoreCase)) value = "false";
            else return "Skriv Ja eller Nej.";
        }
        else if (field == "StockQuantity" && (!int.TryParse(value, out var stock) || stock < 0))
            return "Lagerantal skal være et helt tal på mindst 0.";
        else if (field == "Name" && string.IsNullOrWhiteSpace(value))
            return "Produktnavnet må ikke være tomt.";

        productSaved = null;
        savingProduct = true;
        try
        {
            using var response = await Http.PatchAsJsonAsync($"api/products/{productId}/field",
                new UpdateProductFieldDto(field, value, Product.RowVersionBase64));
            if (response.StatusCode == HttpStatusCode.Conflict)
                return "Produktet er ændret, fx ved et køb. Kopiér din rettelse, og genindlæs siden, før du prøver igen.";
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return "Du skal være logget ind som administrator for at gemme.";
            if (response.StatusCode == HttpStatusCode.BadRequest)
                return "Værdien er ugyldig. Kontrollér feltet og prøv igen.";
            if (!response.IsSuccessStatusCode) return "Ændringen kunne ikke gemmes. Prøv igen om lidt.";
            var updated = await response.Content.ReadFromJsonAsync<ProductDto>()
                ?? throw new InvalidOperationException("Missing updated product.");
            if (id == productId)
            {
                Product = updated;
                productSaved = "Produktet er opdateret.";
                await InvokeAsync(StateHasChanged);
            }
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or InvalidOperationException or Microsoft.JSInterop.JSException)
        {
            return ProductFeedback.UnconfirmedSave;
        }
        finally { savingProduct = false; }
    }

    private async Task AddToCart()
    {
        message = null;
        messageIsError = true;
        try
        {
        if (Product is null || !Product.IsActive || Product.StockQuantity <= 0)
        {
            return;
        }

        if (!int.TryParse(selectedQuantity, out var quantity) || quantity < 1)
        {
            message = "Antal skal være et helt tal på mindst 1.";
            return;
        }

        if (quantity > Product.StockQuantity)
        {
            message = $"Der er kun {Product.StockQuantity} stk. af {Product.Name} på lager.";
            return;
        }

        var isLoggedIn = await IsLoggedInAsync();

        if (isLoggedIn)
        {
            var request = new AddToCartDto(
                Product.Id,
                quantity);

            var response = await Http.PostAsJsonAsync(
                "api/cart/items",
                request);

            if (!response.IsSuccessStatusCode)
            {
                message = ProductFeedback.Error(response, "Produktet kunne ikke tilføjes til kurven. Prøv igen senere.", cart: true);

                return;
            }
        }
        else
        {
            var item = new CartItemDto(
                Product.Id,
                Product.Name,
                Product.Size,
                ProductImageUrl,
                Product.Price,
                quantity,
                Product.Price * quantity);

            message = await GuestCart.AddItemAsync(item);
            if (message is not null)
                return;
        }

        message = "Produktet blev tilføjet til kurven.";
        messageIsError = false;

        CartState.NotifyChanged();
        CartState.OpenSidebar();
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or Microsoft.JSInterop.JSException)
        {
            messageIsError = true;
            message = ProductFeedback.UnconfirmedCart;
            CartState.NotifyChanged();
        }
    }

    private async Task<bool> IsLoggedInAsync()
    {
        var authState =
            await AuthenticationStateProvider.GetAuthenticationStateAsync();

        return authState.User.Identity?.IsAuthenticated == true;
    }
}
