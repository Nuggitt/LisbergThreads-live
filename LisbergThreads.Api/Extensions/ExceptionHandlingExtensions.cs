using LisbergThreads.Api.Services.Errors;
using Microsoft.AspNetCore.Diagnostics;

namespace LisbergThreads.Api.Extensions;

public static class ExceptionHandlingExtensions
{
    public static IServiceCollection AddApiExceptionHandling(this IServiceCollection services) =>
        services.AddExceptionHandler<ServiceExceptionHandler>();

    // ServiceException becomes its status code and Danish message; anything else is a generic 500.
    public static IApplicationBuilder UseApiExceptionHandling(this IApplicationBuilder app) =>
        app.UseExceptionHandler(new ExceptionHandlerOptions
        {
            // A NotFound ServiceException is a valid result, not a missing handler.
            AllowStatusCode404Response = true,
            ExceptionHandler = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(new { message = "Der opstod en serverfejl. Prøv igen senere." });
            }
        });
}

internal sealed class ServiceExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not ServiceException serviceException) return false;

        context.Response.StatusCode = serviceException.Error switch
        {
            ServiceError.Invalid => StatusCodes.Status400BadRequest,
            ServiceError.Unauthorized => StatusCodes.Status401Unauthorized,
            ServiceError.Forbidden => StatusCodes.Status403Forbidden,
            ServiceError.NotFound => StatusCodes.Status404NotFound,
            ServiceError.Conflict => StatusCodes.Status409Conflict,
            ServiceError.ProviderFailed => StatusCodes.Status502BadGateway,
            ServiceError.Unavailable => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError
        };
        if (serviceException.Detail is not null)
        {
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync(serviceException.Detail, cancellationToken);
        }
        return true;
    }
}
