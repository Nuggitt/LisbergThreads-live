namespace LisbergThreads.Contracts.Orders;

public static class OrderStatuses
{
    public const string Placed = "Placed";
    public const string Confirmed = "Confirmed";
    public const string Shipped = "Shipped";
    public const string Completed = "Completed";

    public static readonly string[] All =
    [
        Placed,
        Confirmed,
        Shipped,
        Completed
    ];
}