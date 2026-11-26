using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LisbergThreads.Contracts.Orders;

public sealed record OrderDetailsDto(
    Guid OrderId,
    DateTime CreatedAt,
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string Address,
    string PostalCode,
    string City,
    string PaymentMethod,
    string PaymentStatus,
    string Status,
    decimal Total,
    List<OrderDetailsItemDto> Items
)
{
    public OrderShippingDto? Shipping { get; init; }
    public string? TrackingNumber { get; init; }
}
