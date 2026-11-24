using LisbergThreads.Api.Data;
using LisbergThreads.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.StaticFiles;

var builder = WebApplication.CreateBuilder(args);
// Private files are optional and never copied by the publishing process.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
if (builder.Environment.IsDevelopment()) builder.Configuration.AddUserSecrets(typeof(AppDbContext).Assembly, optional: true);
builder.Configuration.AddEnvironmentVariables().AddCommandLine(args);

builder.Services.AddSingleton<ProductUploadPath>();
builder.Services.Configure<MobilePayOptions>(builder.Configuration.GetSection("MobilePay"));
var mobilePayOptions = builder.Configuration.GetSection("MobilePay").Get<MobilePayOptions>() ?? new();
if (mobilePayOptions.Enabled && !mobilePayOptions.IsConfigured)
    throw new InvalidOperationException("MobilePay er aktiveret, men serveropsætningen er ufuldstændig.");
builder.Services.AddHttpClient("MobilePay", client => client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddSingleton(sp => new MobilePayClient(
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("MobilePay"),
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MobilePayOptions>>()));
builder.Services.AddScoped<MobilePayPaymentService>();
builder.Services.AddHostedService<MobilePayReconciliationService>();

var dataProtection = builder.Services.AddDataProtection().SetApplicationName("LisbergThreads");
if (!builder.Environment.IsDevelopment())
{
    var keysPath = Path.GetFullPath(builder.Configuration["DataProtection:KeysPath"] ?? "App_Data/keys",
        builder.Environment.ContentRootPath);
    Directory.CreateDirectory(keysPath);
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
    if (OperatingSystem.IsWindows()) dataProtection.ProtectKeysWithDpapi();
}

// EF Core + SQL Server
var connectionString =
    builder.Configuration.GetConnectionString("RemoteConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'RemoteConnection' mangler."
    );
}

builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlServer(connectionString));

// Identity
builder.Services.AddIdentityCore<ApplicationUser>(o =>
{
    o.User.RequireUniqueEmail = true;
})
.AddRoles<ApplicationRole>()
.AddEntityFrameworkStores<AppDbContext>()
.AddSignInManager<SignInManager<ApplicationUser>>()
.AddDefaultTokenProviders();

builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
    options.TokenLifespan = TimeSpan.FromHours(1));
builder.Services.AddSingleton<PasswordResetEmailSender>();
builder.Services.AddSingleton<ContactEmailSender>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("checkout", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 6, Window = TimeSpan.FromMinutes(10), QueueLimit = 0,
            AutoReplenishment = true
        }));
    options.AddPolicy("payment-status", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0,
            AutoReplenishment = true
        }));
    options.AddPolicy("authentication", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0,
            AutoReplenishment = true
        }));
    options.AddPolicy("contact", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 3,
            Window = TimeSpan.FromMinutes(10),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});

// JWT
var jwt = builder.Configuration.GetSection("Jwt");
if (string.IsNullOrWhiteSpace(jwt["Key"]) || Encoding.UTF8.GetByteCount(jwt["Key"]!) < 32
    || string.IsNullOrWhiteSpace(jwt["Issuer"]) || string.IsNullOrWhiteSpace(jwt["Audience"]))
    throw new InvalidOperationException("JWT kræver issuer, audience og en privat tilfældig nøgle på mindst 32 bytes.");
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
            IssuerSigningKey = key
        };
        options.Events = new JwtBearerEvents
        {
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

builder.Services.AddAuthorization();


var mobileTestingEnabled = builder.Environment.IsDevelopment()
    && builder.Configuration.GetValue<bool>("MobileTesting:Enabled");
var mobileClientOrigin = mobileTestingEnabled
    ? builder.Configuration["MobileTesting:ClientOrigin"]
    : null;

//CORS
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("DevCors", policy =>
    {
        policy.WithOrigins(
            "https://localhost:7022",   // ? API
            "https://localhost:7185",   // ? Blazor server (tilpas når du kender Web-porten)
            "http://localhost:5102",     // ? fallback til http
            "https://localhost:7148" //WASM
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();

        if (!string.IsNullOrWhiteSpace(mobileClientOrigin))
        {
            policy.WithOrigins(mobileClientOrigin);
        }
    });
});

// Controllers
builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Indsæt dit JWT token"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!mobileTestingEnabled)
{
    app.UseHttpsRedirection();
}
if (app.Environment.IsDevelopment()) app.UseCors("DevCors");
else app.UseHsts();
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new { message = "Der opstod en serverfejl. Prøv igen senere." });
}));
app.Use(async (context, next) =>
{
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    if (context.Request.Path.StartsWithSegments("/api"))
        context.Response.Headers.CacheControl = "no-store";
    await next(context);
});
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseDefaultFiles();
var contentTypes = new FileExtensionContentTypeProvider();
contentTypes.Mappings[".dat"] = "application/octet-stream";
contentTypes.Mappings[".blat"] = "application/octet-stream";
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = contentTypes });
var productsUploadPath = app.Services.GetRequiredService<ProductUploadPath>().Value;
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(productsUploadPath),
    RequestPath = "/uploads/products"
});

app.MapControllers();
app.MapHealthChecks("/health");
// Unknown API routes must never return the Blazor HTML page.
app.Map("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

using (var scope = app.Services.CreateScope())
{
    await SeedData.SeedRolesAsync(scope.ServiceProvider);

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    foreach (var adminId in builder.Configuration.GetSection("Bootstrap:AdminUserIds").Get<Guid[]>() ?? [])
    {
        var user = await userManager.FindByIdAsync(adminId.ToString());
        if (user?.Email is not null)
            await SeedData.SeedAdminAsync(scope.ServiceProvider, user.Email);
    }
}

app.Run();
