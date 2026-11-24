using LisbergThreads.Client.Services;
using LisbergThreads.Contracts.Orders;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Json;

namespace LisbergThreads.Client.Pages;

public partial class Cart : ComponentBase, IDisposable
{
    [Inject]
    public HttpClient Http { get; set; } = null!;

    [Inject]
    public CartStateService CartState { get; set; } = null!;

    [Inject]
    public GuestCartService GuestCart { get; set; } = null!;

    [Inject]
    public NavigationManager Navigation { get; set; } = null!;

    [Inject]
    public AuthenticationStateProvider AuthenticationStateProvider { get; set; } = null!;

    private CartDto? cart;
    private string? message;
    private bool messageIsError = true;
    private string? loadError;
    private int loadVersion;
    private bool disposed;

    protected override async Task OnInitializedAsync()
    {
        CartState.OnChange += RefreshCart;
        AuthenticationStateProvider.AuthenticationStateChanged += OnAuthenticationStateChanged;
        await LoadCart();
    }

    private void OnAuthenticationStateChanged(Task<AuthenticationState> state)
    {
        ++loadVersion;
        cart = null;
        message = null;
        loadError = null;
        RefreshCart();
    }

    private async void RefreshCart()
    {
        await InvokeAsync(async () =>
        {
            if (disposed) return;
            StateHasChanged();
            await LoadCart();
            if (!disposed) StateHasChanged();
        });
    }

    private async Task LoadCart()
    {
        var version = ++loadVersion;
        loadError = null;
        try
        {
            var isLoggedIn = await IsLoggedInAsync();
            var loadedCart = isLoggedIn
                ? await Http.GetFromJsonAsync<CartDto>("api/cart")
                : await GuestCart.GetCartAsync();
            if (loadedCart is null) throw new System.Text.Json.JsonException();
            if (!disposed && version == loadVersion) cart = loadedCart;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or Microsoft.JSInterop.JSException)
        {
            if (!disposed && version == loadVersion)
            {
                cart = null;
                loadError = "Kurven kunne ikke hentes. Kontrollér forbindelsen og prøv igen.";
            }
        }
    }

    public void Dispose()
    {
        disposed = true;
        ++loadVersion;
        CartState.OnChange -= RefreshCart;
        AuthenticationStateProvider.AuthenticationStateChanged -= OnAuthenticationStateChanged;
    }

    private async Task RemoveFromCart(Guid productId)
    {
        var version = loadVersion;
        message = null;
        messageIsError = true;
        try
        {
            var isLoggedIn = await IsLoggedInAsync();

            if (isLoggedIn)
            {
                using var response = await Http.DeleteAsync(
                    $"api/cart/items/{productId}");

                if (!response.IsSuccessStatusCode)
                {
                    if (version == loadVersion) message = OrderFeedback.CartError(response);

                    return;
                }
            }
            else
            {
                await GuestCart.RemoveItemAsync(productId);
            }

            if (disposed || version != loadVersion) { CartState.NotifyChanged(); return; }
            message = "Produktet blev fjernet fra kurven.";
            messageIsError = false;

            await LoadCart();

            CartState.NotifyChanged();
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or Microsoft.JSInterop.JSException)
        {
            if (!disposed && version == loadVersion)
            {
                messageIsError = true;
                message = OrderFeedback.UnconfirmedCart;
            }
        }
    }

    private async Task ChangeQuantity(Guid productId, int quantity)
    {
        var version = loadVersion;
        message = null;
        messageIsError = true;
        try
        {
            if (quantity < 1)
            {
                return;
            }

            var isLoggedIn = await IsLoggedInAsync();

            if (isLoggedIn)
            {
                using var response = await Http.PutAsJsonAsync(
                    $"api/cart/items/{productId}",
                    quantity);

                if (!response.IsSuccessStatusCode)
                {
                    if (version == loadVersion) message = OrderFeedback.CartError(response);

                    return;
                }
            }
            else
            {
                var error = await GuestCart.UpdateQuantityAsync(
                    productId,
                    quantity);
                if (disposed || version != loadVersion) return;
                message = error;
                if (error is not null)
                    return;
            }

            if (disposed || version != loadVersion) { CartState.NotifyChanged(); return; }
            message = "Antal opdateret.";
            messageIsError = false;

            await LoadCart();

            CartState.NotifyChanged();
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or Microsoft.JSInterop.JSException)
        {
            if (!disposed && version == loadVersion)
            {
                messageIsError = true;
                message = OrderFeedback.UnconfirmedCart;
            }
        }
    }

    private void Checkout()
    {
        Navigation.NavigateTo("/checkout");
    }

    private async Task<bool> IsLoggedInAsync()
    {
        var authState = await AuthenticationStateProvider
            .GetAuthenticationStateAsync();

        return authState.User.Identity?.IsAuthenticated == true;
    }
}
