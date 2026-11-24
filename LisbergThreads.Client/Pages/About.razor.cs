using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LisbergThreads.Contracts.Content;
using Microsoft.AspNetCore.Components;

namespace LisbergThreads.Client.Pages;

public partial class About : ComponentBase, IDisposable
{
    [Inject] public HttpClient Http { get; set; } = null!;
    private readonly CancellationTokenSource navigationCancellation = new();
    private Dictionary<string, ContentTextDto> content = new();
    private bool loading = true;
    private bool loaded;
    private string? loadError;
    private string? savedMessage;

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        loading = true;
        loaded = false;
        loadError = null;
        try
        {
            var values = await Http.GetFromJsonAsync<List<ContentTextDto>>("api/content/about", navigationCancellation.Token)
                ?? throw new JsonException();
            content = values.ToDictionary(item => item.Key);
            loaded = true;
        }
        catch (OperationCanceledException) when (navigationCancellation.IsCancellationRequested) { }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or Microsoft.JSInterop.JSException)
        {
            loadError = "Siden kunne ikke hentes. Kontrollér forbindelsen og prøv igen.";
        }
        finally { loading = false; }
    }

    private string Text(string key) => content.TryGetValue(key, out var value) ? value.Value : AboutContentDefaults.Values[key];

    private string? Image(string key)
    {
        var images = JsonSerializer.Deserialize<string[]>(Text(key));
        var path = images?.FirstOrDefault();
        return string.IsNullOrWhiteSpace(path) ? null : new Uri(Http.BaseAddress!, path.TrimStart('/')).ToString();
    }

    private async Task<string?> SaveAsync(string key, string value)
    {
        if (!loaded) return "Genindlæs siden, før du redigerer.";
        if (string.IsNullOrWhiteSpace(value) || value.Length > 2000) return "Skriv mellem 1 og 2000 tegn.";
        savedMessage = null;
        content.TryGetValue(key, out var current);
        try
        {
            using var response = await Http.PutAsJsonAsync($"api/content/about/{key}",
                new UpdateContentTextDto(value, current?.RowVersionBase64), navigationCancellation.Token);
            if (response.StatusCode == HttpStatusCode.Conflict)
                return "Indholdet er ændret i mellemtiden. Kopiér din tekst og genindlæs siden, før du prøver igen.";
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return "Du skal være logget ind som administrator for at gemme.";
            if (!response.IsSuccessStatusCode) return "Ændringen kunne ikke gemmes. Kontrollér indholdet og prøv igen.";
            var saved = await response.Content.ReadFromJsonAsync<ContentTextDto>(navigationCancellation.Token)
                ?? throw new JsonException();
            content[key] = saved;
            savedMessage = AboutContentDefaults.IsImageKey(key) ? "Billedændringen er gemt." : "Teksten er gemt.";
            await InvokeAsync(StateHasChanged);
            return null;
        }
        catch (OperationCanceledException) when (navigationCancellation.IsCancellationRequested)
        {
            return "Siden er lukket. Genindlæs for at kontrollere ændringen.";
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or Microsoft.JSInterop.JSException)
        {
            return "Ændringen kunne ikke bekræftes. Genindlæs siden og kontrollér indholdet, før du prøver igen.";
        }
    }

    public void Dispose()
    {
        navigationCancellation.Cancel();
        navigationCancellation.Dispose();
    }
}
