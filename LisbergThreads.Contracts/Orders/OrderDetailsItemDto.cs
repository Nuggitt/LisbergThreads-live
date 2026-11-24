using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LisbergThreads.Contracts.Orders;

public sealed record OrderDetailsItemDto(
    Guid ProductId,
    string Name,
    string? Size,
    string? ImageUrl,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal
);
