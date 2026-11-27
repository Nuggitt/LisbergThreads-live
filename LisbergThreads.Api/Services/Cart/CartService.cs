using LisbergThreads.Api.Data;
using LisbergThreads.Api.Models;
using LisbergThreads.Api.Services.Errors;
using LisbergThreads.Contracts.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace LisbergThreads.Api.Services.Cart;

// Cart rules for signed-in customers. A cart never reserves stock; checkout checks stock again.
public sealed class CartService(AppDbContext db)
{
    private const string ActivePaymentMessage =
        "Kurven har en igangværende MobilePay-betaling. Åbn checkout for at følge betalingen.";

    public async Task<decimal> AddAsync(Guid userId, AddToCartDto request)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        if (request.Quantity <= 0)
            throw new ServiceException(ServiceError.Invalid, "Antal skal være større end 0.");

        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId && p.IsActive)
            ?? throw new ServiceException(ServiceError.NotFound, "Produktet findes ikke.");
        if (product.StockQuantity < request.Quantity)
            throw new ServiceException(ServiceError.Invalid,
                $"Der er kun {product.StockQuantity} stk. af {product.Name} på lager.");

        var cart = await LockActiveCartAsync(userId);
        if (cart is null)
        {
            cart = new Order
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                Status = OrderStatuses.Pending,
                IsActive = true,
                Total = 0
            };
            db.Orders.Add(cart);
            await db.SaveChangesAsync();
        }
        await EnsureNoActivePaymentAsync(cart.Id);

        // Counts every line for the product, including repeated lines, together with the new quantity.
        var existingQuantity = cart.OrderItems.Where(i => i.ProductId == product.Id).Sum(i => (long)i.Quantity);
        if (existingQuantity + request.Quantity > product.StockQuantity)
            throw new ServiceException(ServiceError.Invalid,
                $"Der er kun {product.StockQuantity} stk. af {product.Name} på lager. Antallet i kurven er medregnet.");

        var item = cart.OrderItems.FirstOrDefault(i => i.ProductId == product.Id);
        if (item is null)
        {
            // Tracking the item also adds it to cart.OrderItems (EF Core relationship fix-up).
            db.OrderItems.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                OrderId = cart.Id,
                ProductId = product.Id,
                Quantity = request.Quantity,
                UnitPrice = product.Price
            });
        }
        else
        {
            item.Quantity += request.Quantity;
        }

        return await SaveTotalAsync(cart, transaction);
    }

    public async Task<decimal> RemoveAsync(Guid userId, Guid productId)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        var cart = await LockActiveCartAsync(userId)
            ?? throw new ServiceException(ServiceError.NotFound, "Kurven findes ikke.");
        await EnsureNoActivePaymentAsync(cart.Id);
        var item = cart.OrderItems.FirstOrDefault(i => i.ProductId == productId)
            ?? throw new ServiceException(ServiceError.NotFound, "Varen findes ikke i kurven.");

        cart.OrderItems.Remove(item);
        db.OrderItems.Remove(item);
        return await SaveTotalAsync(cart, transaction);
    }

    public async Task<decimal> UpdateQuantityAsync(Guid userId, Guid productId, int quantity)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        if (quantity < 1)
            throw new ServiceException(ServiceError.Invalid, "Antal skal være mindst 1.");

        var cart = await LockActiveCartAsync(userId)
            ?? throw new ServiceException(ServiceError.NotFound, "Kurven findes ikke.");
        await EnsureNoActivePaymentAsync(cart.Id);
        var item = cart.OrderItems.FirstOrDefault(i => i.ProductId == productId)
            ?? throw new ServiceException(ServiceError.NotFound, "Varen findes ikke i kurven.");

        var product = await db.Products.FindAsync(productId);
        if (product is null || !product.IsActive)
            throw new ServiceException(ServiceError.Invalid, "Produktet er ikke længere tilgængeligt.");

        var otherQuantity = cart.OrderItems
            .Where(i => i.ProductId == productId && i.Id != item.Id)
            .Sum(i => (long)i.Quantity);
        if (quantity + otherQuantity > product.StockQuantity)
            throw new ServiceException(ServiceError.Invalid,
                $"Der er kun {product.StockQuantity} stk. af {product.Name} på lager.");

        item.Quantity = quantity;
        return await SaveTotalAsync(cart, transaction);
    }

    public async Task<CartDto> GetAsync(Guid userId)
    {
        var cart = await db.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.UserId == userId && o.Status == OrderStatuses.Pending && o.IsActive);
        if (cart is null) return new CartDto(Guid.Empty, [], 0);

        var productIds = cart.OrderItems.Select(i => i.ProductId).ToList();
        var products = await db.Products.Where(p => productIds.Contains(p.Id)).ToListAsync();
        var items = cart.OrderItems.Select(item =>
        {
            var product = products.First(p => p.Id == item.ProductId);
            return new CartItemDto(item.ProductId, product.Name, product.Size, product.ImageUrl,
                item.UnitPrice, item.Quantity, item.LineTotal);
        }).ToList();
        return new CartDto(cart.Id, items, cart.Total);
    }

    public async Task<int> CountAsync(Guid userId)
    {
        var cart = await db.Orders
            .FirstOrDefaultAsync(o => o.UserId == userId && o.Status == OrderStatuses.Pending && o.IsActive);
        if (cart is null) return 0;
        return await db.OrderItems.Where(i => i.OrderId == cart.Id).SumAsync(i => i.Quantity);
    }

    // Locks the customer's active cart until the surrounding transaction ends.
    // Serializes cart changes with each other and with checkout.
    public Task<Order?> LockActiveCartAsync(Guid userId, CancellationToken cancellationToken = default) => db.Orders
        .FromSqlInterpolated(
            $"SELECT * FROM Orders WITH (UPDLOCK, ROWLOCK) WHERE UserId = {userId} AND Status = {OrderStatuses.Pending} AND IsActive = 1")
        .Include(o => o.OrderItems)
        .FirstOrDefaultAsync(cancellationToken);

    // A cart cannot change while a payment for it is in progress.
    private async Task EnsureNoActivePaymentAsync(Guid cartId)
    {
        if (await db.MobilePayPayments.AnyAsync(p => p.CartId == cartId && p.IsActive))
            throw new ServiceException(ServiceError.Conflict, ActivePaymentMessage);
    }

    private async Task<decimal> SaveTotalAsync(Order cart, IDbContextTransaction transaction)
    {
        cart.RecalculateTotal();
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return cart.Total;
    }
}
