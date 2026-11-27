using LisbergThreads.Api.Services.Products;
using LisbergThreads.Contracts.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LisbergThreads.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController(ProductService products) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<ProductDto>> GetAll() => await products.GetActiveAsync();

    [HttpGet("home")]
    public Task<HomeProductsDto> GetHomeProducts(CancellationToken cancellationToken) =>
        products.GetHomeProductsAsync(cancellationToken);

    [HttpGet("all")]
    [Authorize(Roles = "Admin")]
    public async Task<IEnumerable<ProductDto>> GetAllIncludingInactive() =>
        await products.GetAllIncludingInactiveAsync();

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var product = await products.GetByIdAsync(id);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ProductDto>> Create([FromBody] CreateProductDto request)
    {
        var product = await products.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ProductDto>> Update(Guid id, [FromBody] UpdateProductDto request) =>
        Ok(await products.UpdateAsync(id, request));

    [HttpPatch("{id:guid}/field")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ProductDto>> UpdateField(
        Guid id, UpdateProductFieldDto request, CancellationToken cancellationToken) =>
        Ok(await products.UpdateFieldAsync(id, request, cancellationToken));

    [Authorize(Roles = "Admin")]
    [HttpPatch("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id)
    {
        await products.ActivateAsync(id);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await products.DeactivateAsync(id);
        return NoContent();
    }

    [HttpDelete("{id:guid}/permanent")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeletePermanently(Guid id)
    {
        await products.DeletePermanentlyAsync(id);
        return NoContent();
    }
}
