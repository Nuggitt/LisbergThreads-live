using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LisbergThreads.Contracts.Orders;

public sealed record OrderSummaryDto(
    Guid OrderId,
    DateTime CreatedAt,
    decimal Total,
    string Status,
    string PaymentStatus,
    int TotalItems
);
