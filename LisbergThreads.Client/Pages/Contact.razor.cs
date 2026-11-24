using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LisbergThreads.Contracts.Contact;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace LisbergThreads.Client.Pages;

public partial class Contact : ComponentBase, IDisposable
{
    [Inject] public HttpClient Http { get; set; } = null!;
    private readonly CancellationTokenSource navigationCancellation = new();
    private ContactRequestDto model = new();
    private EditContext formContext = null!;
    private ContactInfoDto? info;
    private bool sending;
    private bool sent;
    private string? error;
    private string? success;

    protected override async Task OnInitializedAsync()
    {
        formContext = new EditContext(model);
        formContext.OnFieldChanged += FieldChanged;
        await LoadInfoAsync();
    }

    private void FieldChanged(object? sender, FieldChangedEventArgs args)
    {
        error = null;
        success = null;
        sent = false;
        if (args.FieldIdentifier.FieldName == nameof(ContactRequestDto.Topic))
        {
            if (!ContactTopics.UsesOrder(model.Topic)) model.OrderNumber = null;
            if (!ContactTopics.UsesProduct(model.Topic)) model.Product = null;
        }
    }

    private async Task LoadInfoAsync()
    {
        try
        {
            info = await Http.GetFromJsonAsync<ContactInfoDto>("api/contact", navigationCancellation.Token)
                ?? throw new JsonException();
        }
        catch (OperationCanceledException) when (navigationCancellation.IsCancellationRequested) { }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or Microsoft.JSInterop.JSException)
        {
            info = null;
        }
    }

    private async Task SendAsync()
    {
        if (sending || sent) return;
        error = null;
        success = null;
        if (!formContext.Validate()) return;
        sending = true;
        try
        {
            using var response = await Http.PostAsJsonAsync("api/contact", model, navigationCancellation.Token);
            if (!response.IsSuccessStatusCode)
            {
                error = response.StatusCode switch
                {
                    HttpStatusCode.BadRequest => "Kontrollér navn, e-mail, emne og besked samt felternes længde, og prøv igen.",
                    HttpStatusCode.TooManyRequests => "Der er sendt flere henvendelser på kort tid. Vent op til ti minutter, før du prøver igen.",
                    HttpStatusCode.ServiceUnavailable => "Beskeden kunne ikke sendes. Prøv igen senere; din tekst er bevaret.",
                    _ => "Afsendelsen kunne ikke bekræftes. Din tekst er bevaret; vent lidt, før du eventuelt prøver igen."
                };
                return;
            }
            success = "Tak for din besked! Den er sendt til Lisberg Threads. Svaret kommer til den e-mailadresse, du har angivet.";
            sent = true;
        }
        catch (OperationCanceledException) when (navigationCancellation.IsCancellationRequested) { }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or Microsoft.JSInterop.JSException)
        {
            error = "Afsendelsen kunne ikke bekræftes. Din tekst er bevaret; kontrollér forbindelsen og vent lidt, før du eventuelt prøver igen.";
        }
        finally { sending = false; }
    }

    public void Dispose()
    {
        navigationCancellation.Cancel();
        navigationCancellation.Dispose();
        formContext.OnFieldChanged -= FieldChanged;
    }
}
