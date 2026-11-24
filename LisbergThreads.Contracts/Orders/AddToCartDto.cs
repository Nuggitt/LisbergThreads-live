namespace LisbergThreads.Contracts.Orders;

public sealed record AddToCartDto(
    Guid ProductId,
    int Quantity);
