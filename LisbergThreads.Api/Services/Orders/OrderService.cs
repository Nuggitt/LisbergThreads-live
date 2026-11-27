using LisbergThreads.Api.Data;
using LisbergThreads.Api.Mapping;
using LisbergThreads.Api.Services.Errors;
using LisbergThreads.Api.Services.Payments;
using LisbergThreads.Contracts.Orders;
using Microsoft.EntityFrameworkCore;

namespace LisbergThreads.Api.Services.Orders;

public sealed class OrderService(AppDbContext db, MobilePayPaymentService payments)
{
    // Access: the customer who owns the order, an admin, or a guest with the order's confirmation token.
    public async Task<OrderDetailsDto> GetDetailsAsync(Guid orderId, Guid? userId, bool isAuthenticated,
        bool isAdmin, Guid? token)
    {
        var order = await db.Orders.Include(o => o.OrderItems).FirstOrDefaultAsync(o => o.Id == orderId);
        if (order is null || order.Status == OrderStatuses.Pending)
            throw new ServiceException(ServiceError.NotFound, "Ordren blev ikke fundet.");

        var isOwner = userId.HasValue && order.UserId == userId;
        var hasValidToken = token.HasValue && order.ConfirmationToken == token.Value;
        if (!isOwner && !isAdmin && !hasValidToken)
            throw new ServiceException(isAuthenticated ? ServiceError.Forbidden : ServiceError.Unauthorized);

        var productIds = order.OrderItems.Select(oi => oi.ProductId).ToList();
        var products = await db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        return order.ToDetailsDto(products);
    }

    public Task<List<AdminOrderSummaryDto>> GetAllAsync(Guid? customerId)
    {
        var query = db.Orders.AsNoTracking().Where(o => o.Status != OrderStatuses.Pending);
        if (customerId.HasValue) query = query.Where(o => o.UserId == customerId.Value);
        return query.OrderByDescending(o => o.CreatedAt).Select(OrderMappings.AdminSummaryProjection).ToListAsync();
    }

    public Task<List<OrderSummaryDto>> GetForCustomerAsync(Guid userId) => db.Orders
        .AsNoTracking()
        .Where(o => o.UserId == userId && o.Status != OrderStatuses.Pending)
        .OrderByDescending(o => o.CreatedAt)
        .Select(OrderMappings.SummaryProjection)
        .ToListAsync();

    public async Task UpdateStatusAsync(Guid orderId, string? status, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(status))
            throw new ServiceException(ServiceError.Invalid, "Ordrestatus skal angives.");
        if (!OrderStatuses.All.Contains(status))
            throw new ServiceException(ServiceError.Invalid, "Ugyldig ordrestatus.");

        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken)
            ?? throw new ServiceException(ServiceError.NotFound, "Ordren blev ikke fundet.");
        if (order.Status == OrderStatuses.Pending)
            throw new ServiceException(ServiceError.Invalid, "En aktiv kurv kan ikke få ændret ordrestatus.");

        // PUT must be repeatable without an error.
        if (order.Status == status) return;
        if (!order.CanChangeStatusTo(status))
            throw new ServiceException(ServiceError.Invalid,
                $"Ordrestatus kan ikke ændres fra {order.Status} til {status}.");

        // The reserved amount is captured when the order is shipped.
        if (status == OrderStatuses.Shipped && order.PaymentMethod == PaymentMethods.MobilePay)
            await payments.CaptureOrderAsync(order, cancellationToken);

        order.ChangeStatus(status);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            throw new ServiceException(ServiceError.Conflict,
                "Ordren er ændret af en anden forespørgsel. Hent den igen før næste statusskift.");
        }
    }

    public async Task UpdateTrackingAsync(Guid orderId, UpdateOrderTrackingDto request,
        CancellationToken cancellationToken)
    {
        var number = string.IsNullOrWhiteSpace(request.TrackingNumber) ? null : request.TrackingNumber.Trim();
        if (number?.Length > 100 || number?.Any(char.IsControl) == true)
            throw new ServiceException(ServiceError.Invalid,
                "Trackingnummer må højst være 100 tegn og må ikke indeholde kontroltegn.");
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null || order.Status == OrderStatuses.Pending)
            throw new ServiceException(ServiceError.NotFound, "Ordren blev ikke fundet.");
        if (order.TrackingNumber != request.ExpectedTrackingNumber)
            throw new ServiceException(ServiceError.Conflict, "Trackingnummeret er ændret. Hent ordren igen.");

        order.TrackingNumber = number;
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            throw new ServiceException(ServiceError.Conflict, "Ordren er ændret. Hent ordren igen.");
        }
    }
}
