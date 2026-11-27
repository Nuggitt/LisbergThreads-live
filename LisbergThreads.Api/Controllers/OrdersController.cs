using System.Security.Claims;
using LisbergThreads.Api.Services.Orders;
using LisbergThreads.Contracts.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LisbergThreads.Api.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController(OrderService orders) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("{orderId:guid}")]
    public async Task<ActionResult<OrderDetailsDto>> GetOrder(Guid orderId, [FromQuery] Guid? token) =>
        Ok(await orders.GetDetailsAsync(orderId, GetUserId(), User.Identity?.IsAuthenticated == true,
            User.IsInRole("Admin"), token));

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<ActionResult<List<AdminOrderSummaryDto>>> GetAllOrders([FromQuery] Guid? customerId = null) =>
        Ok(await orders.GetAllAsync(customerId));

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<List<OrderSummaryDto>>> GetMyOrders()
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        return Ok(await orders.GetForCustomerAsync(userId.Value));
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{orderId:guid}/status")]
    public async Task<IActionResult> UpdateOrderStatus(Guid orderId, UpdateOrderStatusDto request,
        CancellationToken cancellationToken)
    {
        await orders.UpdateStatusAsync(orderId, request.Status, cancellationToken);
        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{orderId:guid}/tracking")]
    public async Task<IActionResult> UpdateTracking(Guid orderId, UpdateOrderTrackingDto request,
        CancellationToken cancellationToken)
    {
        await orders.UpdateTrackingAsync(orderId, request, cancellationToken);
        return NoContent();
    }

    private Guid? GetUserId() =>
        User.Identity?.IsAuthenticated == true
        && Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? userId
            : null;
}
