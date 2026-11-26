using System.Data;
using System.Net.Mail;
using System.Text.Json;
using LisbergThreads.Api.Data;
using LisbergThreads.Api.Models;
using LisbergThreads.Contracts.Orders;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LisbergThreads.Api.Services;

public sealed class CheckoutRejectedException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public sealed class MobilePayPaymentService(AppDbContext db, MobilePayClient client, IConfiguration configuration,
    ShippingService shipping)
{
    private bool IsTestMode => configuration.GetValue<bool>("Checkout:AllowTestOrders")
        && !configuration.GetValue<bool>("MobilePay:Enabled");

    public CheckoutPaymentOptionsDto GetOptions() => new(IsTestMode, (IsTestMode || client.IsConfigured) && shipping.IsAvailable);

    public async Task<MobilePayStartDto> StartAsync(CheckoutRequestDto request, Guid? userId,
        CancellationToken cancellationToken)
    {
        var isTestOrder = IsTestMode;
        if (!isTestOrder && !client.IsConfigured)
            throw new CheckoutRejectedException(503, "MobilePay er endnu ikke aktiveret. Kontakt Lisberg Threads.");
        if (request.PaymentId == Guid.Empty || request.PaymentToken == Guid.Empty)
            throw new CheckoutRejectedException(400, "Betalingsforsøget mangler et id.");

        var existing = await db.MobilePayPayments.FindAsync([request.PaymentId], cancellationToken);
        if (existing is not null)
        {
            if (existing.AccessToken != request.PaymentToken || existing.UserId != userId)
                throw new CheckoutRejectedException(403, "Du har ikke adgang til denne betaling.");
            return await StartResultAsync(existing, cancellationToken);
        }

        if (request.PaymentMethod != (isTestOrder ? "Test" : "MobilePay"))
            throw new CheckoutRejectedException(400, "Betalingsmåden er ændret. Genindlæs checkout.");
        if (request.Items is not { Count: > 0 and <= 100 } || request.Items.Any(i => i is null || i.Quantity <= 0))
            throw new CheckoutRejectedException(400, "Kurven skal indeholde gyldige varer og positive antal.");
        var fields = new[] { request.FirstName, request.LastName, request.Email, request.Phone,
            request.Address, request.PostalCode, request.City };
        if (fields.Any(f => string.IsNullOrWhiteSpace(f) || f.Length > 254)
            || !MailAddress.TryCreate(request.Email.Trim(), out var email) || email.Address != request.Email.Trim())
            throw new CheckoutRejectedException(400, "Udfyld gyldige kontakt- og leveringsoplysninger.");

        var quantities = request.Items.GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(i => (long)i.Quantity));
        if (quantities.Values.Any(q => q > int.MaxValue))
            throw new CheckoutRejectedException(400, "Det ønskede antal er for stort.");

        var delivery = await shipping.ResolveAsync(request.Shipping, request.PostalCode, request.Address,
            request.City, cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        Order? cart = null;
        if (userId.HasValue)
        {
            cart = await db.Orders.FromSqlInterpolated(
                    $"SELECT * FROM Orders WITH (UPDLOCK, ROWLOCK) WHERE UserId = {userId.Value} AND Status = N'Pending' AND IsActive = 1")
                .Include(o => o.OrderItems).FirstOrDefaultAsync(cancellationToken);
            if (cart is null) throw new CheckoutRejectedException(400, "Kurven er tom.");
            var activePayment = await db.MobilePayPayments.FirstOrDefaultAsync(p =>
                p.CartId == cart.Id && p.IsActive, cancellationToken);
            if (activePayment is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return await StartResultAsync(activePayment, cancellationToken);
            }
            var cartQuantities = cart.OrderItems.GroupBy(i => i.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(i => (long)i.Quantity));
            if (cartQuantities.Count != quantities.Count || cartQuantities.Any(i =>
                    !quantities.TryGetValue(i.Key, out var quantity) || quantity != i.Value))
                throw new CheckoutRejectedException(409, "Kurven er ændret. Kontrollér varerne igen.");
        }

        var ids = quantities.Keys.ToList();
        var products = await db.Products.Where(p => ids.Contains(p.Id) && p.IsActive).ToListAsync(cancellationToken);
        if (products.Count != ids.Count || products.Any(p => p.StockQuantity < quantities[p.Id] || p.Price <= 0))
            throw new CheckoutRejectedException(400, "En vare er udsolgt eller ikke længere tilgængelig i det ønskede antal.");

        // Snapshot uses current database prices, never prices supplied by the browser or stored in an old cart.
        var snapshot = new Order
        {
            Id = cart?.Id ?? Guid.NewGuid(), UserId = userId,
            FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim(), Email = request.Email.Trim(),
            Phone = request.Phone.Trim(), Address = request.Address.Trim(), PostalCode = request.PostalCode.Trim(),
            City = request.City.Trim(), PaymentMethod = isTestOrder ? "Test" : "MobilePay",
            PaymentStatus = isTestOrder ? "Test" : "Reserved",
            Status = OrderStatuses.Placed, CreatedAt = DateTime.UtcNow
        };
        snapshot.ShippingMethodId = delivery.MethodId;
        snapshot.ShippingMethodName = delivery.MethodName;
        snapshot.ShippingCarrier = delivery.Carrier;
        snapshot.ShippingPrice = delivery.Price;
        snapshot.ServicePointId = delivery.ServicePoint?.Id;
        snapshot.ServicePointName = delivery.ServicePoint?.Name;
        snapshot.ServicePointAddress = delivery.ServicePoint?.Address;
        snapshot.ServicePointAddress2 = delivery.ServicePoint?.Address2;
        snapshot.ServicePointPostalCode = delivery.ServicePoint?.PostalCode;
        snapshot.ServicePointCity = delivery.ServicePoint?.City;
        foreach (var product in products)
            snapshot.OrderItems.Add(new OrderItem
            {
                Id = Guid.NewGuid(), OrderId = snapshot.Id, ProductId = product.Id,
                Quantity = (int)quantities[product.Id], UnitPrice = product.Price
            });
        snapshot.Total = snapshot.OrderItems.Sum(i => i.Quantity * i.UnitPrice) + snapshot.ShippingPrice;
        if (snapshot.Total <= 0 || snapshot.Total > int.MaxValue / 100m || snapshot.Total * 100m != decimal.Truncate(snapshot.Total * 100m))
            throw new CheckoutRejectedException(400, "Ordrebeløbet kan ikke betales med MobilePay.");

        var payment = new MobilePayPayment
        {
            Id = request.PaymentId, AccessToken = request.PaymentToken, UserId = userId, CartId = cart?.Id,
            Total = snapshot.Total, SnapshotJson = JsonSerializer.Serialize(snapshot),
            State = isTestOrder ? "TestPending" : "Creating",
            NextCheckAt = isTestOrder ? DateTime.UtcNow : DateTime.UtcNow.AddSeconds(30)
        };
        db.MobilePayPayments.Add(payment);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new CheckoutRejectedException(409, "Der findes allerede et betalingsforsøg. Genåbn checkout.");
        }
        await transaction.CommitAsync(cancellationToken);
        return await StartResultAsync(payment, cancellationToken);
    }

    private async Task<MobilePayStartDto> StartResultAsync(MobilePayPayment payment, CancellationToken cancellationToken)
    {
        if (payment.IsActive && payment.State == "TestPending")
        {
            if (!IsTestMode)
                throw new CheckoutRejectedException(503, "Testcheckout er ikke aktiveret på serveren.");
            await CompleteOrderAsync(payment, cancellationToken, isTestOrder: true);
        }
        else if (payment.IsActive && payment.State == "Creating")
        {
            payment.RedirectUrl = await client.CreateAsync(payment, cancellationToken);
            payment.State = "Waiting";
            payment.NextCheckAt = DateTime.UtcNow.AddSeconds(5);
            await db.SaveChangesAsync(cancellationToken);
        }
        return new MobilePayStartDto(payment.Id, payment.AccessToken, payment.RedirectUrl, payment.IsActive);
    }

    public async Task<MobilePayStatusDto?> GetStatusAsync(Guid id, Guid token, CancellationToken cancellationToken)
    {
        var payment = await db.MobilePayPayments.FindAsync([id], cancellationToken);
        if (payment is null || payment.AccessToken != token) return null;
        await ReconcileAsync(payment, cancellationToken);
        Guid? confirmationToken = null;
        if (payment.OrderId is { } orderId)
            confirmationToken = await db.Orders.Where(o => o.Id == orderId)
                .Select(o => (Guid?)o.ConfirmationToken).SingleAsync(cancellationToken);
        return new MobilePayStatusDto(payment.State, payment.IsActive, payment.OrderId, confirmationToken, payment.Total,
            payment.RedirectUrl);
    }

    public async Task ReconcileAsync(MobilePayPayment payment, CancellationToken cancellationToken)
    {
        if (!payment.IsActive || payment.NextCheckAt > DateTime.UtcNow) return;

        if (payment.State == "TestPending")
        {
            await StartResultAsync(payment, cancellationToken);
            return;
        }

        // Claim one polling interval using rowversion, including across multiple application instances.
        payment.NextCheckAt = DateTime.UtcNow.AddSeconds(10);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            await db.Entry(payment).ReloadAsync(cancellationToken);
            return;
        }

        var remote = await client.GetAsync(payment.Id, cancellationToken);
        if (remote is null)
        {
            if (payment.State == "Creating") await StartResultAsync(payment, cancellationToken);
            return;
        }
        ValidateDetails(payment, remote);
        if (payment.State == "CancelPending")
        {
            await CancelRejectedAsync(payment, remote, cancellationToken);
            return;
        }
        if (remote.State is "ABORTED" or "EXPIRED" or "TERMINATED"
            || remote.Aggregate.CancelledAmount.Value > 0 || remote.Aggregate.RefundedAmount.Value > 0)
        {
            payment.State = remote.State is "ABORTED" or "EXPIRED" ? remote.State : "Cancelled";
            payment.IsActive = false;
            await db.SaveChangesAsync(cancellationToken);
            return;
        }
        if (remote.State != "AUTHORIZED") return;
        if (remote.Aggregate.AuthorizedAmount.Value != MobilePayClient.MinorUnits(payment.Total))
            throw new MobilePayException();

        await CompleteOrderAsync(payment, cancellationToken);
        if (payment.State == "CancelPending") await CancelRejectedAsync(payment, remote, cancellationToken);
    }

    private async Task CompleteOrderAsync(MobilePayPayment payment, CancellationToken cancellationToken,
        bool isTestOrder = false)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // A database lock serializes completion with other requests and application instances.
        var locked = await db.MobilePayPayments.FromSqlInterpolated(
            $"SELECT * FROM MobilePayPayments WITH (UPDLOCK, ROWLOCK) WHERE Id = {payment.Id}")
            .AsNoTracking().SingleAsync(cancellationToken);
        db.Entry(payment).CurrentValues.SetValues(locked);
        db.Entry(payment).OriginalValues.SetValues(locked);
        if (!payment.IsActive || payment.State == "CancelPending") return;

        var snapshot = JsonSerializer.Deserialize<Order>(payment.SnapshotJson)
            ?? throw new InvalidOperationException("Payment snapshot is missing.");
        var ids = snapshot.OrderItems.Select(i => i.ProductId).ToList();
        var products = await db.Products.Where(p => ids.Contains(p.Id)).ToListAsync(cancellationToken);
        if (products.Count != ids.Count || snapshot.OrderItems.Any(i =>
                products.All(p => p.Id != i.ProductId || !p.IsActive || p.StockQuantity < i.Quantity)))
        {
            payment.State = isTestOrder ? "StockRejected" : "CancelPending";
            if (isTestOrder) payment.IsActive = false;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        Order order;
        if (payment.CartId is { } cartId)
        {
            order = await db.Orders.Include(o => o.OrderItems).SingleAsync(o => o.Id == cartId, cancellationToken);
            if (order.Status != "Pending") throw new InvalidOperationException("Payment cart is no longer pending.");
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
        foreach (var item in order.OrderItems)
        {
            var product = products.Single(p => p.Id == item.ProductId);
            product.StockQuantity -= item.Quantity;
            product.UpdatedAt = DateTime.UtcNow;
        }
        payment.OrderId = order.Id;
        payment.State = "Completed";
        payment.IsActive = false;
        // Order, stock and payment completion commit together. Product.RowVersion rejects overselling.
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task CancelRejectedAsync(MobilePayPayment payment, MobilePayDetails remote, CancellationToken cancellationToken)
    {
        if (remote.Aggregate.CapturedAmount.Value > 0) throw new MobilePayException();
        if (remote.Aggregate.CancelledAmount.Value < MobilePayClient.MinorUnits(payment.Total)
            && remote.State is not ("TERMINATED" or "ABORTED" or "EXPIRED"))
        {
            remote = await client.CancelAsync(payment, cancellationToken);
            ValidateDetails(payment, remote);
            if (remote.Aggregate.CancelledAmount.Value != MobilePayClient.MinorUnits(payment.Total)
                && remote.State != "TERMINATED") throw new MobilePayException();
        }
        payment.State = "StockRejected";
        payment.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task CaptureOrderAsync(Order order, CancellationToken cancellationToken)
    {
        var payment = await db.MobilePayPayments.SingleOrDefaultAsync(p => p.OrderId == order.Id, cancellationToken);
        if (payment is null)
            throw new CheckoutRejectedException(409, "Ordren har ingen verificeret MobilePay-betaling. Kontrollér den manuelt.");
        var remote = await client.GetAsync(payment.Id, cancellationToken) ?? throw new MobilePayException();
        ValidateDetails(payment, remote);
        var total = MobilePayClient.MinorUnits(payment.Total);
        if (remote.State != "AUTHORIZED" || remote.Aggregate.CancelledAmount.Value != 0
            || remote.Aggregate.RefundedAmount.Value != 0 || remote.Aggregate.AuthorizedAmount.Value != total)
            throw new CheckoutRejectedException(409, "MobilePay-reservationen kan ikke hæves. Kontrollér betalingen i erhvervsportalen.");
        if (remote.Aggregate.CapturedAmount.Value != total)
        {
            if (remote.Aggregate.CapturedAmount.Value != 0) throw new MobilePayException();
            remote = await client.CaptureAsync(payment, cancellationToken);
            ValidateDetails(payment, remote);
            if (remote.Aggregate.CapturedAmount.Value != total) throw new MobilePayException();
        }
        order.PaymentStatus = "Paid";
    }

    private static void ValidateDetails(MobilePayPayment payment, MobilePayDetails remote)
    {
        if (remote.Reference != payment.Id.ToString("N") || remote.Amount?.Currency != "DKK"
            || remote.Amount.Value != MobilePayClient.MinorUnits(payment.Total) || remote.Aggregate is null
            || remote.Aggregate.AuthorizedAmount?.Currency != "DKK" || remote.Aggregate.CapturedAmount?.Currency != "DKK"
            || remote.Aggregate.CancelledAmount?.Currency != "DKK" || remote.Aggregate.RefundedAmount?.Currency != "DKK")
            throw new MobilePayException();
    }
}
