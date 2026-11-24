using System.Net;
using LisbergThreads.Contracts.Content;
using System.Net.Http.Json;
using System.Text.Json;
using LisbergThreads.Contracts.Products;
using Microsoft.AspNetCore.Components;

namespace LisbergThreads.Client.Pages;

public partial class Home : ComponentBase
{
    [Inject] public HttpClient Http { get; set; } = null!;
    [Inject] public ILogger<Home> Logger { get; set; } = null!;

    private List<ProductDto> newProducts = new();
    private List<ProductDto> popularProducts = new();
    private bool isLoading = true;
    private string? loadError;
    private ProductDto? HeroProduct => newProducts.Concat(popularProducts).FirstOrDefault(p => GetImageUrl(p) is not null);

    private Dictionary<string, ContentTextDto> contentTexts = new();
    private bool contentLoaded;
    private string? contentError;
    private string? contentSaved;

    protected override Task OnInitializedAsync() => Task.WhenAll(LoadProductsAsync(), LoadContentAsync());

    private string GetText(string key) => contentTexts.TryGetValue(key, out var text)
        ? text.Value : HomeTextDefaults.Values[key];

    private async Task LoadContentAsync()
    {
        contentLoaded = false;
        contentError = null;
        try
        {
            var texts = await Http.GetFromJsonAsync<List<ContentTextDto>>("api/content/home")
                ?? throw new JsonException("Missing homepage text data.");
            contentTexts = texts.ToDictionary(t => t.Key);
            contentLoaded = true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or Microsoft.JSInterop.JSException)
        {
            Logger.LogWarning(ex, "Could not load homepage content.");
            contentError = "Forsideteksterne kunne ikke hentes. Standardtekster vises, og redigering er midlertidigt slået fra.";
        }
    }

    private async Task<string?> SaveTextAsync(string key, string value)
    {
        if (!contentLoaded) return "Hent forsideteksterne igen, før du redigerer.";
        if (string.IsNullOrWhiteSpace(value) || value.Length > 2000)
            return "Skriv mellem 1 og 2000 tegn.";
        contentSaved = null;
        contentTexts.TryGetValue(key, out var current);
        using var response = await Http.PutAsJsonAsync($"api/content/home/{key}",
            new UpdateContentTextDto(value, current?.RowVersionBase64));
        if (response.StatusCode == HttpStatusCode.Conflict)
            return "Teksten er ændret i mellemtiden. Kopiér din rettelse, og genindlæs siden, før du prøver igen.";
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return "Du skal være logget ind som administrator for at gemme.";
        if (!response.IsSuccessStatusCode) return "Teksten kunne ikke gemmes. Prøv igen om lidt.";
        var saved = await response.Content.ReadFromJsonAsync<ContentTextDto>()
            ?? throw new JsonException("Missing saved text.");
        contentTexts[key] = saved;
        contentSaved = "Teksten er gemt.";
        await InvokeAsync(StateHasChanged);
        return null;
    }

    private async Task LoadProductsAsync()
    {
        isLoading = true;
        loadError = null;
        try
        {
            var products = await Http.GetFromJsonAsync<HomeProductsDto>("api/products/home")
                ?? throw new JsonException("Missing homepage product data.");
            newProducts = products.NewProducts;
            popularProducts = products.PopularProducts;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or Microsoft.JSInterop.JSException)
        {
            Logger.LogWarning(ex, "Could not load homepage products.");
            loadError = "Vi kunne ikke hente produkterne lige nu. Prøv igen om lidt.";
        }
        finally
        {
            isLoading = false;
        }
    }

    private string? GetImageUrl(ProductDto product)
    {
        var path = !string.IsNullOrWhiteSpace(product.ImageUrl)
            ? product.ImageUrl
            : product.AdditionalImageUrls?.FirstOrDefault(url => !string.IsNullOrWhiteSpace(url));
        if (string.IsNullOrWhiteSpace(path)) return null;
        return new Uri(Http.BaseAddress!, path.Trim().TrimStart('/')).ToString();
    }
}
