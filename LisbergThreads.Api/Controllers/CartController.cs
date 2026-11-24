using LisbergThreads.Api.Data;
using LisbergThreads.Api.Models;
using LisbergThreads.Contracts.Orders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using LisbergThreads.Api.Services;
using Microsoft.AspNetCore.RateLimiting;

namespace LisbergThreads.Api.Controllers
{
    [ApiController]
    [Route("api/cart")]
    [Authorize]
    public class CartController : ControllerBase
    {
        private readonly AppDbContext _db;

        public CartController(AppDbContext db)
        {
            _db = db;
        }

        [HttpPost("items")]
        public async Task<IActionResult> AddToCart(AddToCartDto request)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();
            if (request.Quantity <= 0)
                return BadRequest("Antal skal være større end 0.");

            var userId = GetUserId();

            var product = await _db.Products
                .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.IsActive);

            if (product is null)
                return NotFound("Produktet findes ikke.");

            if (product.StockQuantity < request.Quantity)
                return BadRequest($"Der er kun {product.StockQuantity} stk. af {product.Name} på lager.");

            var order = await FindCartForUpdateAsync(userId);

            if (order is null)
            {
                order = new Order
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    Status = "Pending",
                    IsActive = true,
                    Total = 0
                };

                _db.Orders.Add(order);
                await _db.SaveChangesAsync();
            }

            if (await HasActivePaymentAsync(order.Id))
                return Conflict("Kurven har en igangværende MobilePay-betaling. Åbn checkout for at følge betalingen.");

            var orderItem = await _db.OrderItems
                .FirstOrDefaultAsync(x => x.OrderId == order.Id && x.ProductId == request.ProductId);

            var existingQuantity = await _db.OrderItems
                .Where(i => i.OrderId == order.Id && i.ProductId == request.ProductId)
                .SumAsync(i => (long)i.Quantity);
            if (existingQuantity + request.Quantity > product.StockQuantity)
                return BadRequest($"Der er kun {product.StockQuantity} stk. af {product.Name} på lager. Antallet i kurven er medregnet.");

            if (orderItem is null)
            {
                orderItem = new OrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    ProductId = product.Id,
                    Quantity = request.Quantity,
                    UnitPrice = product.Price
                };

                _db.OrderItems.Add(orderItem);
            }
            else
            {
                orderItem.Quantity += request.Quantity;
            }

            await _db.SaveChangesAsync();

            order.Total = await _db.OrderItems
                .Where(x => x.OrderId == order.Id)
                .SumAsync(x => x.Quantity * x.UnitPrice);

            await _db.SaveChangesAsync();

            await transaction.CommitAsync();

            return Ok(new
            {
                message = "Produktet blev tilføjet til kurven.",
                total = order.Total
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetCart()
        {
            var userId = GetUserId();

            var order = await _db.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.UserId == userId && o.Status == "Pending" && o.IsActive);

            if (order is null)
            {
                return Ok(new CartDto(Guid.Empty, new List<CartItemDto>(), 0));
            }

            var productIds = order.OrderItems.Select(x => x.ProductId).ToList();

            var products = await _db.Products
                .Where(p => productIds.Contains(p.Id))
                .ToListAsync();

            var items = order.OrderItems.Select(oi =>
            {
                var product = products.First(p => p.Id == oi.ProductId);

                return new CartItemDto(
                    oi.ProductId,
                    product.Name,
                    product.Size,
                    product.ImageUrl,
                    oi.UnitPrice,
                    oi.Quantity,
                    oi.LineTotal
                );
            }).ToList();

            var cart = new CartDto(
                order.Id,
                items,
                order.Total
            );

            return Ok(cart);
        }

        [HttpDelete("items/{productId:guid}")]
        public async Task<IActionResult> RemoveFromCart(Guid productId)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();
            var userId = GetUserId();

            var order = await FindCartForUpdateAsync(userId);

            if (order is null)
                return NotFound("Kurven findes ikke.");

            if (await HasActivePaymentAsync(order.Id))
                return Conflict("Kurven har en igangværende MobilePay-betaling. Åbn checkout for at følge betalingen.");

            var orderItem = await _db.OrderItems
                .FirstOrDefaultAsync(x => x.OrderId == order.Id && x.ProductId == productId);

            if (orderItem is null)
                return NotFound("Varen findes ikke i kurven.");

            _db.OrderItems.Remove(orderItem);

            await _db.SaveChangesAsync();

            order.Total = await _db.OrderItems
                .Where(x => x.OrderId == order.Id)
                .SumAsync(x => x.Quantity * x.UnitPrice);

            await _db.SaveChangesAsync();

            await transaction.CommitAsync();

            return Ok(new
            {
                message = "Produktet blev fjernet fra kurven.",
                total = order.Total
            });
        }

        [HttpPut("items/{productId:guid}")]
        public async Task<IActionResult> UpdateQuantity(Guid productId, [FromBody] int quantity)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();
            if (quantity < 1)
                return BadRequest("Antal skal være mindst 1.");

            var userId = GetUserId();

            var order = await FindCartForUpdateAsync(userId);

            if (order is null)
                return NotFound("Kurven findes ikke.");

            if (await HasActivePaymentAsync(order.Id))
                return Conflict("Kurven har en igangværende MobilePay-betaling. Åbn checkout for at følge betalingen.");

            var orderItem = await _db.OrderItems
                .FirstOrDefaultAsync(x => x.OrderId == order.Id && x.ProductId == productId);

            if (orderItem is null)
                return NotFound("Varen findes ikke i kurven.");

            var product = await _db.Products.FindAsync(productId);
            if (product is null || !product.IsActive)
                return BadRequest("Produktet er ikke længere tilgængeligt.");

            var otherQuantity = await _db.OrderItems
                .Where(i => i.OrderId == order.Id && i.ProductId == productId && i.Id != orderItem.Id)
                .SumAsync(i => (long)i.Quantity);
            if (quantity + otherQuantity > product.StockQuantity)
                return BadRequest($"Der er kun {product.StockQuantity} stk. af {product.Name} på lager.");

            orderItem.Quantity = quantity;

            await _db.SaveChangesAsync();

            order.Total = await _db.OrderItems
                .Where(x => x.OrderId == order.Id)
                .SumAsync(x => x.Quantity * x.UnitPrice);

            await _db.SaveChangesAsync();

            await transaction.CommitAsync();

            return Ok(new
            {
                message = "Antal opdateret.",
                total = order.Total
            });
        }

        [HttpGet("count")]
        public async Task<IActionResult> GetCartCount()
        {
            var userId = GetUserId();

            var order = await _db.Orders
                .FirstOrDefaultAsync(o => o.UserId == userId && o.Status == "Pending" && o.IsActive);

            if (order is null)
                return Ok(0);

            var count = await _db.OrderItems
                .Where(x => x.OrderId == order.Id)
                .SumAsync(x => x.Quantity);

            return Ok(count);
        }

        [AllowAnonymous]
        [HttpPost("checkout")]
        [EnableRateLimiting("checkout")]
        [RequestSizeLimit(64 * 1024)]
        public async Task<ActionResult<MobilePayStartDto>> Checkout(
            CheckoutRequestDto request,
            [FromServices] MobilePayPaymentService payments,
            CancellationToken cancellationToken)
        {
            if (User.Identity?.IsAuthenticated != true && Request.Headers.ContainsKey("Authorization"))
                return Unauthorized();
            Guid? userId = User.Identity?.IsAuthenticated == true ? GetUserId() : null;
            try
            {
                return Ok(await payments.StartAsync(request, userId, cancellationToken));
            }
            catch (CheckoutRejectedException ex) { return StatusCode(ex.StatusCode, ex.Message); }
            catch (Exception ex) when (ex is MobilePayException or HttpRequestException or TaskCanceledException
                                       or System.Text.Json.JsonException or DbUpdateConcurrencyException)
            {
                return StatusCode(502, "MobilePay kunne ikke bekræftes. Genåbn betalingsstatus, før du prøver igen.");
            }
        }

        private Task<Order?> FindCartForUpdateAsync(Guid userId) => _db.Orders
            .FromSqlInterpolated($"SELECT * FROM Orders WITH (UPDLOCK, ROWLOCK) WHERE UserId = {userId} AND Status = N'Pending' AND IsActive = 1")
            .FirstOrDefaultAsync();

        private Task<bool> HasActivePaymentAsync(Guid cartId) => _db.MobilePayPayments
            .AnyAsync(p => p.CartId == cartId && p.IsActive);

        private Guid GetUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userIdClaim))
            {
                throw new UnauthorizedAccessException("Bruger-id mangler i token.");
            }

            return Guid.Parse(userIdClaim);
        }
    }
}
