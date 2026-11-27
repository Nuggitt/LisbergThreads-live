using LisbergThreads.Api.Services.Payments;
using LisbergThreads.Contracts.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LisbergThreads.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/payments/mobilepay")]
[EnableRateLimiting("payment-status")]
public sealed class PaymentsController(MobilePayPaymentService payments) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<MobilePayStatusDto>> Get(Guid id, [FromQuery] Guid token,
        CancellationToken cancellationToken)
    {
        if (token == Guid.Empty) return NotFound();
        var status = await payments.GetStatusAsync(id, token, cancellationToken);
        return status is null ? NotFound() : Ok(status);
    }
}
