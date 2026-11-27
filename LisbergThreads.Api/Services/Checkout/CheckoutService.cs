using System.Net.Mail;
using LisbergThreads.Api.Data;
using LisbergThreads.Api.Models;
using LisbergThreads.Api.Services.Errors;
using LisbergThreads.Api.Services.Shipping;
using LisbergThreads.Contracts.Orders;
using Microsoft.EntityFrameworkCore;

namespace LisbergThreads.Api.Services.Checkout;

public sealed record ValidatedCheckout(IReadOnlyDictionary<Guid, int> Quantities, OrderShippingDto Delivery);

// Order and stock rules for checkout, independent of the payment provider.
// Applies to guests and signed-in customers alike.
public sealed class CheckoutService(AppDbContext db, ShippingService shipping)
{
    // Validates customer input and delivery before any database transaction starts.
    public async Task<ValidatedCheckout> ValidateAsync(CheckoutRequestDto request, CancellationToken cancellationToken)
    {
        if (request.Items is not { Count: > 0 and <= 100 } || request.Items.Any(i => i is null || i.Quantity <= 0))
            throw Invalid("Kurven skal indeholde gyldige varer og positive antal.");
        var fields = new[] { request.FirstName, request.LastName, request.Email, request.Phone,
            request.Address, request.PostalCode, request.City };
        if (fields.Any(f => string.IsNullOrWhiteSpace(f) || f.Length > 254)
            || !MailAddress.TryCreate(request.Email.Trim(), out var email) || email.Address != request.Email.Trim())
            throw Invalid("Udfyld gyldige kontakt- og leveringsoplysninger.");

        // Repeated lines for the same product count together.
        var quantities = request.Items.GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(i => (long)i.Quantity));
        if (quantities.Values.Any(q => q > int.MaxValue))
            throw Invalid("Det ønskede antal er for stort.");

        var delivery = await shipping.ResolveAsync(request.Shipping, request.PostalCode, request.Address,
            request.City, cancellationToken);
        return new ValidatedCheckout(quantities.ToDictionary(q => q.Key, q => (int)q.Value), delivery);
    }

    // A signed-in customer's server cart must match the items shown in the browser.
    public static void EnsureMatchesCart(Order cart, IReadOnlyDictionary<Guid, int> quantities)
    {
        var cartQuantities = cart.OrderItems.GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(i => (long)i.Quantity));
        if (cartQuantities.Count != quantities.Count || cartQuantities.Any(i =>
                !quantities.TryGetValue(i.Key, out var quantity) || quantity != i.Value))
            throw new ServiceException(ServiceError.Conflict, "Kurven er ændret. Kontrollér varerne igen.");
    }

    // Builds the order snapshot from current database prices and stock; never from browser prices.
    public async Task<Order> CreateSnapshotAsync(CheckoutRequestDto request, Guid? userId, Guid? cartId,
        ValidatedCheckout checkout, string paymentMethod, string paymentStatus, CancellationToken cancellationToken)
    {
        var quantities = checkout.Quantities;
        var ids = quantities.Keys.ToList();
        var products = await db.Products.Where(p => ids.Contains(p.Id) && p.IsActive).ToListAsync(cancellationToken);
        if (products.Count != ids.Count || products.Any(p => p.StockQuantity < quantities[p.Id] || p.Price <= 0))
            throw Invalid("En vare er udsolgt eller ikke længere tilgængelig i det ønskede antal.");

        var delivery = checkout.Delivery;
        var snapshot = new Order
        {
            Id = cartId ?? Guid.NewGuid(),
            UserId = userId,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = request.Email.Trim(),
            Phone = request.Phone.Trim(),
            Address = request.Address.Trim(),
            PostalCode = request.PostalCode.Trim(),
            City = request.City.Trim(),
            PaymentMethod = paymentMethod,
            PaymentStatus = paymentStatus,
            Status = OrderStatuses.Placed,
            CreatedAt = DateTime.UtcNow,
            ShippingMethodId = delivery.MethodId,
            ShippingMethodName = delivery.MethodName,
            ShippingCarrier = delivery.Carrier,
            ShippingPrice = delivery.Price,
            ServicePointId = delivery.ServicePoint?.Id,
            ServicePointName = delivery.ServicePoint?.Name,
            ServicePointAddress = delivery.ServicePoint?.Address,
            ServicePointAddress2 = delivery.ServicePoint?.Address2,
            ServicePointPostalCode = delivery.ServicePoint?.PostalCode,
            ServicePointCity = delivery.ServicePoint?.City
        };
        foreach (var product in products)
            snapshot.OrderItems.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                OrderId = snapshot.Id,
                ProductId = product.Id,
                Quantity = quantities[product.Id],
                UnitPrice = product.Price
            });
        snapshot.RecalculateTotal();
        return snapshot;
    }

    // Rechecks stock, turns the snapshot into a placed order and deducts stock.
    // Returns null when stock is no longer sufficient. The caller saves and commits in its own
    // transaction, so the order and the stock change are stored together or not at all.
    public async Task<Order?> PlaceOrderAsync(Order snapshot, Guid? cartId, CancellationToken cancellationToken)
    {
        var ids = snapshot.OrderItems.Select(i => i.ProductId).ToList();
        var products = await db.Products.Where(p => ids.Contains(p.Id)).ToListAsync(cancellationToken);
        if (products.Count != ids.Count || snapshot.OrderItems.Any(i =>
                products.All(p => p.Id != i.ProductId || !p.IsActive || p.StockQuantity < i.Quantity)))
            return null;

        Order order;
        if (cartId is { } id)
        {
            order = await db.Orders.Include(o => o.OrderItems).SingleAsync(o => o.Id == id, cancellationToken);
            if (order.Status != OrderStatuses.Pending)
                throw new InvalidOperationException("Payment cart is no longer pending.");
            // Discard stale cart prices; retain its id and confirmation token.
            snapshot.ConfirmationToken = order.ConfirmationToken;
            db.OrderItems.RemoveRange(order.OrderItems);
            db.Entry(order).CurrentValues.SetValues(snapshot);
            order.OrderItems = snapshot.OrderItems;
            db.OrderItems.AddRange(order.OrderItems);
        }
        else
        {
            order = snapshot;
            db.Orders.Add(order);
        }
        order.CreatedAt = DateTime.UtcNow;
        // Product.RowVersion rejects the save if another checkout changed the stock meanwhile.
        foreach (var item in order.OrderItems)
            products.Single(p => p.Id == item.ProductId).DeductStock(item.Quantity);
        return order;
    }

    private static ServiceException Invalid(string message) => new(ServiceError.Invalid, message);
}
