using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LisbergThreads.Contracts.Orders;

public sealed record CheckoutResponseDto(
    Guid OrderId,
    Guid ConfirmationToken,
    decimal Total
);
