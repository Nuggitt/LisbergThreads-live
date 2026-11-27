using System.Linq.Expressions;
using LisbergThreads.Api.Models;
using LisbergThreads.Contracts.Orders;

namespace LisbergThreads.Api.Mapping;

public static class OrderMappings
{
    // Used in database queries; EF Core translates them to SQL.
    public static readonly Expression<Func<Order, OrderSummaryDto>> SummaryProjection = o => new OrderSummaryDto(
        o.Id,
        o.CreatedAt,
        o.Total,
        o.Status,
        o.PaymentStatus,
        o.OrderItems.Sum(oi => oi.Quantity));

    public static readonly Expression<Func<Order, AdminOrderSummaryDto>> AdminSummaryProjection = o =>
        new AdminOrderSummaryDto(
            o.Id,
            o.UserId,
            o.CreatedAt,
            o.FirstName,
            o.LastName,
            o.Email,
            o.Total,
            o.Status,
            o.PaymentStatus,
            o.OrderItems.Sum(oi => oi.Quantity));

    // Lines whose product no longer exists are left out.
    public static OrderDetailsDto ToDetailsDto(this Order order, IReadOnlyDictionary<Guid, Product> products)
    {
        var items = order.OrderItems
            .Where(item => products.ContainsKey(item.ProductId))
            .Select(item =>
            {
                var product = products[item.ProductId];
                return new OrderDetailsItemDto(item.ProductId, product.Name, product.Size, product.ImageUrl,
                    item.Quantity, item.UnitPrice, item.LineTotal);
            })
            .ToList();

        return new OrderDetailsDto(
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
            items)
        {
            Shipping = order.ShippingMethodId is null ? null : new OrderShippingDto(
                order.ShippingMethodId, order.ShippingMethodName ?? "Levering", order.ShippingCarrier ?? "",
                order.ShippingPrice, order.ServicePointId is null ? null : new ShippingServicePointDto(
                    order.ServicePointId, order.ServicePointName ?? "", order.ServicePointAddress ?? "",
                    order.ServicePointAddress2, order.ServicePointPostalCode ?? "", order.ServicePointCity ?? "")),
            TrackingNumber = order.TrackingNumber
        };
    }
}
