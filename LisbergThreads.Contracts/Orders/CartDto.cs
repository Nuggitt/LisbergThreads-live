namespace LisbergThreads.Contracts.Orders;

public sealed record CartDto(
    Guid OrderId,
    List<CartItemDto> Items,
    decimal Total
);