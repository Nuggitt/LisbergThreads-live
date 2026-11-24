namespace LisbergThreads.Contracts.Orders;

public sealed record CartItemDto(
    Guid ProductId,
    string Name,
    string? Size,
    string? ImageUrl,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal
);