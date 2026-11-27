namespace LisbergThreads.Contracts.Orders;

public static class OrderStatuses
{
    // An active cart. Deliberately not in All: it is not part of the order flow and admins cannot set it.
    public const string Pending = "Pending";

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