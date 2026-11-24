using LisbergThreads.Client.Services;
using LisbergThreads.Contracts.Orders;
using LisbergThreads.Contracts.Products;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net;

namespace LisbergThreads.Client.Pages.Products;

public partial class ProductList : ComponentBase, IDisposable
{
    [Inject]
    public HttpClient Http { get; set; } = null!;

    [Inject]
    public CartStateService CartState { get; set; } = null!;

    [Inject]
    public GuestCartService GuestCart { get; set; } = null!;

    [Inject]
    public AuthenticationStateProvider AuthenticationStateProvider { get; set; } = null!;

    [Inject]
    public GlobalSearchService SearchService { get; set; } = null!;

    private List<ProductDto>? products;
    // Bevar input som tekst, så delvist indtastede tal ikke omskrives under indtastning.
    private readonly Dictionary<Guid, string?> quantities = new();
    private string? message;
    private bool messageIsError = true;

    [SupplyParameterFromQuery(Name = "saved")]
    public string? Saved { get; set; }

    protected override void OnParametersSet()
    {
        message = Saved switch { "created" => "Produktet blev oprettet.", "updated" => "Produktet blev opdateret.", _ => null };
        messageIsError = message is null;
    }
    private bool isAdmin;
    private bool isLoading;
    private string? loadError;
    private ProductDto? productToDeactivate;
    private bool isDeactivating;
    private ProductDto? productToDelete;
    private bool isDeleting;

    private async Task ConfirmPermanentDeletionAsync()
    {
        if (isDeleting || productToDelete is null || !isAdmin) return;
        var product = productToDelete;
        productToDelete = null;
        isDeleting = true;
        message = null;
        messageIsError = true;
        try
        {
            using var response = await Http.DeleteAsync($"api/products/{product.Id}/permanent");
            if (!response.IsSuccessStatusCode)
            {
                message = response.StatusCode switch
                {
                    HttpStatusCode.Conflict => "Produktet skal være deaktiveret og må ikke bruges i en ordre eller kurv. Genindlæs siden og kontrollér produktet.",
                    HttpStatusCode.NotFound => "Produktet findes ikke længere. Genindlæs siden.",
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                        "Du skal være logget ind som administrator for at slette produkter.",
                    _ => "Produktet kunne ikke slettes. Prøv igen om lidt."
                };
                return;
            }
            products?.RemoveAll(p => p.Id == product.Id);
            quantities.Remove(product.Id);
            message = $"“{product.Name}” blev slettet permanent.";
            messageIsError = false;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or Microsoft.JSInterop.JSException)
        {
            messageIsError = true;
            message = "Sletningen kunne ikke bekræftes. Genindlæs siden for at kontrollere produktets status.";
        }
        finally { isDeleting = false; }
    }
    private ProductStatusFilter selectedStatusFilter = ProductStatusFilter.All;

    private enum ProductStatusFilter
    {
        All,
        Active,
        SoldOut,
        Inactive
    }

    private bool HasProductSearch =>
        SearchService.SearchType == SearchType.Products &&
        !string.IsNullOrWhiteSpace(SearchService.SearchText);

    [SupplyParameterFromQuery(Name = "loggedIn")]
    public bool LoggedIn { get; set; }

    private IEnumerable<ProductDto> FilteredProducts
    {
        get
        {
            if (products is null)
            {
                return Enumerable.Empty<ProductDto>();
            }

            IEnumerable<ProductDto> filteredProducts = products;

            if (isAdmin)
            {
                filteredProducts = selectedStatusFilter switch
                {
                    ProductStatusFilter.Active => filteredProducts.Where(p => p.IsActive),
                    ProductStatusFilter.SoldOut => filteredProducts.Where(p => p.IsActive && p.StockQuantity == 0),
                    ProductStatusFilter.Inactive => filteredProducts.Where(p => !p.IsActive),
                    _ => filteredProducts
                };
            }

            if (HasProductSearch)
            {
                filteredProducts = filteredProducts.Where(p =>
                    p.Name.Contains(
                        SearchService.SearchText,
                        StringComparison.OrdinalIgnoreCase));
            }

            return filteredProducts;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        SearchService.SearchChanged += OnSearchChanged;
        await LoadProductsAsync();
    }

    private async Task LoadProductsAsync()
    {
        if (isLoading) return;
        isLoading = true;
        loadError = null;
        products = null;
        try
        {
            var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            isAdmin = authState.User.IsInRole("Admin");
            if (isAdmin) await LoadAllProducts();
            else await LoadActiveProductsAsync();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or Microsoft.JSInterop.JSException)
        {
            loadError = "Vi kunne ikke hente produkterne lige nu. Prøv igen om lidt.";
        }
        finally { isLoading = false; }
    }

    private async Task LoadActiveProductsAsync()
    {
        var response =
            await Http.GetFromJsonAsync<List<ProductDto>>("api/products");

        products = response ?? new List<ProductDto>();
    }

    private async Task LoadAllProducts()
    {
        using var response = await Http.GetAsync("api/products/all");
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            isAdmin = false;
            products = null;
            message = response.StatusCode == HttpStatusCode.Unauthorized
                ? "Din session er udløbet. Log ind igen."
                : "Du har ikke adgang til administratorens produktoversigt.";
            messageIsError = true;
            await LoadActiveProductsAsync();
            return;
        }

        response.EnsureSuccessStatusCode();
        products = await response.Content.ReadFromJsonAsync<List<ProductDto>>() ?? new List<ProductDto>();
    }

    private async Task ConfirmDeactivationAsync()
    {
        if (isDeactivating || productToDeactivate is null || !isAdmin)
            return;

        var product = productToDeactivate;
        productToDeactivate = null;
        isDeactivating = true;
        message = null;
        messageIsError = true;
        try
        {
            using var response = await Http.DeleteAsync($"api/products/{product.Id}");
            if (!response.IsSuccessStatusCode)
            {
                message = response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                    ? "Du skal være logget ind som administrator for at deaktivere produkter."
                    : "Produktet kunne ikke deaktiveres. Prøv igen om lidt.";
                return;
            }

            message = $"“{product.Name}” blev deaktiveret.";
            messageIsError = false;
            try
            {
                await LoadAllProducts();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or Microsoft.JSInterop.JSException)
            {
                message = "Produktet blev deaktiveret, men produktlisten kunne ikke opdateres. Genindlæs siden.";
                messageIsError = true;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or Microsoft.JSInterop.JSException)
        {
            message = "Deaktiveringen kunne ikke bekræftes. Genindlæs siden for at kontrollere produktets status.";
            messageIsError = true;
        }
        finally
        {
            isDeactivating = false;
        }
    }

    private async Task ActivateProductAsync(Guid id)
    {
        message = null;
        messageIsError = true;
        try
        {
        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"api/products/{id}/activate");

        using var response = await Http.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            message = ProductFeedback.Error(response, "Produktet kunne ikke aktiveres. Prøv igen senere.");

            return;
        }

        message = "Produktet blev aktiveret.";
        messageIsError = false;

        await LoadAllProducts();
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or Microsoft.JSInterop.JSException)
        {
            messageIsError = true;
            message = "Aktiveringen eller opdateringen af listen kunne ikke bekræftes. Genindlæs siden for at kontrollere produktets status.";
        }
    }

    private void OnSearchChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    private string? GetQuantity(Guid productId) =>
        quantities.TryGetValue(productId, out var quantity) ? quantity : "1";

    private async Task AddToCart(Guid productId)
    {
        message = null;
        messageIsError = true;
        try
        {
        var product = products?.FirstOrDefault(p => p.Id == productId);

        if (product is null || !product.IsActive || product.StockQuantity <= 0)
        {
            return;
        }

        if (!int.TryParse(GetQuantity(productId), out var quantity) || quantity < 1)
        {
            message = "Antal skal være et helt tal på mindst 1.";
            return;
        }

        if (quantity > product.StockQuantity)
        {
            message = $"Der er kun {product.StockQuantity} stk. af {product.Name} på lager.";
            return;
        }

        var authState = await AuthenticationStateProvider
            .GetAuthenticationStateAsync();

        var isLoggedIn =
            authState.User.Identity?.IsAuthenticated == true;

        if (isLoggedIn)
        {
            var request = new AddToCartDto(
                productId,
                quantity
            );

            var response = await Http.PostAsJsonAsync(
                "api/cart/items",
                request
            );

            if (!response.IsSuccessStatusCode)
            {
                message = ProductFeedback.Error(response, "Produktet kunne ikke tilføjes til kurven. Prøv igen senere.", cart: true);

                return;
            }
        }
        else
        {
            var guestItem = new CartItemDto(
                product.Id,
                product.Name,
                product.Size,
                product.ImageUrl,
                product.Price,
                quantity,
                product.Price * quantity
            );

            message = await GuestCart.AddItemAsync(guestItem);
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

    protected string? GetImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return null;
        }

        if (imageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            imageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return imageUrl;
        }

        return new Uri(
            Http.BaseAddress!,
            imageUrl.TrimStart('/')
        ).ToString();
    }

    public void Dispose()
    {
        SearchService.SearchChanged -= OnSearchChanged;
    }
}
