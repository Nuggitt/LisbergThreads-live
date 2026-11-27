using System.Data;
using System.Text.Json;
using LisbergThreads.Api.Data;
using LisbergThreads.Api.Models;
using LisbergThreads.Api.Services.Cart;
using LisbergThreads.Api.Services.Checkout;
using LisbergThreads.Api.Services.Errors;
using LisbergThreads.Api.Services.Shipping;
using LisbergThreads.Contracts.Orders;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LisbergThreads.Api.Services.Payments;

// The MobilePay payment flow: create, reconcile, cancel and capture. Order and stock rules live in
// CheckoutService; this class owns the payment record, its locks and the transaction boundaries.
public sealed class MobilePayPaymentService(AppDbContext db, MobilePayClient client, IConfiguration configuration,
    ShippingService shipping, CartService carts, CheckoutService checkout)
{
    private bool IsTestMode => configuration.GetValue<bool>("Checkout:AllowTestOrders")
        && !configuration.GetValue<bool>("MobilePay:Enabled");

    public CheckoutPaymentOptionsDto GetOptions() =>
        new(IsTestMode, (IsTestMode || client.IsConfigured) && shipping.IsAvailable);

    public Task<MobilePayStartDto> StartAsync(CheckoutRequestDto request, Guid? userId,
        CancellationToken cancellationToken) =>
        TranslateProviderFailuresAsync(() => StartCoreAsync(request, userId, cancellationToken),
            "MobilePay kunne ikke bekræftes. Genåbn betalingsstatus, før du prøver igen.");

    public Task<MobilePayStatusDto?> GetStatusAsync(Guid id, Guid token, CancellationToken cancellationToken) =>
        TranslateProviderFailuresAsync(() => GetStatusCoreAsync(id, token, cancellationToken),
            "Betalingen kunne ikke bekræftes endnu. Prøv at hente status igen.");

    public Task CaptureOrderAsync(Order order, CancellationToken cancellationToken) =>
        TranslateProviderFailuresAsync(async () =>
            {
                await CaptureCoreAsync(order, cancellationToken);
                return true;
            },
            "Hævningen kunne ikke bekræftes. Hent ordren og prøv igen; betalingen hæves ikke to gange.");

    private async Task<MobilePayStartDto> StartCoreAsync(CheckoutRequestDto request, Guid? userId,
        CancellationToken cancellationToken)
    {
        var isTestOrder = IsTestMode;
        if (!isTestOrder && !client.IsConfigured)
            throw new ServiceException(ServiceError.Unavailable, "MobilePay er endnu ikke aktiveret. Kontakt Lisberg Threads.");
        if (request.PaymentId == Guid.Empty || request.PaymentToken == Guid.Empty)
            throw new ServiceException(ServiceError.Invalid, "Betalingsforsøget mangler et id.");

        // The browser repeats the same payment id on retries, so a request is never paid twice.
        var existing = await db.MobilePayPayments.FindAsync([request.PaymentId], cancellationToken);
        if (existing is not null)
        {
            if (existing.AccessToken != request.PaymentToken || existing.UserId != userId)
                throw new ServiceException(ServiceError.Forbidden, "Du har ikke adgang til denne betaling.");
            return await StartResultAsync(existing, cancellationToken);
        }

        var paymentMethod = isTestOrder ? PaymentMethods.Test : PaymentMethods.MobilePay;
        if (request.PaymentMethod != paymentMethod)
            throw new ServiceException(ServiceError.Invalid, "Betalingsmåden er ændret. Genindlæs checkout.");
        var validated = await checkout.ValidateAsync(request, cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        Order? cart = null;
        if (userId.HasValue)
        {
            cart = await carts.LockActiveCartAsync(userId.Value, cancellationToken)
                ?? throw new ServiceException(ServiceError.Invalid, "Kurven er tom.");
            var activePayment = await db.MobilePayPayments.FirstOrDefaultAsync(p =>
                p.CartId == cart.Id && p.IsActive, cancellationToken);
            if (activePayment is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return await StartResultAsync(activePayment, cancellationToken);
            }
            CheckoutService.EnsureMatchesCart(cart, validated.Quantities);
        }

        var snapshot = await checkout.CreateSnapshotAsync(request, userId, cart?.Id, validated, paymentMethod,
            isTestOrder ? PaymentStatuses.Test : PaymentStatuses.Reserved, cancellationToken);
        if (snapshot.Total <= 0 || snapshot.Total > int.MaxValue / 100m
            || snapshot.Total * 100m != decimal.Truncate(snapshot.Total * 100m))
            throw new ServiceException(ServiceError.Invalid, "Ordrebeløbet kan ikke betales med MobilePay.");

        var payment = new MobilePayPayment
        {
            Id = request.PaymentId,
            AccessToken = request.PaymentToken,
            UserId = userId,
            CartId = cart?.Id,
            Total = snapshot.Total,
            SnapshotJson = JsonSerializer.Serialize(snapshot),
            State = isTestOrder ? PaymentStates.TestPending : PaymentStates.Creating,
            NextCheckAt = isTestOrder ? DateTime.UtcNow : DateTime.UtcNow.AddSeconds(30)
        };
        db.MobilePayPayments.Add(payment);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ServiceException(ServiceError.Conflict, "Der findes allerede et betalingsforsøg. Genåbn checkout.");
        }
        await transaction.CommitAsync(cancellationToken);
        return await StartResultAsync(payment, cancellationToken);
    }

    private async Task<MobilePayStartDto> StartResultAsync(MobilePayPayment payment, CancellationToken cancellationToken)
    {
        if (payment.IsActive && payment.State == PaymentStates.TestPending)
        {
            if (!IsTestMode)
                throw new ServiceException(ServiceError.Unavailable, "Testcheckout er ikke aktiveret på serveren.");
            await CompleteOrderAsync(payment, cancellationToken, isTestOrder: true);
        }
        else if (payment.IsActive && payment.State == PaymentStates.Creating)
        {
            payment.RedirectUrl = await client.CreateAsync(payment, cancellationToken);
            payment.State = PaymentStates.Waiting;
            payment.NextCheckAt = DateTime.UtcNow.AddSeconds(5);
            await db.SaveChangesAsync(cancellationToken);
        }
        return new MobilePayStartDto(payment.Id, payment.AccessToken, payment.RedirectUrl, payment.IsActive);
    }

    private async Task<MobilePayStatusDto?> GetStatusCoreAsync(Guid id, Guid token, CancellationToken cancellationToken)
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

        if (payment.State == PaymentStates.TestPending)
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
            if (payment.State == PaymentStates.Creating) await StartResultAsync(payment, cancellationToken);
            return;
        }
        ValidateDetails(payment, remote);
        if (payment.State == PaymentStates.CancelPending)
        {
            await CancelRejectedAsync(payment, remote, cancellationToken);
            return;
        }
        if (remote.State is "ABORTED" or "EXPIRED" or "TERMINATED"
            || remote.Aggregate.CancelledAmount.Value > 0 || remote.Aggregate.RefundedAmount.Value > 0)
        {
            payment.State = remote.State switch
            {
                "ABORTED" => PaymentStates.Aborted,
                "EXPIRED" => PaymentStates.Expired,
                _ => PaymentStates.Cancelled
            };
            payment.IsActive = false;
            await db.SaveChangesAsync(cancellationToken);
            return;
        }
        if (remote.State != "AUTHORIZED") return;
        if (remote.Aggregate.AuthorizedAmount.Value != MobilePayClient.MinorUnits(payment.Total))
            throw new MobilePayException();

        await CompleteOrderAsync(payment, cancellationToken);
        if (payment.State == PaymentStates.CancelPending) await CancelRejectedAsync(payment, remote, cancellationToken);
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
        if (!payment.IsActive || payment.State == PaymentStates.CancelPending) return;

        var snapshot = JsonSerializer.Deserialize<Order>(payment.SnapshotJson)
            ?? throw new InvalidOperationException("Payment snapshot is missing.");
        var order = await checkout.PlaceOrderAsync(snapshot, payment.CartId, cancellationToken);
        if (order is null)
        {
            payment.State = isTestOrder ? PaymentStates.StockRejected : PaymentStates.CancelPending;
            if (isTestOrder) payment.IsActive = false;
        }
        else
        {
            payment.OrderId = order.Id;
            payment.State = PaymentStates.Completed;
            payment.IsActive = false;
        }
        // Order, stock and payment state commit together.
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
        payment.State = PaymentStates.StockRejected;
        payment.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task CaptureCoreAsync(Order order, CancellationToken cancellationToken)
    {
        var payment = await db.MobilePayPayments.SingleOrDefaultAsync(p => p.OrderId == order.Id, cancellationToken)
            ?? throw new ServiceException(ServiceError.Conflict,
                "Ordren har ingen verificeret MobilePay-betaling. Kontrollér den manuelt.");
        var remote = await client.GetAsync(payment.Id, cancellationToken) ?? throw new MobilePayException();
        ValidateDetails(payment, remote);
        var total = MobilePayClient.MinorUnits(payment.Total);
        if (remote.State != "AUTHORIZED" || remote.Aggregate.CancelledAmount.Value != 0
            || remote.Aggregate.RefundedAmount.Value != 0 || remote.Aggregate.AuthorizedAmount.Value != total)
            throw new ServiceException(ServiceError.Conflict,
                "MobilePay-reservationen kan ikke hæves. Kontrollér betalingen i erhvervsportalen.");
        if (remote.Aggregate.CapturedAmount.Value != total)
        {
            if (remote.Aggregate.CapturedAmount.Value != 0) throw new MobilePayException();
            remote = await client.CaptureAsync(payment, cancellationToken);
            ValidateDetails(payment, remote);
            if (remote.Aggregate.CapturedAmount.Value != total) throw new MobilePayException();
        }
        order.PaymentStatus = PaymentStatuses.Paid;
    }

    private static void ValidateDetails(MobilePayPayment payment, MobilePayDetails remote)
    {
        if (remote.Reference != payment.Id.ToString("N") || remote.Amount?.Currency != "DKK"
            || remote.Amount.Value != MobilePayClient.MinorUnits(payment.Total) || remote.Aggregate is null
            || remote.Aggregate.AuthorizedAmount?.Currency != "DKK" || remote.Aggregate.CapturedAmount?.Currency != "DKK"
            || remote.Aggregate.CancelledAmount?.Currency != "DKK" || remote.Aggregate.RefundedAmount?.Currency != "DKK")
            throw new MobilePayException();
    }

    // Provider, network and concurrency failures leave the outcome unknown; the client must re-check status.
    private static async Task<T> TranslateProviderFailuresAsync<T>(Func<Task<T>> action, string message)
    {
        try { return await action(); }
        catch (Exception ex) when (ex is MobilePayException or HttpRequestException or TaskCanceledException
                                   or JsonException or DbUpdateConcurrencyException)
        {
            throw new ServiceException(ServiceError.ProviderFailed, message);
        }
    }
}
