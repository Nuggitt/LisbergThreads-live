namespace LisbergThreads.Api.Services;

public sealed class ShippingOptions
{
    public bool Enabled { get; set; }
    public string DeliveryCheckoutKey { get; set; } = string.Empty;
    public Dictionary<string, ShippingMethodOptions> Methods { get; set; } = new();
}

public sealed class ShippingMethodOptions
{
    public decimal? Price { get; set; }
    public string ProductCode { get; set; } = string.Empty;
}
