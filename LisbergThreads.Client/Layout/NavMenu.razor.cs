using LisbergThreads.Client.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System.Net;
using System.Text.Json;

namespace LisbergThreads.Client.Layout;

public partial class NavMenu : IDisposable
{
    [Inject]
    public JwtAuthenticationStateProvider AuthenticationStateProvider { get; set; } = null!;

    [Inject]
    public NavigationManager Navigation { get; set; } = null!;

    [Inject]
    public GlobalSearchService SearchService { get; set; } = null!;
    [Inject]
    public IJSRuntime JS { get; set; } = null!;

    private bool IsSearchOpen { get; set; }
    private bool IsSearchLoading { get; set; }
    private string? SearchErrorMessage { get; set; }
    private CancellationTokenSource? _searchLoadCancellation;
    private int _searchLoadVersion;

    private ElementReference SearchContainer;
    private DotNetObjectReference<NavMenu>? _dotNetReference;

    protected override async Task OnInitializedAsync()
    {
        AuthenticationStateProvider.AuthenticationStateChanged += OnAuthenticationStateChanged;
        await SearchService.LoadProductsAsync();
    }

    private void OnAuthenticationStateChanged(Task<AuthenticationState> authenticationState)
    {
        ResetSearch();
        _ = InvokeAsync(StateHasChanged);
    }

    private void ResetSearch()
    {
        _searchLoadVersion++;
        _searchLoadCancellation?.Cancel();
        SearchService.ClearAdminResults();
        SearchService.SearchText = string.Empty;
        SearchService.SearchType = SearchType.Products;
        IsSearchOpen = false;
        IsSearchLoading = false;
        SearchErrorMessage = null;
    }

    private async Task<bool> CanUseAdminSearchAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        if (authState.User.IsInRole("Admin"))
        {
            return true;
        }

        ResetSearch();
        return false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dotNetReference = DotNetObjectReference.Create(this);

            await JS.InvokeVoidAsync(
                "searchOutsideClick.register",
                SearchContainer,
                _dotNetReference);
        }
    }

    [JSInvokable]
    public async Task CloseSearchFromJs()
    {
        if (!IsSearchOpen)
        {
            return;
        }

        IsSearchOpen = false;

        await InvokeAsync(StateHasChanged);
    }

    private async Task Logout()
    {
        ResetSearch();
        logoutError = null;
        try
        {
            await AuthenticationStateProvider.NotifyUserLogoutAsync();
            Navigation.NavigateTo("/");
        }
        catch (JSException)
        {
            logoutError = "Du kunne ikke logges ud. Kontrollér at browserlagring er tilladt, og prøv igen.";
        }
    }

    private string? logoutError;

    private async Task OnSearchInput(ChangeEventArgs e)
    {
        if (SearchService.SearchType == SearchType.Admin && !await CanUseAdminSearchAsync())
        {
            return;
        }

        SearchService.SearchText = e.Value?.ToString() ?? string.Empty;

        IsSearchOpen = !string.IsNullOrWhiteSpace(SearchService.SearchText);
    }
    private async Task OpenSearch()
    {
        if (SearchService.SearchType == SearchType.Admin && !await CanUseAdminSearchAsync())
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(SearchService.SearchText) ||
            IsSearchLoading || SearchErrorMessage is not null)
        {
            IsSearchOpen = true;
        }
    }

    private async Task SelectSearchType(SearchType searchType)
    {
        ResetSearch();
        var loadVersion = _searchLoadVersion;

        if (searchType != SearchType.Products && !await CanUseAdminSearchAsync())
        {
            return;
        }

        if (loadVersion != _searchLoadVersion)
        {
            return;
        }

        SearchService.SearchType = searchType;
        IsSearchLoading = true;
        IsSearchOpen = searchType == SearchType.Admin;
        using var cancellation = new CancellationTokenSource();
        _searchLoadCancellation = cancellation;

        try
        {
            switch (searchType)
            {
                case SearchType.Products:
                    await SearchService.LoadProductsAsync();
                    break;
                case SearchType.Admin:
                    await SearchService.LoadAdminResultsAsync(cancellation.Token);
                    break;
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Et brugerskifte eller et nyt søgevalg har afbrudt indlæsningen.
        }
        catch (OperationCanceledException)
        {
            if (loadVersion == _searchLoadVersion)
            {
                SearchService.ClearAdminResults();
                SearchErrorMessage = "Indlæsningen tog for lang tid. Prøv at vælge søgningen igen.";
            }
        }
        catch (HttpRequestException ex)
        {
            if (loadVersion == _searchLoadVersion)
            {
                SearchService.ClearAdminResults();
                SearchErrorMessage = ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                    ? "Du har ikke adgang til Admin-søgning. Log ind som Admin igen."
                    : "Søgeresultaterne kunne ikke indlæses. Prøv at vælge søgningen igen.";
            }
        }
        catch (JsonException)
        {
            if (loadVersion == _searchLoadVersion)
            {
                SearchService.ClearAdminResults();
                SearchErrorMessage = "Søgeresultaterne kunne ikke indlæses. Prøv at vælge søgningen igen.";
            }
        }
        finally
        {
            if (loadVersion == _searchLoadVersion)
            {
                IsSearchLoading = false;
            }

            if (ReferenceEquals(_searchLoadCancellation, cancellation))
            {
                _searchLoadCancellation = null;
            }
        }
    }

    private async Task RetryAdminSearch()
    {
        var searchText = SearchService.SearchText;
        var loading = SelectSearchType(SearchType.Admin);
        var loadVersion = _searchLoadVersion;
        await loading;

        if (loadVersion == _searchLoadVersion && SearchService.SearchType == SearchType.Admin)
        {
            SearchService.SearchText = searchText;
            IsSearchOpen = true;
        }
    }

    private string GetSearchTypeLabel()
    {
        return SearchService.SearchType switch
        {
            SearchType.Products => "Produkter",
            SearchType.Admin => "Admin",
            _ => "Søg"
        };
    }

    private string GetSearchPlaceholder()
    {
        return SearchService.SearchType switch
        {
            SearchType.Products => "Søg efter produkter...",
            SearchType.Admin => "Søg efter kunde, e-mail eller ordrenummer...",
            _ => "Søg..."
        };
    }

    private void OpenProduct(Guid productId)
    {
        SearchService.SearchText = string.Empty;
        IsSearchOpen = false;

        Navigation.NavigateTo($"/products/{productId}");
    }

    private async Task OpenCustomer(Guid customerId)
    {
        if (!await CanUseAdminSearchAsync())
        {
            return;
        }

        SearchService.SearchText = string.Empty;
        IsSearchOpen = false;

        Navigation.NavigateTo($"/admin/customers/{customerId}");
    }

    private async Task OpenOrder(Guid orderId)
    {
        if (!await CanUseAdminSearchAsync())
        {
            return;
        }

        SearchService.SearchText = string.Empty;
        IsSearchOpen = false;

        Navigation.NavigateTo($"/admin/orders/{orderId}");
    }

    private void ShowAllProductResults()
    {
        IsSearchOpen = false;
        Navigation.NavigateTo("/products");
    }

    private string GetProductImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return "/images/product-placeholder.png";
        }

        return imageUrl;
    }

    private async Task OnSearchKeyDown(KeyboardEventArgs e)
    {
        if (e.Key != "Enter")
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(SearchService.SearchText))
        {
            return;
        }

        if (IsSearchLoading || SearchErrorMessage is not null)
        {
            return;
        }

        if (SearchService.SearchType != SearchType.Products && !await CanUseAdminSearchAsync())
        {
            return;
        }

        switch (SearchService.SearchType)
        {
            case SearchType.Products:
                IsSearchOpen = false;
                Navigation.NavigateTo("/products");
                break;

            case SearchType.Admin:
                {
                    var customer = SearchService.CustomerSuggestions.FirstOrDefault();
                    var order = SearchService.OrderSuggestions.FirstOrDefault();

                    // Ved resultater i begge grupper vælger brugeren selv et resultat.
                    if (customer is not null && order is null)
                    {
                        await OpenCustomer(customer.Id);
                    }
                    else if (order is not null && customer is null)
                    {
                        await OpenOrder(order.OrderId);
                    }

                    break;
                }
        }
    }

    public void Dispose()
    {
        AuthenticationStateProvider.AuthenticationStateChanged -= OnAuthenticationStateChanged;
        ResetSearch();
    }
}
