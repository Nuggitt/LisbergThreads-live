using System.Net;
using System.Net.Http.Json;
using LisbergThreads.Client.Services;
using LisbergThreads.Contracts.Orders;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace LisbergThreads.Client.Pages;

public partial class Payment : ComponentBase, IDisposable
{
    [Parameter] public Guid PaymentId { get; set; }
    [SupplyParameterFromQuery(Name = "token")] public Guid Token { get; set; }
    [Inject] public HttpClient Http { get; set; } = null!;
    [Inject] public NavigationManager Navigation { get; set; } = null!;
    [Inject] public MobilePayCheckoutService MobilePayCheckout { get; set; } = null!;
    [Inject] public GuestCartService GuestCart { get; set; } = null!;
    [Inject] public CartStateService CartState { get; set; } = null!;
    protected MobilePayStatusDto? Status;
    protected bool IsLoading;
    protected bool CanRetryStart;
    protected string? ErrorMessage;
    private readonly CancellationTokenSource cancellation = new();

    protected string ResultMessage => Status?.State switch
    {
        "StockRejected" => "En vare er ikke længere tilgængelig i det ønskede antal. Ordren er ikke oprettet. Kontrollér kurven før et nyt forsøg.",
        "EXPIRED" => "Betalingstiden udløb. Der er ikke oprettet en ordre. Varerne er bevaret i kurven.",
        _ => "Betalingen er afbrudt eller annulleret. Der er ikke oprettet en ordre. Varerne er bevaret i kurven."
    };

    protected override async Task OnInitializedAsync()
    {
        // Polling is also performed on the server if the customer never returns from the app.
        for (var attempt = 0; attempt < 30 && !cancellation.IsCancellationRequested; attempt++)
        {
            await CheckAsync();
            await InvokeAsync(StateHasChanged);
            if (Status is not { IsActive: true }) break;
            try { await Task.Delay(TimeSpan.FromSeconds(10), cancellation.Token); }
            catch (OperationCanceledException) { break; }
        }
    }

    protected async Task CheckAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        ErrorMessage = null;
        CanRetryStart = false;
        try
        {
            using var response = await Http.GetAsync($"api/payments/mobilepay/{PaymentId}?token={Token}", cancellation.Token);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                var pending = await MobilePayCheckout.GetAsync();
                CanRetryStart = pending?.Request.PaymentId == PaymentId && pending.Request.PaymentToken == Token;
                ErrorMessage = CanRetryStart
                    ? "Bestillingsforsøget er endnu ikke registreret. Du kan genoptage det samme forsøg uden at oprette en ekstra ordre."
                    : "Betalingen blev ikke fundet, eller du har ikke adgang til den.";
                return;
            }
            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = "Betalingen kunne ikke bekræftes endnu. Hent status igen; undlad at bestille på ny.";
                return;
            }
            Status = await response.Content.ReadFromJsonAsync<MobilePayStatusDto>(cancellation.Token)
                ?? throw new System.Text.Json.JsonException();
            if (Status.State == "Completed" && Status.OrderId is { } orderId)
            {
                var cleanupFailed = false;
                try
                {
                    var pending = await MobilePayCheckout.GetAsync();
                    if (pending?.Request.PaymentId == PaymentId && pending.IsGuest)
                        await GuestCart.RemovePurchasedItemsAsync(pending.Request.Items);
                    await MobilePayCheckout.ClearAsync(PaymentId);
                }
                catch (Exception ex) when (ex is JSException or System.Text.Json.JsonException) { cleanupFailed = true; }
                CartState.NotifyChanged();
                Navigation.NavigateTo($"/orders/confirmation/{orderId}?token={Status.ConfirmationToken}&cartCleanupFailed={cleanupFailed.ToString().ToLowerInvariant()}");
            }
            else if (!Status.IsActive) await MobilePayCheckout.ClearAsync(PaymentId);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or JSException)
        {
            ErrorMessage = "Betalingsstatus kunne ikke hentes. Prøv igen; undlad at bestille på ny, før status er bekræftet.";
        }
        finally { IsLoading = false; }
    }

    protected async Task RetryStartAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            var pending = await MobilePayCheckout.GetAsync();
            if (pending is null || pending.Request.PaymentId != PaymentId || pending.Request.PaymentToken != Token) return;
            using var response = await Http.PostAsJsonAsync("api/cart/checkout", pending.Request, cancellation.Token);
            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = OrderFeedback.CheckoutError(response);
                if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.ServiceUnavailable)
                {
                    await MobilePayCheckout.ClearAsync(PaymentId);
                    Status = new MobilePayStatusDto("Cancelled", false, null, null, 0, null);
                    CanRetryStart = false;
                }
                return;
            }
            var result = await response.Content.ReadFromJsonAsync<MobilePayStartDto>(cancellation.Token);
            if (result is null) throw new System.Text.Json.JsonException();
            if (result.PaymentId != PaymentId)
                await MobilePayCheckout.SaveAsync(pending with
                {
                    Request = pending.Request with { PaymentId = result.PaymentId, PaymentToken = result.PaymentToken }
                });
            if (result is { IsActive: true, RedirectUrl: not null })
                Navigation.NavigateTo(result.RedirectUrl, forceLoad: true);
            else
                Navigation.NavigateTo($"/payment/{result.PaymentId}?token={result.PaymentToken}", forceLoad: true);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or JSException)
        {
            ErrorMessage = "Betalingen kunne ikke startes. Behold denne side og hent status igen.";
        }
        finally { IsLoading = false; }
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
