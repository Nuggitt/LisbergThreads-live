using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LisbergThreads.Contracts.Orders;

public sealed record CheckoutRequestDto(
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string Address,
    string PostalCode,
    string City,
    string PaymentMethod,
    List<CheckoutItemDto> Items,
    Guid PaymentId = default,
    Guid PaymentToken = default
)
{
    public ShippingSelectionDto? Shipping { get; init; }
}
