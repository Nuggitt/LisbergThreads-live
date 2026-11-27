using LisbergThreads.Client.Services;
using LisbergThreads.Contracts.Orders;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Json;
using LisbergThreads.Contracts.Authentication;
using System.ComponentModel.DataAnnotations;

namespace LisbergThreads.Client.Pages;

public partial class Checkout : ComponentBase
{
    [Inject]
    public HttpClient Http { get; set; } = null!;

    [Inject]
    public GuestCartService GuestCart { get; set; } = null!;

    [Inject]
    public CartStateService CartState { get; set; } = null!;

    [Inject]
    public AuthenticationStateProvider AuthenticationStateProvider { get; set; } = null!;

    [Inject]
    public NavigationManager Navigation { get; set; } = null!;

    [Inject] public MobilePayCheckoutService MobilePayCheckout { get; set; } = null!;

    protected CartDto? Cart { get; set; }
    protected CheckoutPaymentOptionsDto? PaymentOptions { get; set; }

    protected List<ShippingMethodDto> ShippingMethods { get; set; } = [];
    protected List<ShippingServicePointDto> ServicePoints { get; set; } = [];
    protected string SelectedMethodId { get; set; } = string.Empty;
    protected string SelectedServicePointId { get; set; } = string.Empty;
    protected bool IsLoadingServicePoints { get; set; }
    protected string? ShippingError { get; set; }
    private int servicePointRequestVersion;
    private string? searchedAddress;
    private string CurrentShippingAddress => string.Join("\n", SelectedMethodId, Model.PostalCode.Trim(), Model.Address.Trim(), Model.City.Trim());
    protected ShippingMethodDto? SelectedMethod => ShippingMethods.FirstOrDefault(method => method.Id == SelectedMethodId);
    protected bool HasValidShipping => SelectedMethod is { IsAvailable: true, Price: not null } method
        && (!method.RequiresServicePoint || searchedAddress == CurrentShippingAddress
            && ServicePoints.Any(point => point.Id == SelectedServicePointId));
    protected decimal? ShippingTotal => SelectedMethod is { IsAvailable: true } ? SelectedMethod.Price : null;
    protected bool CanCheckout => !IsSaving && !CheckoutUncertain && PaymentOptions?.IsAvailable == true && HasValidShipping;

    protected void ResetServicePoints()
    {
        servicePointRequestVersion++;
        ServicePoints = [];
        SelectedServicePointId = string.Empty;
        searchedAddress = null;
        ShippingError = null;
        IsLoadingServicePoints = false;
    }

    protected async Task FindServicePointsAsync()
    {
        ResetServicePoints();
        if (SelectedMethod is not { IsAvailable: true, RequiresServicePoint: true }) return;
        if (!System.Text.RegularExpressions.Regex.IsMatch(Model.PostalCode.Trim(), "^[0-9]{4}$")
            || string.IsNullOrWhiteSpace(Model.Address) || string.IsNullOrWhiteSpace(Model.City))
        {
            ShippingError = "Udfyld adresse, dansk postnummer og by, før du søger efter pakkeshops.";
            return;
        }
        var version = servicePointRequestVersion;
        var address = CurrentShippingAddress;
        IsLoadingServicePoints = true;
        try
        {
            var url = $"api/shipping/service-points?methodId={Uri.EscapeDataString(SelectedMethodId)}"
                + $"&postalCode={Uri.EscapeDataString(Model.PostalCode.Trim())}"
                + $"&address={Uri.EscapeDataString(Model.Address.Trim())}&city={Uri.EscapeDataString(Model.City.Trim())}";
            using var response = await Http.GetAsync(url);
            if (version != servicePointRequestVersion || address != CurrentShippingAddress) return;
            if (!response.IsSuccessStatusCode)
            {
                ShippingError = response.StatusCode == System.Net.HttpStatusCode.TooManyRequests
                    ? "Der er søgt for mange gange. Vent et minut og prøv igen."
                    : "Pakkeshops kunne ikke hentes. Kontrollér adressen og prøv igen, eller vælg hjemmelevering.";
                return;
            }
            var points = await response.Content.ReadFromJsonAsync<List<ShippingServicePointDto>>()
                ?? throw new System.Text.Json.JsonException();
            if (version != servicePointRequestVersion || address != CurrentShippingAddress) return;
            ServicePoints = points;
            searchedAddress = address;
            if (points.Count == 0) ShippingError = "Der blev ikke fundet en pakkeshop. Kontrollér adressen eller vælg en anden leveringsform.";
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException)
        {
            if (version == servicePointRequestVersion) ShippingError = "Pakkeshops kunne ikke hentes. Kontrollér forbindelsen og prøv igen.";
        }
        finally { if (version == servicePointRequestVersion) IsLoadingServicePoints = false; }
    }

    protected CheckoutFormModel Model { get; set; } = new();

    protected bool IsSaving { get; set; }
    protected bool IsLoading { get; set; }
    protected bool CheckoutUncertain { get; set; }
    protected string? LoadError { get; set; }

    protected string? ErrorMessage { get; set; }

    protected override async Task OnInitializedAsync()
    {
        IsLoading = true;
        try
        {
            PaymentOptions = await Http.GetFromJsonAsync<CheckoutPaymentOptionsDto>("api/cart/checkout-options")
                ?? throw new System.Text.Json.JsonException();
            Model.PaymentMethod = PaymentOptions.IsTestMode ? PaymentMethods.Test : PaymentMethods.MobilePay;
            ShippingMethods = await Http.GetFromJsonAsync<List<ShippingMethodDto>>("api/shipping/methods")
                ?? throw new System.Text.Json.JsonException();
            var pending = await MobilePayCheckout.GetAsync();
            if (pending is not null)
            {
                Navigation.NavigateTo($"/payment/{pending.Request.PaymentId}?token={pending.Request.PaymentToken}");
                return;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or Microsoft.JSInterop.JSException or System.Text.Json.JsonException)
        {
            ErrorMessage = "Checkout kunne ikke klargøres. Kontrollér forbindelsen og en eventuel tidligere bestilling, før du bestiller igen.";
            CheckoutUncertain = true;
        }
        await LoadCartAsync();
        try { await LoadUserAsync(); }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException)
        {
            ErrorMessage = "Dine profiloplysninger kunne ikke hentes. Udfyld kontaktoplysningerne manuelt.";
        }
    }

    private async Task LoadCartAsync()
    {
        IsLoading = true;
        LoadError = null;
        try
        {
            var isLoggedIn = await IsLoggedInAsync();

            if (isLoggedIn)
            {
                Cart = await Http.GetFromJsonAsync<CartDto>("api/cart");
            }
            else
            {
                Cart = await GuestCart.GetCartAsync();
            }
            if (Cart is null) LoadError = "Kurven kunne ikke hentes. Prøv igen.";
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or Microsoft.JSInterop.JSException)
        {
            Cart = null;
            LoadError = "Kurven kunne ikke hentes. Kontrollér forbindelsen og prøv igen.";
        }
        finally { IsLoading = false; }
    }

    protected async Task CheckoutAsync()
    {
        if (Cart is null ||
            Cart.Items.Count == 0 ||
            !CanCheckout)
        {
            return;
        }

        ErrorMessage = null;
        IsSaving = true;

        try
        {
            var items = Cart.Items
                .Select(item => new CheckoutItemDto(
                    item.ProductId,
                    item.Quantity))
                .ToList();

            var request = new CheckoutRequestDto(
                Model.FirstName,
                Model.LastName,
                Model.Email,
                Model.Phone,
                Model.Address,
                Model.PostalCode,
                Model.City,
                Model.PaymentMethod,
                items,
                Guid.NewGuid(),
                Guid.NewGuid())
            {
                Shipping = new ShippingSelectionDto(SelectedMethodId, ShippingTotal!.Value,
                    SelectedMethod!.RequiresServicePoint ? SelectedServicePointId : null)
            };

            await MobilePayCheckout.SaveAsync(new PendingMobilePayCheckout(request, !await IsLoggedInAsync()));

            using var response = await Http.PostAsJsonAsync(
                "api/cart/checkout",
                request);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode is System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.Conflict
                    or System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden
                    or System.Net.HttpStatusCode.ServiceUnavailable)
                {
                    await MobilePayCheckout.ClearAsync(request.PaymentId);
                    ErrorMessage = OrderFeedback.CheckoutError(response);
                    // Refresh changed prices and force a fresh parcelshop choice after a controlled rejection.
                    ResetServicePoints();
                    try
                    {
                        ShippingMethods = await Http.GetFromJsonAsync<List<ShippingMethodDto>>("api/shipping/methods") ?? [];
                        PaymentOptions = await Http.GetFromJsonAsync<CheckoutPaymentOptionsDto>("api/cart/checkout-options");
                    }
                    catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException)
                    {
                        ShippingMethods = [];
                        ErrorMessage += " Leveringsmulighederne kunne ikke genindlæses. Genindlæs siden.";
                    }
                    return;
                }
                Navigation.NavigateTo($"/payment/{request.PaymentId}?token={request.PaymentToken}");
                return;
            }

            var result = await response.Content.ReadFromJsonAsync<MobilePayStartDto>();
            if (result is null) throw new System.Text.Json.JsonException();
            if (result.PaymentId != request.PaymentId)
            {
                // Another tab may already have started the account's cart payment.
                await MobilePayCheckout.SaveAsync(new PendingMobilePayCheckout(
                    request with { PaymentId = result.PaymentId, PaymentToken = result.PaymentToken }, !await IsLoggedInAsync()));
            }
            if (result.IsActive && !string.IsNullOrWhiteSpace(result.RedirectUrl))
                Navigation.NavigateTo(result.RedirectUrl, forceLoad: true);
            else
                Navigation.NavigateTo($"/payment/{result.PaymentId}?token={result.PaymentToken}");
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or Microsoft.JSInterop.JSException)
        {
            CheckoutUncertain = true;
            ErrorMessage = "Bestillingen kunne ikke bekræftes. Genåbn checkout for at se det gemte forsøg; undlad at oprette en ny bestilling.";
        }
        finally
        {
            IsSaving = false;
        }
    }

    protected void BackToCart()
    {
        Navigation.NavigateTo("/cart");
    }

    private async Task<bool> IsLoggedInAsync()
    {
        var authState =
            await AuthenticationStateProvider.GetAuthenticationStateAsync();

        return authState.User.Identity?.IsAuthenticated == true;
    }

    private async Task LoadUserAsync()
    {
        var isLoggedIn = await IsLoggedInAsync();

        if (!isLoggedIn)
        {
            return;
        }

        var user = await Http.GetFromJsonAsync<CurrentUserDto>(
            "api/Auth/me");

        if (user is null)
        {
            return;
        }

        Model.FirstName = user.FirstName;
        Model.LastName = user.LastName;
        Model.Email = user.Email;
        Model.Phone = user.PhoneNumber;
    }

    protected sealed class CheckoutFormModel
    {
        [Required(ErrorMessage = "Fornavn er påkrævet.")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Efternavn er påkrævet.")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "E-mail er påkrævet.")]
        [EmailAddress(ErrorMessage = "Indtast en gyldig e-mailadresse.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Telefonnummer er påkrævet.")]
        [RegularExpression(@"^[0-9]+$",
    ErrorMessage = "Telefonnummer må kun indeholde tal.")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Adresse er påkrævet.")]
        public string Address { get; set; } = string.Empty;

        [Required(ErrorMessage = "Postnummer er påkrævet.")]
        [RegularExpression(@"^[0-9]{4}$", ErrorMessage = "Indtast et dansk postnummer med fire cifre.")]
        public string PostalCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "By er påkrævet.")]
        public string City { get; set; } = string.Empty;

        public string PaymentMethod { get; set; } = PaymentMethods.MobilePay;
    }
}
