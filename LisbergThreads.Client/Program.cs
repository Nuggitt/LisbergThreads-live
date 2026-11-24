using LisbergThreads.Client;
using LisbergThreads.Client.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Authorization;
using System.Globalization;

var culture = new CultureInfo("da-DK");
culture.NumberFormat.CurrencySymbol = "kr.";
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped<JwtAuthorizationMessageHandler>();

// The local mobile profile uses separate HTTP ports on the same host as the client.
var clientBaseUri = new Uri(builder.HostEnvironment.BaseAddress);
var apiBaseUri = builder.HostEnvironment.IsDevelopment()
    ? clientBaseUri.Port == 5149
        ? new UriBuilder(clientBaseUri) { Port = 5182, Path = "/", Query = "", Fragment = "" }.Uri
        : new Uri("https://localhost:7013/")
    // Uden for udvikling ligger API'et bag samme adresse som klienten (reverse proxy).
    : clientBaseUri;


builder.Services.AddHttpClient("Api", client =>
{
    client.BaseAddress = apiBaseUri;
})
.AddHttpMessageHandler<JwtAuthorizationMessageHandler>();

builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>()
        .CreateClient("Api"));

builder.Services.AddScoped<AuthService>();
builder.Services.AddSingleton<TokenStorageService>();
builder.Services.AddScoped<CartStateService>();
builder.Services.AddScoped<GuestCartService>();
builder.Services.AddScoped<MobilePayCheckoutService>();
builder.Services.AddScoped<GlobalSearchService>();

builder.Services.AddAuthorizationCore();

builder.Services.AddSingleton<JwtAuthenticationStateProvider>();

builder.Services.AddSingleton<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<JwtAuthenticationStateProvider>());


await builder.Build().RunAsync();
