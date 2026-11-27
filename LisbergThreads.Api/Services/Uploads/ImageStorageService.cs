using LisbergThreads.Api.Services.Errors;

namespace LisbergThreads.Api.Services.Uploads;

// Stores uploaded product and content images under a random file name.
public sealed class ImageStorageService(ProductUploadPath uploadPath)
{
    private const long MaxFileSize = 5 * 1024 * 1024;
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    // Returns the public URL of the stored image.
    public async Task<string> SaveAsync(IFormFile? file, CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
            throw new ServiceException(ServiceError.Invalid, "Vælg en billedfil.");
        if (file.Length > MaxFileSize)
            throw new ServiceException(ServiceError.Invalid, "Billedet må højst fylde 5 MB.");
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            throw new ServiceException(ServiceError.Invalid, "Kun JPG-, JPEG-, PNG- og WebP-billeder er tilladt.");

        Directory.CreateDirectory(uploadPath.Value);
        var fileName = $"{Guid.NewGuid()}{extension}";
        await using var stream = new FileStream(Path.Combine(uploadPath.Value, fileName), FileMode.Create);
        await file.CopyToAsync(stream, cancellationToken);
        return $"/uploads/products/{fileName}";
    }
}
