using LisbergThreads.Client.Services;
using LisbergThreads.Contracts.Authentication;
using Microsoft.AspNetCore.Components;
using System.Text.Json;

namespace LisbergThreads.Client.Pages;

public partial class Login : IDisposable
{
    private readonly CancellationTokenSource _navigationCancellation = new();

    [Inject]
    public NavigationManager NavigationManager { get; set; } = null!;

    [Inject]
    public AuthService AuthService { get; set; } = null!;

    [Inject]
    public TokenStorageService TokenStorageService { get; set; } = null!;

    [Inject]
    public JwtAuthenticationStateProvider AuthenticationStateProvider { get; set; }
        = null!;

    [SupplyParameterFromQuery(Name = "registered")]
    public bool Registered { get; set; }

    [SupplyParameterFromQuery(Name = "passwordChanged")]
    public bool PasswordChanged { get; set; }

    [SupplyParameterFromQuery(Name = "passwordReset")]
    public bool PasswordReset { get; set; }

    [SupplyParameterFromQuery(Name = "sessionExpired")]
    public bool SessionExpired { get; set; }

    protected string Email { get; set; } = string.Empty;
    protected string Password { get; set; } = string.Empty;

    protected string? SuccessMessage { get; set; }
    protected string? ErrorMessage { get; set; }
    protected bool IsLoggingIn { get; set; }

    protected override void OnParametersSet()
    {
        SuccessMessage = PasswordChanged
            ? "Din adgangskode er ændret. Log ind med den nye adgangskode."
            : PasswordReset
                ? "Din adgangskode er nulstillet. Log ind med den nye adgangskode."
                : Registered ? "Brugeren er oprettet. Du kan nu logge ind." : null;
        ErrorMessage = SessionExpired ? "Din session er udløbet. Log ind igen for at fortsætte." : null;
    }

    protected async Task LoginUser()
    {
        if (IsLoggingIn)
        {
            return;
        }

        IsLoggingIn = true;
        SuccessMessage = null;
        ErrorMessage = null;
        var cancellationToken = _navigationCancellation.Token;

        try
        {
            var request = new LoginRequest(
                Email,
                Password
            );

            var result = await AuthService.LoginAsync(request, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(result?.Token))
            {
                ErrorMessage = result is null
                    ? "Forkert e-mail eller adgangskode."
                    : "Login kunne ikke bekræftes. Prøv igen senere.";
                return;
            }

            await TokenStorageService.SetTokenAsync(result.Token);
            cancellationToken.ThrowIfCancellationRequested();

            await AuthenticationStateProvider.NotifyUserAuthenticationAsync();
            cancellationToken.ThrowIfCancellationRequested();
            NavigationManager.NavigateTo("/products?loggedIn=true");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Brugeren har forladt siden, så der skal ikke navigeres videre.
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = ex.StatusCode.HasValue
                ? "Login er ikke tilgængeligt lige nu. Prøv igen senere."
                : "Kunne ikke kontakte serveren. Prøv igen.";
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "Login tog for lang tid. Prøv igen.";
        }
        catch (JsonException)
        {
            ErrorMessage = "Login kunne ikke gennemføres. Prøv igen senere.";
        }
        catch (Microsoft.JSInterop.JSException)
        {
            ErrorMessage = "Browseren kunne ikke gemme dit login. Kontrollér at browserlagring er tilladt, og prøv igen.";
        }
        finally
        {
            Password = string.Empty;
            IsLoggingIn = false;
        }
    }

    public void Dispose()
    {
        _navigationCancellation.Cancel();
        Password = string.Empty;
        _navigationCancellation.Dispose();
    }
}
