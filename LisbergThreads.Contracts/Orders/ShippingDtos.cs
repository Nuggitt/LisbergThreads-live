namespace LisbergThreads.Contracts.Orders;

public sealed record ShippingMethodDto(string Id, string Name, string Carrier, bool RequiresServicePoint,
    decimal? Price, bool IsAvailable);

public sealed record ShippingSelectionDto(string MethodId, decimal ExpectedPrice, string? ServicePointId);

public sealed record ShippingServicePointDto(string Id, string Name, string Address, string? Address2,
    string PostalCode, string City);

public sealed record OrderShippingDto(string MethodId, string MethodName, string Carrier, decimal Price,
    ShippingServicePointDto? ServicePoint);

public sealed record UpdateOrderTrackingDto(string? TrackingNumber, string? ExpectedTrackingNumber);
