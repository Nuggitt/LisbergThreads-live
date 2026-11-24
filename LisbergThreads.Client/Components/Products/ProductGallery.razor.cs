using LisbergThreads.Contracts.Products;
using Microsoft.AspNetCore.Components;

namespace LisbergThreads.Client.Components.Products;

public partial class ProductGallery : ComponentBase
{
    [Inject]
    public HttpClient Http { get; set; } = null!;

    [Parameter, EditorRequired]
    public ProductDto Product { get; set; } = null!;

    [Parameter]
    public bool Compact { get; set; }

    [Parameter]
    public bool LinkToProduct { get; set; }

    private List<string> imageUrls = new();
    private Guid productId;
    private int selectedIndex;

    protected override void OnParametersSet()
    {
        var urls = new List<string>();
        if (!string.IsNullOrWhiteSpace(Product.ImageUrl))
        {
            urls.Add(Product.ImageUrl);
        }

        urls.AddRange(Product.AdditionalImageUrls ?? new List<string>());
        urls = urls.Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (productId != Product.Id || !imageUrls.SequenceEqual(urls))
        {
            selectedIndex = 0;
        }

        productId = Product.Id;
        imageUrls = urls;
    }

    private void MoveImage(int direction)
    {
        if (imageUrls.Count > 1)
        {
            selectedIndex = (selectedIndex + direction + imageUrls.Count) % imageUrls.Count;
        }
    }

    private string GetImageUrl(string imageUrl)
    {
        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return imageUrl;
        }

        return new Uri(Http.BaseAddress!, imageUrl.TrimStart('/')).ToString();
    }
}
