using LisbergThreads.Client.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net;
using System.Net.Http.Json;

namespace LisbergThreads.Client.Components.Shared;

public partial class CartNavLink : ComponentBase, IDisposable
{
    [Inject]
    public HttpClient Http { get; set; } = null!;

    [Inject]
    public CartStateService CartState { get; set; } = null!;

    [Inject]
    public GuestCartService GuestCart { get; set; } = null!;

    [Inject]
    public AuthenticationStateProvider AuthenticationStateProvider { get; set; } = null!;

    private int cartCount;
    private bool countUnavailable;
    private int loadVersion;
    private bool disposed;

    protected override async Task OnInitializedAsync()
    {
        CartState.OnChange += RefreshCartCount;
        AuthenticationStateProvider.AuthenticationStateChanged += OnAuthenticationStateChanged;

        await LoadCartCount();
    }

    private async void RefreshCartCount()
    {
        if (disposed) return;
        try
        {
            await LoadCartCount();
            if (!disposed) await InvokeAsync(StateHasChanged);
        }
        catch (Exception)
        {
            // Komponentens indlæsningsfejl vises gennem countUnavailable.
        }
    }

    private void OnAuthenticationStateChanged(Task<AuthenticationState> state)
    {
        ++loadVersion;
        cartCount = 0;
        countUnavailable = false;
        _ = InvokeAsync(StateHasChanged);
        RefreshCartCount();
    }

    private async Task LoadCartCount()
    {
        var version = ++loadVersion;
        var count = 0;
        try
        {
            var authState = await AuthenticationStateProvider
                .GetAuthenticationStateAsync();

            var isLoggedIn =
                authState.User.Identity?.IsAuthenticated == true;

            if (isLoggedIn)
            {
                using var response = await Http.GetAsync("api/cart/count");
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    if (!disposed && version == loadVersion)
                        countUnavailable = true;
                    return;
                }

                response.EnsureSuccessStatusCode();
                count = await response.Content.ReadFromJsonAsync<int>();
            }
            else
            {
                var cart = await GuestCart.GetCartAsync();

                count = cart.Items.Sum(item => item.Quantity);
            }
            if (!disposed && version == loadVersion)
            {
                cartCount = count;
                countUnavailable = false;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or Microsoft.JSInterop.JSException)
        {
            if (!disposed && version == loadVersion) countUnavailable = true;
        }
    }

    public void Dispose()
    {
        disposed = true;
        ++loadVersion;
        CartState.OnChange -= RefreshCartCount;
        AuthenticationStateProvider.AuthenticationStateChanged -= OnAuthenticationStateChanged;
    }
}
