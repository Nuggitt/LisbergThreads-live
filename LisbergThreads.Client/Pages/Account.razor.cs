using LisbergThreads.Client.Services;
using LisbergThreads.Contracts.Authentication;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LisbergThreads.Client.Pages;

public partial class Account : IDisposable
{
    private readonly CancellationTokenSource _navigationCancellation = new();
    [Inject] public HttpClient Http { get; set; } = null!;
    [Inject] public NavigationManager Navigation { get; set; } = null!;
    [Inject] public TokenStorageService TokenStorage { get; set; } = null!;
    [Inject] public JwtAuthenticationStateProvider AuthenticationStateProvider { get; set; } = null!;

    protected ProfileFormModel Profile { get; } = new();
    protected PasswordFormModel Password { get; } = new();
    protected EditContext ProfileContext { get; private set; } = null!;
    protected EditContext PasswordContext { get; private set; } = null!;
    private ValidationMessageStore _profileErrors = null!;
    private ValidationMessageStore _passwordErrors = null!;
    protected bool IsLoading { get; set; }
    protected bool IsLoaded { get; set; }
    protected bool IsSavingProfile { get; set; }
    protected bool IsChangingPassword { get; set; }
    protected bool IsBusy => IsLoading || IsSavingProfile || IsChangingPassword;
    protected string? LoadError { get; set; }
    protected string? ProfileError { get; set; }
    protected string? ProfileSuccess { get; set; }
    protected string? PasswordError { get; set; }
    protected string? LogoutError { get; set; }

    protected override async Task OnInitializedAsync()
    {
        ProfileContext = new EditContext(Profile);
        PasswordContext = new EditContext(Password);
        _profileErrors = new ValidationMessageStore(ProfileContext);
        _passwordErrors = new ValidationMessageStore(PasswordContext);
        ProfileContext.OnFieldChanged += OnProfileFieldChanged;
        PasswordContext.OnFieldChanged += OnPasswordFieldChanged;
        await LoadAsync();
    }

    private void OnProfileFieldChanged(object? sender, FieldChangedEventArgs e)
    {
        _profileErrors.Clear(e.FieldIdentifier);
        ProfileError = null;
        ProfileSuccess = null;
        ProfileContext.NotifyValidationStateChanged();
    }

    private void OnPasswordFieldChanged(object? sender, FieldChangedEventArgs e)
    {
        _passwordErrors.Clear(e.FieldIdentifier);
        PasswordError = null;
        PasswordContext.NotifyValidationStateChanged();
    }

    protected async Task LoadAsync()
    {
        if (IsBusy) return;
        IsLoading = true;
        LoadError = null;
        var cancellationToken = _navigationCancellation.Token;
        try
        {
            using var response = await Http.GetAsync("api/Auth/me", cancellationToken);
            if (await HandleUnauthorizedAsync(response)) return;
            if (!response.IsSuccessStatusCode)
            {
                LoadError = "Dine oplysninger kunne ikke hentes. Prøv igen.";
                return;
            }
            var user = await response.Content.ReadFromJsonAsync<CurrentUserDto>(cancellationToken);
            if (user is null || user.Email is null || user.FirstName is null || user.LastName is null)
            {
                LoadError = "Dine oplysninger kunne ikke hentes. Prøv igen.";
                return;
            }
            cancellationToken.ThrowIfCancellationRequested();
            Profile.FirstName = user.FirstName;
            Profile.LastName = user.LastName;
            Profile.Email = user.Email;
            Profile.PhoneNumber = user.PhoneNumber ?? string.Empty;
            IsLoaded = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (HttpRequestException) { LoadError = AccountFormErrors.Connection; }
        catch (OperationCanceledException) { LoadError = AccountFormErrors.Timeout; }
        catch (JsonException) { LoadError = "Dine oplysninger kunne ikke hentes. Prøv igen."; }
        catch (Microsoft.JSInterop.JSException) { LoadError = "Browseren kunne ikke læse eller opdatere dit login. Kontrollér at browserlagring er tilladt, og prøv igen."; }
        finally { IsLoading = false; }
    }

    protected async Task SaveProfileAsync()
    {
        if (IsBusy) return;
        ProfileError = null;
        ProfileSuccess = null;
        _profileErrors.Clear();
        if (!ProfileContext.Validate()) return;
        IsSavingProfile = true;
        var cancellationToken = _navigationCancellation.Token;
        try
        {
            var request = new UpdateProfileRequest(Profile.FirstName, Profile.LastName, Profile.Email, Profile.PhoneNumber);
            using var response = await Http.PutAsJsonAsync("api/Auth/me", request, cancellationToken);
            if (await HandleUnauthorizedAsync(response)) return;
            if (!response.IsSuccessStatusCode)
            {
                ProfileError = await AccountFormErrors.ShowAsync(response, ProfileContext, _profileErrors, cancellationToken);
                return;
            }
            var result = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken);
            if (string.IsNullOrWhiteSpace(result?.Token))
            {
                ProfileError = "Vi kunne ikke bekræfte ændringen. Genindlæs siden og kontrollér dine oplysninger.";
                return;
            }
            cancellationToken.ThrowIfCancellationRequested();
            await TokenStorage.SetTokenAsync(result.Token);
            cancellationToken.ThrowIfCancellationRequested();
            await AuthenticationStateProvider.NotifyUserAuthenticationAsync();
            ProfileSuccess = "Dine oplysninger er gemt.";
            ProfileContext.MarkAsUnmodified();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (HttpRequestException) { ProfileError = AccountFormErrors.Connection; }
        catch (OperationCanceledException) { ProfileError = AccountFormErrors.Timeout; }
        catch (JsonException) { ProfileError = "Vi kunne ikke bekræfte ændringen. Genindlæs siden og kontrollér dine oplysninger."; }
        catch (Microsoft.JSInterop.JSException) { ProfileError = "Oplysningerne kan være gemt, men browseren kunne ikke opdatere dit login. Kontrollér browserlagring og log ind igen."; }
        finally { IsSavingProfile = false; }
    }

    protected async Task ChangePasswordAsync()
    {
        if (IsBusy) return;
        PasswordError = null;
        _passwordErrors.Clear();
        if (!PasswordContext.Validate()) return;
        IsChangingPassword = true;
        var cancellationToken = _navigationCancellation.Token;
        try
        {
            using var response = await Http.PostAsJsonAsync("api/Auth/change-password",
                new ChangePasswordRequest(Password.CurrentPassword, Password.NewPassword), cancellationToken);
            if (await HandleUnauthorizedAsync(response)) return;
            if (!response.IsSuccessStatusCode)
            {
                PasswordError = await AccountFormErrors.ShowAsync(response, PasswordContext, _passwordErrors, cancellationToken);
                return;
            }
            cancellationToken.ThrowIfCancellationRequested();
            await AuthenticationStateProvider.NotifyUserLogoutAsync();
            Navigation.NavigateTo("/login?passwordChanged=true");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (HttpRequestException) { PasswordError = AccountFormErrors.Connection; }
        catch (OperationCanceledException) { PasswordError = AccountFormErrors.Timeout; }
        catch (JsonException) { PasswordError = AccountFormErrors.Generic; }
        catch (Microsoft.JSInterop.JSException) { PasswordError = "Browseren kunne ikke opdatere dit login. Adgangskoden kan være ændret; kontrollér browserlagring og log ind med den nye adgangskode."; }
        finally
        {
            Password.CurrentPassword = string.Empty;
            Password.NewPassword = string.Empty;
            Password.ConfirmPassword = string.Empty;
            IsChangingPassword = false;
        }
    }

    private async Task<bool> HandleUnauthorizedAsync(HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.Unauthorized) return false;
        if (_navigationCancellation.IsCancellationRequested) return true;
        await AuthenticationStateProvider.NotifyUserLogoutAsync();
        Navigation.NavigateTo("/login?sessionExpired=true");
        return true;
    }

    protected async Task LogoutAsync()
    {
        LogoutError = null;
        try
        {
            await AuthenticationStateProvider.NotifyUserLogoutAsync();
            Navigation.NavigateTo("/");
        }
        catch (Microsoft.JSInterop.JSException)
        {
            LogoutError = "Du kunne ikke logges ud, fordi browseren ikke kunne fjerne dit login. Kontrollér browserlagring og prøv igen.";
        }
    }

    protected void OnCurrentPasswordChanged(string value)
    {
        Password.CurrentPassword = value;
        PasswordContext.NotifyFieldChanged(PasswordContext.Field(nameof(Password.CurrentPassword)));
    }

    protected void OnNewPasswordChanged(string value)
    {
        Password.NewPassword = value;
        PasswordContext.NotifyFieldChanged(PasswordContext.Field(nameof(Password.NewPassword)));
        if (Password.ConfirmPassword.Length > 0)
            PasswordContext.NotifyFieldChanged(PasswordContext.Field(nameof(Password.ConfirmPassword)));
    }

    protected void OnConfirmPasswordChanged(string value)
    {
        Password.ConfirmPassword = value;
        PasswordContext.NotifyFieldChanged(PasswordContext.Field(nameof(Password.ConfirmPassword)));
    }

    protected sealed class ProfileFormModel
    {
        [Required(ErrorMessage = "Fornavn er påkrævet.")]
        [StringLength(100, ErrorMessage = "Fornavn må højst indeholde 100 tegn.")]
        public string FirstName { get; set; } = string.Empty;
        [Required(ErrorMessage = "Efternavn er påkrævet.")]
        [StringLength(100, ErrorMessage = "Efternavn må højst indeholde 100 tegn.")]
        public string LastName { get; set; } = string.Empty;
        [Required(ErrorMessage = "E-mail er påkrævet.")]
        [EmailAddress(ErrorMessage = "Indtast en gyldig e-mailadresse.")]
        [StringLength(256, ErrorMessage = "E-mail må højst indeholde 256 tegn.")]
        public string Email { get; set; } = string.Empty;
        [Required(ErrorMessage = "Telefonnummer er påkrævet.")]
        [RegularExpression(@"^[0-9]+$", ErrorMessage = "Telefonnummer må kun indeholde tal.")]
        public string PhoneNumber { get; set; } = string.Empty;
    }

    protected sealed class PasswordFormModel
    {
        [Required(ErrorMessage = "Nuværende adgangskode er påkrævet.")]
        public string CurrentPassword { get; set; } = string.Empty;
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
        ProfileContext.OnFieldChanged -= OnProfileFieldChanged;
        PasswordContext.OnFieldChanged -= OnPasswordFieldChanged;
        Password.CurrentPassword = string.Empty;
        Password.NewPassword = string.Empty;
        Password.ConfirmPassword = string.Empty;
    }
}
