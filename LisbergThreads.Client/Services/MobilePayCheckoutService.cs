using System.Text.Json;
using LisbergThreads.Contracts.Orders;
using Microsoft.JSInterop;

namespace LisbergThreads.Client.Services;

public sealed record PendingMobilePayCheckout(CheckoutRequestDto Request, bool IsGuest);

public sealed class MobilePayCheckoutService(IJSRuntime js)
{
    private const string Key = "pendingMobilePayCheckout";

    public async Task<PendingMobilePayCheckout?> GetAsync()
    {
        var json = await js.InvokeAsync<string?>("localStorage.getItem", Key);
        return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<PendingMobilePayCheckout>(json);
    }

    public ValueTask SaveAsync(PendingMobilePayCheckout checkout) =>
        js.InvokeVoidAsync("localStorage.setItem", Key, JsonSerializer.Serialize(checkout));

    public async Task ClearAsync(Guid paymentId)
    {
        if ((await GetAsync())?.Request.PaymentId == paymentId)
            await js.InvokeVoidAsync("localStorage.removeItem", Key);
    }
}
