namespace LisbergThreads.Contracts.Orders;

// The state of a payment attempt, returned to the client by the payment status endpoint.
public static class PaymentStates
{
    public const string Creating = "Creating";
    public const string Waiting = "Waiting";
    public const string TestPending = "TestPending";
    public const string Completed = "Completed";
    public const string CancelPending = "CancelPending";
    public const string StockRejected = "StockRejected";
    public const string Cancelled = "Cancelled";
    public const string Aborted = "ABORTED";
    public const string Expired = "EXPIRED";
}
