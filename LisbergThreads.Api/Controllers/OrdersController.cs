using System.Security.Claims;
using LisbergThreads.Api.Services;
using LisbergThreads.Api.Data;
using LisbergThreads.Contracts.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LisbergThreads.Api.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _db;

    public OrdersController(AppDbContext db)
    {
        _db = db;
    }

    [AllowAnonymous]
    [HttpGet("{orderId:guid}")]
    public async Task<ActionResult<OrderDetailsDto>> GetOrder(
        Guid orderId,
        [FromQuery] Guid? token)
    {
        var order = await _db.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order is null || order.Status == "Pending")
        {
            return NotFound("Ordren blev ikke fundet.");
        }

        var isOwner = false;

        if (User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (Guid.TryParse(userIdClaim, out var userId))
            {
                isOwner = order.UserId == userId;
            }
        }

        var hasValidToken =
            token.HasValue &&
            order.ConfirmationToken == token.Value;

        var isAdmin = User.IsInRole("Admin");

        if (!isOwner && !isAdmin && !hasValidToken)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return Forbid();
            }

            return Unauthorized();
        }

        var productIds = order.OrderItems
            .Select(oi => oi.ProductId)
            .ToList();

        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var items = order.OrderItems
            .Where(oi => products.ContainsKey(oi.ProductId))
            .Select(oi =>
            {
                var product = products[oi.ProductId];

                return new OrderDetailsItemDto(
                    oi.ProductId,
                    product.Name,
                    product.Size,
                    product.ImageUrl,
                    oi.Quantity,
                    oi.UnitPrice,
                    oi.LineTotal
                );
            })
            .ToList();

        var result = new OrderDetailsDto(
            order.Id,
            order.CreatedAt,
            order.FirstName,
            order.LastName,
            order.Email,
            order.Phone,
            order.Address,
            order.PostalCode,
            order.City,
            order.PaymentMethod,
            order.PaymentStatus,
            order.Status,
            order.Total,
            items
        )
        {
            Shipping = order.ShippingMethodId is null ? null : new OrderShippingDto(
                order.ShippingMethodId, order.ShippingMethodName ?? "Levering", order.ShippingCarrier ?? "",
                order.ShippingPrice, order.ServicePointId is null ? null : new ShippingServicePointDto(
                    order.ServicePointId, order.ServicePointName ?? "", order.ServicePointAddress ?? "",
                    order.ServicePointAddress2, order.ServicePointPostalCode ?? "", order.ServicePointCity ?? "")),
            TrackingNumber = order.TrackingNumber
        };

        return Ok(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<ActionResult<List<AdminOrderSummaryDto>>> GetAllOrders(
        [FromQuery] Guid? customerId = null)
    {
        var query = _db.Orders
            .AsNoTracking()
            .Where(o => o.Status != "Pending");

        if (customerId.HasValue)
        {
            query = query.Where(o => o.UserId == customerId.Value);
        }

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new AdminOrderSummaryDto(
                o.Id,
                o.UserId,
                o.CreatedAt,
                o.FirstName,
                o.LastName,
                o.Email,
                o.Total,
                o.Status,
                o.PaymentStatus,
                o.OrderItems.Sum(oi => oi.Quantity)
            ))
            .ToListAsync();

        return Ok(orders);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{orderId:guid}/status")]
    public async Task<IActionResult> UpdateOrderStatus(
    Guid orderId,
    UpdateOrderStatusDto request,
    [FromServices] MobilePayPaymentService payments,
    CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Status))
        {
            return BadRequest("Ordrestatus skal angives.");
        }

        if (!OrderStatuses.All.Contains(request.Status))
        {
            return BadRequest("Ugyldig ordrestatus.");
        }

        var order = await _db.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order is null)
        {
            return NotFound("Ordren blev ikke fundet.");
        }

        if (order.Status == "Pending")
        {
            return BadRequest("En aktiv kurv kan ikke få ændret ordrestatus.");
        }

        // PUT skal kunne gentages uden at give fejl.
        if (order.Status == request.Status)
        {
            return NoContent();
        }

        if (!IsValidStatusTransition(order.Status, request.Status))
        {
            return BadRequest(
                $"Ordrestatus kan ikke ændres fra {order.Status} til {request.Status}.");
        }

        if (request.Status == OrderStatuses.Shipped && order.PaymentMethod == "MobilePay")
        {
            try { await payments.CaptureOrderAsync(order, cancellationToken); }
            catch (CheckoutRejectedException ex) { return StatusCode(ex.StatusCode, ex.Message); }
            catch (Exception ex) when (ex is MobilePayException or HttpRequestException or TaskCanceledException
                                       or System.Text.Json.JsonException)
            {
                return StatusCode(502, "Hævningen kunne ikke bekræftes. Hent ordren og prøv igen; betalingen hæves ikke to gange.");
            }
        }

        order.Status = request.Status;

        try { await _db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict("Ordren er ændret af en anden forespørgsel. Hent den igen før næste statusskift.");
        }

        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{orderId:guid}/tracking")]
    public async Task<IActionResult> UpdateTracking(Guid orderId, UpdateOrderTrackingDto request,
        CancellationToken cancellationToken)
    {
        var number = string.IsNullOrWhiteSpace(request.TrackingNumber) ? null : request.TrackingNumber.Trim();
        if (number?.Length > 100 || number?.Any(char.IsControl) == true)
            return BadRequest("Trackingnummer må højst være 100 tegn og må ikke indeholde kontroltegn.");
        var order = await _db.Orders.FirstOrDefaultAsync(order => order.Id == orderId, cancellationToken);
        if (order is null || order.Status == "Pending") return NotFound("Ordren blev ikke fundet.");
        if (order.TrackingNumber != request.ExpectedTrackingNumber)
            return Conflict("Trackingnummeret er ændret. Hent ordren igen.");
        order.TrackingNumber = number;
        try { await _db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Conflict("Ordren er ændret. Hent ordren igen."); }
        return NoContent();
    }

    private static bool IsValidStatusTransition(
    string currentStatus,
    string newStatus)
    {
        return currentStatus switch
        {
            OrderStatuses.Placed =>
                newStatus == OrderStatuses.Confirmed,

            OrderStatuses.Confirmed =>
                newStatus == OrderStatuses.Shipped,

            OrderStatuses.Shipped =>
                newStatus == OrderStatuses.Completed,

            OrderStatuses.Completed =>
                false,

            _ => false
        };
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<List<OrderSummaryDto>>> GetMyOrders()
    {
        var userIdClaim = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var orders = await _db.Orders
            .AsNoTracking()
            .Where(o =>
                o.UserId == userId &&
                o.Status != "Pending")
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new OrderSummaryDto(
                o.Id,
                o.CreatedAt,
                o.Total,
                o.Status,
                o.PaymentStatus,
                o.OrderItems.Sum(oi => oi.Quantity)
            ))
            .ToListAsync();

        return Ok(orders);
    }
}
