namespace LisbergThreads.Api.Services.Errors;

// Outcome of a rejected business rule. ServiceExceptionHandler maps it to an HTTP status code.
public enum ServiceError
{
    Invalid,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
    ProviderFailed,
    Unavailable
}

public sealed class ServiceException(ServiceError error, string? detail = null)
    : Exception(detail ?? error.ToString())
{
    public ServiceError Error { get; } = error;

    // Danish text returned to the client; null gives an empty response body.
    public string? Detail { get; } = detail;
}
