using LisbergThreads.Api.Data;
using LisbergThreads.Api.Services.Auth;
using LisbergThreads.Api.Services.Cart;
using LisbergThreads.Api.Services.Checkout;
using LisbergThreads.Api.Services.Email;
using LisbergThreads.Api.Services.Orders;
using LisbergThreads.Api.Services.Payments;
using LisbergThreads.Api.Services.Products;
using LisbergThreads.Api.Services.Shipping;
using LisbergThreads.Api.Services.Uploads;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

namespace LisbergThreads.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("RemoteConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Connection string 'RemoteConnection' mangler.");
        return services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<ProductService>();
        services.AddScoped<CartService>();
        services.AddScoped<CheckoutService>();
        services.AddScoped<OrderService>();
        services.AddScoped<JwtTokenService>();

        services.AddSingleton<ProductUploadPath>();
        services.AddSingleton<ImageStorageService>();
        services.AddSingleton<PasswordResetEmailSender>();
        services.AddSingleton<ContactEmailSender>();

        services.Configure<ShippingOptions>(configuration.GetSection("Shipping"));
        services.AddHttpClient("Shipmondo", client =>
        {
            client.BaseAddress = new Uri("https://app.shipmondo.com/api/public/v3/");
            client.Timeout = TimeSpan.FromSeconds(15);
        }).RemoveAllLoggers();
        services.AddSingleton<ShippingService>();

        services.Configure<MobilePayOptions>(configuration.GetSection("MobilePay"));
        var mobilePayOptions = configuration.GetSection("MobilePay").Get<MobilePayOptions>() ?? new();
        if (mobilePayOptions.Enabled && !mobilePayOptions.IsConfigured)
            throw new InvalidOperationException("MobilePay er aktiveret, men serveropsætningen er ufuldstændig.");
        services.AddHttpClient("MobilePay", client => client.Timeout = TimeSpan.FromSeconds(30));
        services.AddSingleton(sp => new MobilePayClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("MobilePay"),
            sp.GetRequiredService<IOptions<MobilePayOptions>>()));
        services.AddScoped<MobilePayPaymentService>();
        services.AddHostedService<MobilePayReconciliationService>();
        return services;
    }

    public static IServiceCollection AddSwaggerWithJwt(this IServiceCollection services) =>
        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Indsæt dit JWT token"
            });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                    },
                    Array.Empty<string>()
                }
            });
        });
}
