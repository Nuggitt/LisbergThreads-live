using LisbergThreads.Contracts.Customers;
using LisbergThreads.Contracts.Orders;
using Microsoft.AspNetCore.Components;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LisbergThreads.Client.Pages.Admin.Customers;

public partial class Details : ComponentBase, IDisposable
{
    [Parameter]
    public Guid CustomerId { get; set; }

    [Inject]
    public HttpClient Http { get; set; } = null!;

    protected CustomerDetailsDto? Customer { get; set; }

    protected string? ErrorMessage { get; set; }
    protected List<AdminOrderSummaryDto>? Orders { get; set; }
    protected string? OrdersErrorMessage { get; set; }

    private CancellationTokenSource? _loadCancellation;

    protected override async Task OnParametersSetAsync()
    {
        _loadCancellation?.Cancel();
        using var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;
        var customerId = CustomerId;

        Customer = null;
        ErrorMessage = null;
        Orders = null;
        OrdersErrorMessage = null;

        try
        {
            await LoadCustomerAsync(customerId, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();

            if (Customer is not null)
            {
                StateHasChanged();
                await LoadOrdersAsync(customerId, cancellation.Token);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Et kundeskifte eller navigation væk har afbrudt indlæsningen.
        }
        finally
        {
            if (ReferenceEquals(_loadCancellation, cancellation))
            {
                _loadCancellation = null;
            }
        }
    }

    private async Task LoadCustomerAsync(Guid customerId, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await Http.GetAsync(
                $"api/customers/{customerId}", cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                ErrorMessage = "Kunden blev ikke fundet.";
                return;
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized ||
                response.StatusCode == HttpStatusCode.Forbidden)
            {
                ErrorMessage = "Du har ikke adgang til denne kunde.";
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = "Kunden kunne ikke indlæses.";
                return;
            }

            var customer = await response.Content
                .ReadFromJsonAsync<CustomerDetailsDto>(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Customer = customer;

            if (Customer is null)
            {
                ErrorMessage = "Kunden kunne ikke indlæses.";
            }
        }
        catch (HttpRequestException)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                ErrorMessage = "Kunden kunne ikke indlæses. Prøv igen senere.";
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            ErrorMessage = "Indlæsningen af kunden tog for lang tid. Prøv igen senere.";
        }
        catch (Microsoft.JSInterop.JSException)
        {
            if (!cancellationToken.IsCancellationRequested)
                ErrorMessage = "Browseren kunne ikke læse dit login. Kontrollér browserlagring og log ind igen.";
        }
        catch (JsonException)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                ErrorMessage = "Kunden kunne ikke indlæses. Prøv igen senere.";
            }
        }
    }

    private async Task LoadOrdersAsync(Guid customerId, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await Http.GetAsync(
                $"api/orders?customerId={customerId}", cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (response.StatusCode == HttpStatusCode.Unauthorized ||
                response.StatusCode == HttpStatusCode.Forbidden)
            {
                OrdersErrorMessage = "Du har ikke adgang til kundens ordrer.";
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                OrdersErrorMessage = "Kundens ordrer kunne ikke indlæses.";
                return;
            }

            var orders = await response.Content
                .ReadFromJsonAsync<List<AdminOrderSummaryDto>>(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Orders = orders ?? [];
        }
        catch (HttpRequestException)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                OrdersErrorMessage = "Kundens ordrer kunne ikke indlæses. Prøv igen senere.";
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            OrdersErrorMessage = "Indlæsningen af ordrer tog for lang tid. Prøv igen senere.";
        }
        catch (Microsoft.JSInterop.JSException)
        {
            if (!cancellationToken.IsCancellationRequested)
                OrdersErrorMessage = "Browseren kunne ikke læse dit login. Kontrollér browserlagring og log ind igen.";
        }
        catch (JsonException)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                OrdersErrorMessage = "Kundens ordrer kunne ikke indlæses. Prøv igen senere.";
            }
        }
    }

    public void Dispose()
    {
        _loadCancellation?.Cancel();
    }
}
