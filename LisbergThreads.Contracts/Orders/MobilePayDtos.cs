namespace LisbergThreads.Contracts.Orders;

public sealed record CheckoutPaymentOptionsDto(bool IsTestMode, bool IsAvailable);

public sealed record MobilePayStartDto(Guid PaymentId, Guid PaymentToken, string? RedirectUrl, bool IsActive);
public sealed record MobilePayStatusDto(string State, bool IsActive, Guid? OrderId,
    Guid? ConfirmationToken, decimal Total, string? RedirectUrl);
