using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LisbergThreads.Contracts.Products;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace LisbergThreads.Client.Components.Shared;

public partial class ContentImageEditor : ComponentBase
{
    [Inject] public HttpClient Http { get; set; } = null!;
    [Parameter] public string? ImageUrl { get; set; }
    [Parameter, EditorRequired] public string Label { get; set; } = string.Empty;
    [Parameter] public bool Enabled { get; set; } = true;
    [Parameter, EditorRequired] public Func<string, Task<string?>> OnSave { get; set; } = null!;

    private readonly string inputId = $"content-image-{Guid.NewGuid():N}";
    private bool busy;
    private string? error;

    private async Task UploadAsync(InputFileChangeEventArgs args)
    {
        if (busy || !Enabled) return;
        error = null;
        var file = args.File;
        if (file.Size is <= 0 or > 5 * 1024 * 1024)
        {
            error = "Vælg et billede på højst 5 MB.";
            return;
        }
        var extension = Path.GetExtension(file.Name).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp"))
        {
            error = "Vælg et JPG-, PNG- eller WebP-billede.";
            return;
        }
        busy = true;
        try
        {
            using var content = new MultipartFormDataContent();
            await using var stream = file.OpenReadStream(5 * 1024 * 1024);
            using var part = new StreamContent(stream);
            part.Headers.ContentType = new MediaTypeHeaderValue(extension switch
            {
                ".png" => "image/png", ".webp" => "image/webp", _ => "image/jpeg"
            });
            content.Add(part, "file", file.Name);
            using var response = await Http.PostAsync("api/uploads/content-image", content);
            if (!response.IsSuccessStatusCode)
            {
                error = response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Du skal være logget ind som administrator for at uploade billeder.",
                    HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge => "Vælg et JPG-, PNG- eller WebP-billede på højst 5 MB.",
                    _ => "Billedet kunne ikke uploades. Prøv igen senere."
                };
                return;
            }
            var result = await response.Content.ReadFromJsonAsync<UploadImageResponse>();
            if (string.IsNullOrWhiteSpace(result?.ImageUrl)) throw new JsonException();
            error = await OnSave(JsonSerializer.Serialize(new[] { result.ImageUrl }));
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or IOException or Microsoft.JSInterop.JSException)
        {
            error = "Billedændringen kunne ikke bekræftes. Genindlæs siden og kontrollér billedet, før du prøver igen.";
        }
        finally { busy = false; }
    }

    private async Task RemoveAsync()
    {
        if (busy || !Enabled) return;
        busy = true;
        error = null;
        try { error = await OnSave("[]"); }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or Microsoft.JSInterop.JSException)
        {
            error = "Fjernelsen kunne ikke bekræftes. Genindlæs siden og kontrollér billedet.";
        }
        finally { busy = false; }
    }
}
