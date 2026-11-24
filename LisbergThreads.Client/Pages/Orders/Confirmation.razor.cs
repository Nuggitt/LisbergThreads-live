using System.Net.Http.Json;
using LisbergThreads.Contracts.Orders;
using Microsoft.AspNetCore.Components;

namespace LisbergThreads.Client.Pages.Orders;

public partial class Confirmation : ComponentBase
{
    [Parameter]
    public Guid OrderId { get; set; }

    [SupplyParameterFromQuery(Name = "token")]
    public Guid? ConfirmationToken { get; set; }

    [SupplyParameterFromQuery(Name = "cartCleanupFailed")]
    public bool CartCleanupFailed { get; set; }

    [Inject]
    public HttpClient Http { get; set; } = null!;

    protected OrderDetailsDto? Order { get; set; }

    protected bool IsLoading { get; set; } = true;

    protected string? ErrorMessage { get; set; }
    private int loadVersion;

    protected override async Task OnParametersSetAsync()
    {
        await LoadOrderAsync();
    }

    private async Task LoadOrderAsync()
    {
        var version = ++loadVersion;
        var orderId = OrderId;
        var token = ConfirmationToken;
        IsLoading = true;
        ErrorMessage = null;
        Order = null;

        try
        {
            var url = $"api/orders/{orderId}";

            if (token.HasValue)
            {
                url += $"?token={token.Value}";
            }

            using var response = await Http.GetAsync(url);
            if (version != loadVersion) return;

            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = response.StatusCode switch
                {
                    System.Net.HttpStatusCode.NotFound
                        => "Ordren blev ikke fundet.",

                    System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden
                        => "Du har ikke adgang til denne ordre.",

                    _ => "Ordren kunne ikke indlæses."
                };

                return;
            }

            var order =
                await response.Content.ReadFromJsonAsync<OrderDetailsDto>();
            if (version != loadVersion) return;
            Order = order;

            if (Order is null)
            {
                ErrorMessage = "Ordren kunne ikke indlæses.";
            }
        }
        catch (HttpRequestException)
        {
            if (version == loadVersion) ErrorMessage = "Ordren kunne ikke hentes. Kontrollér forbindelsen og tryk Prøv igen. Det opretter ikke en ny ordre.";
        }
        catch (OperationCanceledException)
        {
            if (version == loadVersion) ErrorMessage = "Indlæsningen tog for lang tid. Tryk Prøv igen for at hente ordrebekræftelsen.";
        }
        catch (System.Text.Json.JsonException)
        {
            if (version == loadVersion) ErrorMessage = "Ordresvaret kunne ikke læses. Tryk Prøv igen for at hente ordren; undlad at bestille igen.";
        }
        finally
        {
            if (version == loadVersion) IsLoading = false;
        }
    }
}
