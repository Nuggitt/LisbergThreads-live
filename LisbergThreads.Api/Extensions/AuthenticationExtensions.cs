using System.Security.Claims;
using System.Text;
using LisbergThreads.Api.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace LisbergThreads.Api.Extensions;

public static class AuthenticationExtensions
{
    // Identity users and roles, JWT bearer authentication and the keys behind password-reset tokens.
    public static IServiceCollection AddIdentityAndJwt(this IServiceCollection services,
        IConfiguration configuration, IWebHostEnvironment environment)
    {
        var dataProtection = services.AddDataProtection().SetApplicationName("LisbergThreads");
        if (!environment.IsDevelopment())
        {
            var keysPath = Path.GetFullPath(configuration["DataProtection:KeysPath"] ?? "App_Data/keys",
                environment.ContentRootPath);
            Directory.CreateDirectory(keysPath);
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
            if (OperatingSystem.IsWindows()) dataProtection.ProtectKeysWithDpapi();
        }

        services.AddIdentityCore<ApplicationUser>(o => o.User.RequireUniqueEmail = true)
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager<SignInManager<ApplicationUser>>()
            .AddDefaultTokenProviders();
        services.Configure<DataProtectionTokenProviderOptions>(options =>
            options.TokenLifespan = TimeSpan.FromHours(1));

        var jwt = configuration.GetSection("Jwt");
        if (string.IsNullOrWhiteSpace(jwt["Key"]) || Encoding.UTF8.GetByteCount(jwt["Key"]!) < 32
            || string.IsNullOrWhiteSpace(jwt["Issuer"]) || string.IsNullOrWhiteSpace(jwt["Audience"]))
            throw new InvalidOperationException("JWT kræver issuer, audience og en privat tilfældig nøgle på mindst 32 bytes.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new()
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = jwt["Issuer"],
                    ValidAudience = jwt["Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!))
                };
                options.Events = new JwtBearerEvents
                {
                    // Deactivated users and tokens issued before a security stamp change are rejected.
                    OnTokenValidated = async context =>
                    {
                        var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                        var stamp = context.Principal?.FindFirstValue("security_stamp");
                        if (!Guid.TryParse(userId, out var id) || string.IsNullOrEmpty(stamp))
                        {
                            context.Fail("Session is no longer valid.");
                            return;
                        }

                        var userManager = context.HttpContext.RequestServices
                            .GetRequiredService<UserManager<ApplicationUser>>();
                        var user = await userManager.FindByIdAsync(id.ToString());
                        if (user is null || !user.IsActive
                            || !string.Equals(stamp, await userManager.GetSecurityStampAsync(user), StringComparison.Ordinal))
                        {
                            context.Fail("Session is no longer valid.");
                        }
                    }
                };
            });
        services.AddAuthorization();
        return services;
    }
}
