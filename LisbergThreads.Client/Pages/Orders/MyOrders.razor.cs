using LisbergThreads.Contracts.Orders;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace LisbergThreads.Client.Pages.Orders;

public partial class MyOrders : ComponentBase
{
    [Inject]
    public HttpClient Http { get; set; } = null!;

    protected List<OrderSummaryDto>? Orders { get; set; }

    protected string? ErrorMessage { get; set; }

    protected override Task OnInitializedAsync() => LoadOrdersAsync();

    private async Task LoadOrdersAsync()
    {
        ErrorMessage = null;
        Orders = null;
        try
        {
            using var response = await Http.GetAsync("api/orders/me");
            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = response.StatusCode switch
                {
                    System.Net.HttpStatusCode.Unauthorized => "Din session er udløbet. Log ind igen for at se dine ordrer.",
                    System.Net.HttpStatusCode.Forbidden => "Du har ikke adgang til denne ordreoversigt.",
                    _ => "Dine ordrer kunne ikke hentes. Prøv igen senere."
                };
                return;
            }
            Orders = await response.Content.ReadFromJsonAsync<List<OrderSummaryDto>>()
                ?? throw new System.Text.Json.JsonException();
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or Microsoft.JSInterop.JSException)
        {
            ErrorMessage =
                "Dine ordrer kunne ikke indlæses.";
        }
    }
}
