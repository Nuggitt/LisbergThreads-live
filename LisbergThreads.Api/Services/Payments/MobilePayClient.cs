using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using LisbergThreads.Api.Models;

namespace LisbergThreads.Api.Services.Payments;

public sealed class MobilePayException : Exception
{
    public MobilePayException(HttpStatusCode? statusCode = null)
        : base("MobilePay request could not be confirmed.") => StatusCode = statusCode;
    public HttpStatusCode? StatusCode { get; }
}

public sealed record MobilePayAmount(string Currency, long Value);
public sealed record MobilePayAggregate(MobilePayAmount AuthorizedAmount, MobilePayAmount CapturedAmount,
    MobilePayAmount CancelledAmount, MobilePayAmount RefundedAmount);
public sealed record MobilePayDetails(string Reference, string State, MobilePayAmount Amount,
    MobilePayAggregate Aggregate, string? RedirectUrl);

public sealed class MobilePayClient(HttpClient http, Microsoft.Extensions.Options.IOptions<MobilePayOptions> options)
{
    private readonly MobilePayOptions settings = options.Value;
    private string? accessToken;
    private DateTime tokenExpiresAt;
    private readonly SemaphoreSlim tokenGate = new(1, 1);

    public bool IsConfigured => settings.IsConfigured;
    public static long MinorUnits(decimal total) => checked((long)(total * 100m));

    public async Task<string> CreateAsync(MobilePayPayment payment, CancellationToken cancellationToken)
    {
        var returnUrl = $"{settings.ClientBaseUrl.TrimEnd('/')}/payment/{payment.Id}?token={payment.AccessToken}";
        var result = await SendAsync<CreateResponse>(HttpMethod.Post, "epayment/v1/payments", new
        {
            amount = new MobilePayAmount("DKK", MinorUnits(payment.Total)),
            paymentMethod = new { type = "WALLET" },
            reference = payment.Id.ToString("N"),
            returnUrl,
            userFlow = "WEB_REDIRECT",
            paymentDescription = "Bestilling hos Lisberg Threads"
        }, $"create-{payment.Id:N}", cancellationToken);
        if (!Uri.TryCreate(result.RedirectUrl, UriKind.Absolute, out var redirect)
            || redirect.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(redirect.UserInfo))
            throw new MobilePayException();
        return redirect.AbsoluteUri;
    }

    public async Task<MobilePayDetails?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return await SendAsync<MobilePayDetails>(HttpMethod.Get, $"epayment/v1/payments/{id:N}",
                null, null, cancellationToken);
        }
        catch (MobilePayException ex) when (ex.StatusCode == HttpStatusCode.NotFound) { return null; }
    }

    public Task<MobilePayDetails> CaptureAsync(MobilePayPayment payment, CancellationToken cancellationToken) =>
        SendAsync<MobilePayDetails>(HttpMethod.Post, $"epayment/v1/payments/{payment.Id:N}/capture",
            new { modificationAmount = new MobilePayAmount("DKK", MinorUnits(payment.Total)) },
            $"capture-{payment.Id:N}", cancellationToken);

    public Task<MobilePayDetails> CancelAsync(MobilePayPayment payment, CancellationToken cancellationToken) =>
        SendAsync<MobilePayDetails>(HttpMethod.Post, $"epayment/v1/payments/{payment.Id:N}/cancel",
            new { }, $"cancel-{payment.Id:N}", cancellationToken);

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, string? key,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured) throw new MobilePayException();
        using var request = new HttpRequestMessage(method, new Uri(new Uri(settings.BaseUrl), path));
        AddHeaders(request);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetTokenAsync(cancellationToken));
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        if (body is not null) request.Content = JsonContent.Create(body);
        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized) accessToken = null;
            throw new MobilePayException(response.StatusCode);
        }
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
            ?? throw new MobilePayException();
    }

    private void AddHeaders(HttpRequestMessage request)
    {
        request.Headers.Add("Ocp-Apim-Subscription-Key", settings.SubscriptionKey);
        request.Headers.Add("Merchant-Serial-Number", settings.MerchantSerialNumber);
        request.Headers.Add("Vipps-System-Name", "LisbergThreads");
        request.Headers.Add("Vipps-System-Version", "1.0.0");
        request.Headers.Add("Vipps-System-Plugin-Name", "LisbergThreads");
        request.Headers.Add("Vipps-System-Plugin-Version", "1.0.0");
    }

    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        await tokenGate.WaitAsync(cancellationToken);
        try
        {
            if (accessToken is not null && tokenExpiresAt > DateTime.UtcNow) return accessToken;
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(settings.BaseUrl), "accesstoken/get"));
            AddHeaders(request);
            request.Headers.Add("client_id", settings.ClientId);
            request.Headers.Add("client_secret", settings.ClientSecret);
            request.Content = new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json");
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) throw new MobilePayException(response.StatusCode);
            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
                ?? throw new MobilePayException();
            if (string.IsNullOrWhiteSpace(token.AccessToken)) throw new MobilePayException();
            accessToken = token.AccessToken;
            tokenExpiresAt = DateTime.UtcNow.AddSeconds(Math.Max(0, token.ExpiresIn - 60));
            return accessToken;
        }
        finally { tokenGate.Release(); }
    }

    private sealed record CreateResponse(string RedirectUrl);
    private sealed record TokenResponse([property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in"), JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] int ExpiresIn);
}
