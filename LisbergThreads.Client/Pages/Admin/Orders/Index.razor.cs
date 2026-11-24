using LisbergThreads.Contracts.Orders;
using Microsoft.AspNetCore.Components;
using System.Net;
using System.Net.Http.Json;

namespace LisbergThreads.Client.Pages.Admin.Orders;

public partial class Index : ComponentBase
{
    [Inject]
    public HttpClient Http { get; set; } = null!;

    protected List<AdminOrderSummaryDto>? Orders { get; set; }

    protected string SelectedStatus { get; set; } = string.Empty;
    protected string? ErrorMessage { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await LoadOrdersAsync();
    }

    protected IEnumerable<AdminOrderSummaryDto> FilteredOrders
    {
        get
        {
            if (Orders is null)
            {
                return [];
            }

            if (string.IsNullOrWhiteSpace(SelectedStatus))
            {
                return Orders;
            }

            return Orders.Where(o => o.Status == SelectedStatus);
        }
    }

    private async Task LoadOrdersAsync()
    {
        ErrorMessage = null;
        Orders = null;

        try
        {
            using var response = await Http.GetAsync("api/orders");

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                ErrorMessage = "Du skal være logget ind for at se ordrer.";
                return;
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                ErrorMessage = "Du har ikke adgang til ordreoversigten.";
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = "Ordreoversigten kunne ikke indlæses.";
                return;
            }

            Orders = await response.Content
                .ReadFromJsonAsync<List<AdminOrderSummaryDto>>()
                ?? [];
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or Microsoft.JSInterop.JSException)
        {
            ErrorMessage = "Ordreoversigten kunne ikke indlæses. Prøv igen om lidt.";
        }
    }
}
