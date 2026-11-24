using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LisbergThreads.Contracts.Orders;

public sealed record AdminOrderSummaryDto(
    Guid OrderId,
    Guid? UserId,
    DateTime CreatedAt,
    string FirstName,
    string LastName,
    string Email,
    decimal Total,
    string Status,
    string PaymentStatus,
    int TotalItems
);
