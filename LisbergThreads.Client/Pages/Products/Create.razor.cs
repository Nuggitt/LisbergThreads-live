using LisbergThreads.Contracts.Products;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Forms;
using System.Net.Http.Headers;
using LisbergThreads.Client.Services;

namespace LisbergThreads.Client.Pages.Products;

public partial class Create : ComponentBase
{
    [Inject]
    public HttpClient Http { get; set; } = null!;

    [Inject]
    public NavigationManager Navigation { get; set; } = null!;

    protected ProductFormModel Model { get; set; } = new();
    
    protected bool IsUploadingAdditionalImages { get; set; }

    protected bool IsSaving { get; set; }

    protected string? ErrorMessage { get; set; }

    protected async Task SaveAsync()
    {
        if (IsSaving || IsUploadingAdditionalImages)
        {
            return;
        }

        ErrorMessage = null;
        IsSaving = true;

        try
        {
            var request = new CreateProductDto(
                Model.Name,
                Model.Description,
                Model.Size,
                Model.Price,
                Model.StockQuantity,
                Model.ImageUrl,
                true,
                Model.AdditionalImageUrls.ToList()
            );

            var response = await Http.PostAsJsonAsync(
                "api/products",
                request);

            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = ProductFeedback.Error(response, "Produktet kunne ikke oprettes. Prøv igen senere.");

                return;
            }

            Navigation.NavigateTo("/products?saved=created");
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or Microsoft.JSInterop.JSException)
        {
            ErrorMessage = ProductFeedback.UnconfirmedSave;
        }
        finally
        {
            IsSaving = false;
        }
    }

    protected async Task UploadImageAsync(
    InputFileChangeEventArgs e)
    {
        ErrorMessage = null;

        var file = e.File;

        if (file.Size > 5 * 1024 * 1024)
        {
            ErrorMessage = "Billedet må højst fylde 5 MB.";
            return;
        }

        try
        {
            using var content =
                new MultipartFormDataContent();

            await using var stream =
                file.OpenReadStream(
                    maxAllowedSize: 5 * 1024 * 1024);

            using var fileContent =
                new StreamContent(stream);

            fileContent.Headers.ContentType =
                new MediaTypeHeaderValue(file.ContentType);

            content.Add(
                fileContent,
                "file",
                file.Name);

            var response = await Http.PostAsync(
                "api/uploads/product-image",
                content);

            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = response.StatusCode == System.Net.HttpStatusCode.BadRequest
                    ? "Vælg et JPG-, PNG- eller WebP-billede på højst 5 MB, og prøv igen."
                    : ProductFeedback.Error(response, "Billedet kunne ikke uploades. Prøv igen senere.");

                return;
            }

            var result =
                await response.Content
                    .ReadFromJsonAsync<UploadImageResponse>();

            if (result is not null && !string.IsNullOrWhiteSpace(result.ImageUrl))
            {
                Model.ImageUrl = result.ImageUrl;
            }
            else { ErrorMessage = "Upload kunne ikke bekræftes. Prøv igen."; }
        }
        catch (Exception)
        {
            ErrorMessage = "Billedet kunne ikke uploades. Kontrollér forbindelsen og prøv igen.";
        }
    }

    protected void Cancel()
    {
        Navigation.NavigateTo("/products");
    }

    protected class ProductFormModel
    {
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string? Size { get; set; }

        public decimal Price { get; set; }

        public int StockQuantity { get; set; }

        public string? ImageUrl { get; set; }
        
        public List<string> AdditionalImageUrls { get; set; } = new();
    }

    protected class UploadImageResponse
    {
        public string ImageUrl { get; set; } = string.Empty;
    }

    protected string? PreviewImageUrl
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Model.ImageUrl))
            {
                return null;
            }

            if (Model.ImageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                Model.ImageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return Model.ImageUrl;
            }

            return new Uri(
                Http.BaseAddress!,
                Model.ImageUrl.TrimStart('/')
            ).ToString();
        }
    }
    
    protected async Task UploadAdditionalImagesAsync(
    InputFileChangeEventArgs e)
{
    if (Model is null || IsSaving || IsUploadingAdditionalImages)
    {
        return;
    }

    ErrorMessage = null;
    IsUploadingAdditionalImages = true;

    try
    {
        var files = e.GetMultipleFiles(5);

        if (Model.AdditionalImageUrls.Count + files.Count > 5)
        {
            ErrorMessage = "Du kan højst tilføje 5 ekstra billeder.";
            return;
        }

        if (files.Any(file => file.Size > 5 * 1024 * 1024))
        {
            ErrorMessage = "Hvert billede må højst fylde 5 MB.";
            return;
        }

        foreach (var file in files)
        {
            using var content = new MultipartFormDataContent();

            await using var stream = file.OpenReadStream(
                maxAllowedSize: 5 * 1024 * 1024);

            using var fileContent = new StreamContent(stream);

            fileContent.Headers.ContentType =
                new MediaTypeHeaderValue(file.ContentType);

            content.Add(fileContent, "file", file.Name);

            using var response = await Http.PostAsync(
                "api/uploads/product-image",
                content);

            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage =
                    $"Kunne ikke uploade {file.Name}. "
                    + "Tidligere uploadede billeder er stadig på listen.";
                return;
            }

            var result = await response.Content.ReadFromJsonAsync<
                LisbergThreads.Contracts.Products.UploadImageResponse>();

            if (result is null
                || string.IsNullOrWhiteSpace(result.ImageUrl))
            {
                ErrorMessage = "Upload gav ingen billedadresse.";
                return;
            }

            Model.AdditionalImageUrls.Add(result.ImageUrl);
        }
    }
    catch (Exception)
    {
        ErrorMessage =
            "Billederne kunne ikke uploades. "
            + "Vælg højst 5 billeder, og prøv igen.";
    }
    finally
    {
        IsUploadingAdditionalImages = false;
    }
}

protected void RemoveAdditionalImage(string imageUrl)
{
    if (Model is null || IsSaving || IsUploadingAdditionalImages)
    {
        return;
    }

    Model.AdditionalImageUrls.Remove(imageUrl);
}

protected string GetAdditionalImageUrl(string imageUrl)
{
    if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp
            || uri.Scheme == Uri.UriSchemeHttps))
    {
        return imageUrl;
    }

    return new Uri(
        Http.BaseAddress!,
        imageUrl.TrimStart('/')).ToString();
}
}
