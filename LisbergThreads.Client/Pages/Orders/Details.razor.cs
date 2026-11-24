using LisbergThreads.Contracts.Orders;
using Microsoft.AspNetCore.Components;
using System.Net;
using System.Net.Http.Json;

namespace LisbergThreads.Client.Pages.Orders;

public partial class Details : ComponentBase
{
    [Parameter]
    public Guid OrderId { get; set; }

    [Inject]
    public HttpClient Http { get; set; } = null!;

    protected OrderDetailsDto? Order { get; set; }

    protected string? ErrorMessage { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        await LoadOrderAsync();
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

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
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
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or Microsoft.JSInterop.JSException)
        {
            if (OrderId == orderId) ErrorMessage = "Ordren kunne ikke hentes. Kontrollér forbindelsen og prøv igen.";
        }
    }
}
