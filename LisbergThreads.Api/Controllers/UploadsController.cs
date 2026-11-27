using LisbergThreads.Api.Services.Uploads;
using LisbergThreads.Contracts.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LisbergThreads.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UploadsController(ImageStorageService images) : ControllerBase
{
    [HttpPost("product-image")]
    [HttpPost("content-image")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UploadImageResponse>> UploadProductImage(IFormFile file) =>
        Ok(new UploadImageResponse(await images.SaveAsync(file)));
}
