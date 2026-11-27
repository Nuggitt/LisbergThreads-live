namespace LisbergThreads.Api.Services.Payments;

public sealed class MobilePayOptions
{
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "https://apitest.vipps.no/";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string SubscriptionKey { get; set; } = string.Empty;
    public string MerchantSerialNumber { get; set; } = string.Empty;
    public string ClientBaseUrl { get; set; } = string.Empty;

    public bool IsConfigured => Enabled
        && BaseUrl is "https://apitest.vipps.no/" or "https://api.vipps.no/"
        && !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret)
        && !string.IsNullOrWhiteSpace(SubscriptionKey)
        && !string.IsNullOrWhiteSpace(MerchantSerialNumber)
        && Uri.TryCreate(ClientBaseUrl, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo)
        && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);
}
