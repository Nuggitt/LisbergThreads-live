using LisbergThreads.Contracts.Products;
using LisbergThreads.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LisbergThreads.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UploadsController : ControllerBase
{
    private readonly ProductUploadPath _uploadPath;

    public UploadsController(ProductUploadPath uploadPath)
    {
        _uploadPath = uploadPath;
    }

    [HttpPost("product-image")]
    [HttpPost("content-image")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UploadImageResponse>> UploadProductImage(
        IFormFile file)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }

        if (file.Length > 5 * 1024 * 1024)
            return BadRequest("Billedet må højst fylde 5 MB.");

        var allowedExtensions = new[]
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp"
        };

        var extension = Path
            .GetExtension(file.FileName)
            .ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
        {
            return BadRequest(
                "Only JPG, JPEG, PNG and WEBP files are allowed.");
        }

        var uploadsFolder = _uploadPath.Value;

        Directory.CreateDirectory(uploadsFolder);

        var fileName = $"{Guid.NewGuid()}{extension}";

        var filePath = Path.Combine(
            uploadsFolder,
            fileName);

        await using var stream =
            new FileStream(filePath, FileMode.Create);

        await file.CopyToAsync(stream);

        var imageUrl = $"/uploads/products/{fileName}";

        return Ok(new UploadImageResponse(imageUrl));
    }
}
