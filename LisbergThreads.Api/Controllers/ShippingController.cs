using LisbergThreads.Api.Services.Shipping;
using LisbergThreads.Contracts.Orders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LisbergThreads.Api.Controllers;

[ApiController]
[Route("api/shipping")]
public sealed class ShippingController(ShippingService shipping) : ControllerBase
{
    [HttpGet("methods")]
    public ActionResult<IReadOnlyList<ShippingMethodDto>> GetMethods() => Ok(shipping.GetMethods());

    [HttpGet("service-points")]
    [EnableRateLimiting("shipping")]
    public async Task<ActionResult<IReadOnlyList<ShippingServicePointDto>>> GetServicePoints(
        [FromQuery] string methodId, [FromQuery] string postalCode, [FromQuery] string address,
        [FromQuery] string city, CancellationToken cancellationToken) =>
        Ok(await shipping.GetServicePointsAsync(methodId, postalCode, address, city, cancellationToken));
}
