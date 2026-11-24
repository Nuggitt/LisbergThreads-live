using LisbergThreads.Contracts.Customers;
using Microsoft.AspNetCore.Components;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LisbergThreads.Client.Pages.Admin.Customers;

public partial class Index : ComponentBase
{
    [Inject]
    public HttpClient Http { get; set; } = null!;

    protected List<CustomerSummaryDto>? Customers { get; set; }
    protected string? ErrorMessage { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await LoadCustomersAsync();
    }

    private async Task LoadCustomersAsync()
    {
        ErrorMessage = null;
        Customers = null;

        try
        {
            using var response = await Http.GetAsync("api/customers");

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                ErrorMessage = "Du skal være logget ind for at se kunder.";
                return;
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                ErrorMessage = "Du har ikke adgang til kundeoversigten.";
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = "Kundeoversigten kunne ikke indlæses.";
                return;
            }

            Customers = await response.Content
                .ReadFromJsonAsync<List<CustomerSummaryDto>>()
                ?? [];
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Kundeoversigten kunne ikke indlæses. Prøv igen senere.";
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "Indlæsningen tog for lang tid. Prøv igen senere.";
        }
        catch (Microsoft.JSInterop.JSException)
        {
            ErrorMessage = "Browseren kunne ikke læse dit login. Kontrollér browserlagring og log ind igen.";
        }
        catch (JsonException)
        {
            ErrorMessage = "Kundeoversigten kunne ikke indlæses. Prøv igen senere.";
        }
    }
}
