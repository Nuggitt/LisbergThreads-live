using System.Security.Claims;
using LisbergThreads.Api.Services.Cart;
using LisbergThreads.Api.Services.Payments;
using LisbergThreads.Contracts.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LisbergThreads.Api.Controllers;

[ApiController]
[Route("api/cart")]
[Authorize]
public class CartController(CartService cart) : ControllerBase
{
    [HttpPost("items")]
    public async Task<IActionResult> AddToCart(AddToCartDto request)
    {
        var total = await cart.AddAsync(GetUserId(), request);
        return Ok(new { message = "Produktet blev tilføjet til kurven.", total });
    }

    [HttpGet]
    public async Task<IActionResult> GetCart() => Ok(await cart.GetAsync(GetUserId()));

    [HttpDelete("items/{productId:guid}")]
    public async Task<IActionResult> RemoveFromCart(Guid productId)
    {
        var total = await cart.RemoveAsync(GetUserId(), productId);
        return Ok(new { message = "Produktet blev fjernet fra kurven.", total });
    }

    [HttpPut("items/{productId:guid}")]
    public async Task<IActionResult> UpdateQuantity(Guid productId, [FromBody] int quantity)
    {
        var total = await cart.UpdateQuantityAsync(GetUserId(), productId, quantity);
        return Ok(new { message = "Antal opdateret.", total });
    }

    [HttpGet("count")]
    public async Task<IActionResult> GetCartCount() => Ok(await cart.CountAsync(GetUserId()));

    [AllowAnonymous]
    [HttpGet("checkout-options")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public CheckoutPaymentOptionsDto GetCheckoutOptions([FromServices] MobilePayPaymentService payments) =>
        payments.GetOptions();

    [AllowAnonymous]
    [HttpPost("checkout")]
    [EnableRateLimiting("checkout")]
    [RequestSizeLimit(64 * 1024)]
    public async Task<ActionResult<MobilePayStartDto>> Checkout(
        CheckoutRequestDto request,
        [FromServices] MobilePayPaymentService payments,
        CancellationToken cancellationToken)
    {
        // An invalid or expired token must not silently turn a customer into a guest.
        if (User.Identity?.IsAuthenticated != true && Request.Headers.ContainsKey("Authorization"))
            return Unauthorized();
        Guid? userId = User.Identity?.IsAuthenticated == true ? GetUserId() : null;
        return Ok(await payments.StartAsync(request, userId, cancellationToken));
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userIdClaim))
            throw new UnauthorizedAccessException("Bruger-id mangler i token.");
        return Guid.Parse(userIdClaim);
    }
}
