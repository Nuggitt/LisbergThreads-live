using LisbergThreads.Api.Services;
using LisbergThreads.Contracts.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
        try
        {
            var status = await payments.GetStatusAsync(id, token, cancellationToken);
            return status is null ? NotFound() : Ok(status);
        }
        catch (CheckoutRejectedException ex) { return StatusCode(ex.StatusCode, ex.Message); }
        catch (Exception ex) when (ex is MobilePayException or HttpRequestException or TaskCanceledException
                                   or System.Text.Json.JsonException or DbUpdateConcurrencyException)
        {
            return StatusCode(502, "Betalingen kunne ikke bekræftes endnu. Prøv at hente status igen.");
        }
    }
}
