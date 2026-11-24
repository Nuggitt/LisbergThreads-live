using LisbergThreads.Client.Services;
using LisbergThreads.Contracts.Authentication;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Text.Json;

namespace LisbergThreads.Client.Pages;

public partial class ResetPassword : IDisposable
{
    private readonly CancellationTokenSource _navigationCancellation = new();
    private static readonly EmailAddressAttribute EmailValidator = new();
    [Inject] public HttpClient Http { get; set; } = null!;
    [Inject] public NavigationManager Navigation { get; set; } = null!;
    [Inject] public JwtAuthenticationStateProvider AuthenticationStateProvider { get; set; } = null!;
    [SupplyParameterFromQuery(Name = "email")] public string? Email { get; set; }
    [SupplyParameterFromQuery(Name = "token")] public string? Token { get; set; }
    protected bool HasValidLink => !string.IsNullOrWhiteSpace(Email)
        && Email.Length <= 256 && EmailValidator.IsValid(Email)
        && !string.IsNullOrWhiteSpace(Token);
    protected ResetFormModel Model { get; } = new();
    protected EditContext FormContext { get; private set; } = null!;
    private ValidationMessageStore _serverErrors = null!;
    private string? _previousEmail;
    private string? _previousToken;
    protected bool IsSaving { get; set; }
    protected string? ErrorMessage { get; set; }

    protected override void OnInitialized()
    {
        FormContext = new EditContext(Model);
        _serverErrors = new ValidationMessageStore(FormContext);
        FormContext.OnFieldChanged += OnFieldChanged;
    }

    protected override void OnParametersSet()
    {
        if (_previousEmail == Email && _previousToken == Token) return;
        _previousEmail = Email;
        _previousToken = Token;
        ClearPasswords();
        _serverErrors.Clear();
        ErrorMessage = null;
        FormContext.NotifyValidationStateChanged();
    }

    private void OnFieldChanged(object? sender, FieldChangedEventArgs e)
    {
        _serverErrors.Clear(e.FieldIdentifier);
        ErrorMessage = null;
        FormContext.NotifyValidationStateChanged();
    }

    protected void OnPasswordChanged(string value)
    {
        Model.NewPassword = value;
        FormContext.NotifyFieldChanged(FormContext.Field(nameof(Model.NewPassword)));
        if (Model.ConfirmPassword.Length > 0)
            FormContext.NotifyFieldChanged(FormContext.Field(nameof(Model.ConfirmPassword)));
    }

    protected void OnConfirmPasswordChanged(string value)
    {
        Model.ConfirmPassword = value;
        FormContext.NotifyFieldChanged(FormContext.Field(nameof(Model.ConfirmPassword)));
    }

    protected async Task ResetAsync()
    {
        if (IsSaving || !HasValidLink) return;
        ErrorMessage = null;
        _serverErrors.Clear();
        if (!FormContext.Validate()) return;
        IsSaving = true;
        var cancellationToken = _navigationCancellation.Token;
        try
        {
            using var response = await Http.PostAsJsonAsync("api/Auth/reset-password",
                new ResetPasswordRequest(Email!, Token!, Model.NewPassword), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = await AccountFormErrors.ShowAsync(response, FormContext, _serverErrors, cancellationToken);
                return;
            }
            await AuthenticationStateProvider.NotifyUserLogoutAsync();
            cancellationToken.ThrowIfCancellationRequested();
            Navigation.NavigateTo("/login?passwordReset=true", replace: true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (HttpRequestException) { ErrorMessage = AccountFormErrors.Connection; }
        catch (OperationCanceledException) { ErrorMessage = AccountFormErrors.Timeout; }
        catch (JsonException) { ErrorMessage = "Adgangskoden kunne ikke nulstilles. Prøv igen eller bed om et nyt link."; }
        catch (Microsoft.JSInterop.JSException) { ErrorMessage = "Adgangskoden kan være nulstillet, men browseren kunne ikke fjerne dit tidligere login. Kontrollér browserlagring og log ind med den nye adgangskode."; }
        finally
        {
            ClearPasswords();
            IsSaving = false;
        }
    }

    private void ClearPasswords()
    {
        Model.NewPassword = string.Empty;
        Model.ConfirmPassword = string.Empty;
    }

    protected sealed class ResetFormModel
    {
        [Required(ErrorMessage = "Ny adgangskode er påkrævet.")]
        [MinLength(6, ErrorMessage = "Adgangskoden skal være mindst 6 tegn lang.")]
        public string NewPassword { get; set; } = string.Empty;
        [Required(ErrorMessage = "Du skal bekræfte den nye adgangskode.")]
        [Compare(nameof(NewPassword), ErrorMessage = "Adgangskoderne er ikke ens.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public void Dispose()
    {
        _navigationCancellation.Cancel();
        _navigationCancellation.Dispose();
        FormContext.OnFieldChanged -= OnFieldChanged;
        ClearPasswords();
    }
}
