using LisbergThreads.Contracts.Orders;
using Microsoft.AspNetCore.Components;
using System.Net;
using System.Net.Http.Json;

namespace LisbergThreads.Client.Pages.Admin.Orders;

public partial class Details : ComponentBase
{
    [Parameter]
    public Guid OrderId { get; set; }

    [Inject]
    public HttpClient Http { get; set; } = null!;

    protected OrderDetailsDto? Order { get; set; }

    protected string? ErrorMessage { get; set; }

    protected string SelectedStatus { get; set; } = string.Empty;

    protected List<string> AvailableStatuses { get; set; } = [];

    protected bool IsUpdatingStatus { get; set; }

    protected string? StatusMessage { get; set; }

    protected string? StatusErrorMessage { get; set; }

    protected string TrackingNumber { get; set; } = string.Empty;
    protected bool IsUpdatingTracking { get; set; }
    protected string? TrackingMessage { get; set; }
    protected string? TrackingErrorMessage { get; set; }

    protected async Task UpdateTrackingAsync()
    {
        if (Order is null || IsUpdatingTracking) return;
        var orderId = Order.OrderId;
        TrackingMessage = null;
        TrackingErrorMessage = null;
        IsUpdatingTracking = true;
        try
        {
            using var response = await Http.PutAsJsonAsync($"api/orders/{orderId}/tracking",
                new UpdateOrderTrackingDto(TrackingNumber, Order.TrackingNumber));
            if (OrderId != orderId) return;
            if (!response.IsSuccessStatusCode)
            {
                TrackingErrorMessage = response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Du skal være logget ind som administrator for at gemme tracking.",
                    HttpStatusCode.Conflict => "Ordren er ændret. Hent ordren igen, før du gemmer tracking.",
                    HttpStatusCode.BadRequest => "Kontrollér trackingnummeret. Det må højst være 100 tegn.",
                    _ => "Tracking kunne ikke gemmes. Hent ordren igen og kontrollér nummeret."
                };
                return;
            }
            await LoadOrderAsync();
            if (OrderId == orderId) TrackingMessage = "Trackingnummeret blev gemt.";
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException)
        {
            if (OrderId == orderId) TrackingErrorMessage = "Tracking kunne ikke bekræftes. Hent ordren igen og kontrollér nummeret.";
        }
        finally { IsUpdatingTracking = false; }
    }

    protected override async Task OnParametersSetAsync()
    {
        StatusMessage = null;
        StatusErrorMessage = null;
        TrackingMessage = null;
        TrackingErrorMessage = null;
        await LoadOrderAsync();
    }

    private void SetAvailableStatuses()
    {
        AvailableStatuses = Order?.Status switch
        {
            OrderStatuses.Placed =>
                [OrderStatuses.Confirmed],

            OrderStatuses.Confirmed =>
                [OrderStatuses.Shipped],

            OrderStatuses.Shipped =>
                [OrderStatuses.Completed],

            _ => []
        };

        SelectedStatus = string.Empty;
    }

    private async Task LoadOrderAsync()
    {
        var orderId = OrderId;
        try
        {
            ErrorMessage = null;
            Order = null;

            using var response = await Http.GetAsync(
                $"api/orders/{orderId}");
            if (OrderId != orderId) return;

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                ErrorMessage = "Ordren blev ikke fundet.";
                return;
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                ErrorMessage = "Du har ikke adgang til denne ordre.";
                return;
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                ErrorMessage = "Du har ikke adgang til denne ordre.";
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = "Ordren kunne ikke indlæses.";
                return;
            }

            var order = await response.Content.ReadFromJsonAsync<OrderDetailsDto>();
            if (OrderId != orderId) return;
            Order = order;

            if (Order is null)
            {
                ErrorMessage = "Ordren kunne ikke indlæses.";
                return;
            }

            TrackingNumber = Order.TrackingNumber ?? string.Empty;
            SetAvailableStatuses();
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or Microsoft.JSInterop.JSException)
        {
            if (OrderId == orderId) ErrorMessage = "Ordren kunne ikke hentes. Kontrollér forbindelsen og prøv igen.";
        }
    }

    protected async Task UpdateStatusAsync()
    {
        if (Order is null ||
            string.IsNullOrWhiteSpace(SelectedStatus) ||
            IsUpdatingStatus)
        {
            return;
        }

        StatusMessage = null;
        StatusErrorMessage = null;
        IsUpdatingStatus = true;
        var orderId = Order.OrderId;

        try
        {
            var request = new UpdateOrderStatusDto(
                SelectedStatus);

            using var response = await Http.PutAsJsonAsync(
                $"api/orders/{orderId}/status",
                request);
            if (OrderId != orderId) return;

            if (!response.IsSuccessStatusCode)
            {
                StatusErrorMessage = response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Du skal være logget ind som administrator for at ændre ordrestatus.",
                    HttpStatusCode.BadRequest or HttpStatusCode.Conflict => "Statusændringen er ikke gyldig, eller ordren er ændret. Hent ordren igen og kontrollér status.",
                    HttpStatusCode.NotFound => "Ordren findes ikke længere.",
                    _ => "Statusændringen kunne ikke bekræftes. Hent ordren igen og kontrollér status, før du prøver igen."
                };

                return;
            }

            StatusMessage = "Ordrestatus blev opdateret.";

            await LoadOrderAsync();
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or Microsoft.JSInterop.JSException)
        {
            if (OrderId == orderId) StatusErrorMessage = "Statusændringen kunne ikke bekræftes. Hent ordren igen og kontrollér status, før du prøver igen.";
        }
        finally
        {
            IsUpdatingStatus = false;
        }
    }
}
