using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace LisbergThreads.Api.Extensions;

public static class RateLimitingExtensions
{
    // Fixed-window limits per client IP; endpoints opt in with [EnableRateLimiting("name")].
    public static IServiceCollection AddRateLimitPolicies(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPerIpPolicy("shipping", 20, TimeSpan.FromMinutes(1));
            options.AddPerIpPolicy("checkout", 6, TimeSpan.FromMinutes(10));
            options.AddPerIpPolicy("payment-status", 60, TimeSpan.FromMinutes(1));
            options.AddPerIpPolicy("authentication", 10, TimeSpan.FromMinutes(1));
            options.AddPerIpPolicy("contact", 3, TimeSpan.FromMinutes(10));
        });

    private static void AddPerIpPolicy(this RateLimiterOptions options, string name, int permitLimit,
        TimeSpan window) =>
        options.AddPolicy(name, context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                QueueLimit = 0,
                AutoReplenishment = true
            }));
}
