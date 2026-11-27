using LisbergThreads.Api.Data;
using LisbergThreads.Api.Extensions;
using LisbergThreads.Api.Services.Uploads;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
// Private files are optional and never copied by the publishing process.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
if (builder.Environment.IsDevelopment()) builder.Configuration.AddUserSecrets(typeof(AppDbContext).Assembly, optional: true);
builder.Configuration.AddEnvironmentVariables().AddCommandLine(args);

builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddIdentityAndJwt(builder.Configuration, builder.Environment);
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddRateLimitPolicies();
builder.Services.AddApiExceptionHandling();

var mobileTestingEnabled = builder.Environment.IsDevelopment()
    && builder.Configuration.GetValue<bool>("MobileTesting:Enabled");
var mobileClientOrigin = mobileTestingEnabled
    ? builder.Configuration["MobileTesting:ClientOrigin"]
    : null;

builder.Services.AddCors(opt =>
{
    opt.AddPolicy("DevCors", policy =>
    {
        policy.WithOrigins(
            "https://localhost:7022",   // API
            "https://localhost:7185",   // Blazor server
            "http://localhost:5102",    // HTTP fallback
            "https://localhost:7148"    // WASM
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

builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerWithJwt();

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
app.UseApiExceptionHandling();
app.Use(async (context, next) =>
{
    // Set when the response starts, so error responses from the exception handler keep them too.
    context.Response.OnStarting(() =>
    {
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        if (context.Request.Path.StartsWithSegments("/api"))
            context.Response.Headers.CacheControl = "no-store";
        return Task.CompletedTask;
    });
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
