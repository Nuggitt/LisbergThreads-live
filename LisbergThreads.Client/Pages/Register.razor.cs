using LisbergThreads.Contracts.Authentication;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System.Net;
using System.Net.Http.Json;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace LisbergThreads.Client.Pages;

public partial class Register : ComponentBase, IDisposable
{
    [Inject]
    public HttpClient Http { get; set; } = null!;

    [Inject]
    public NavigationManager Navigation { get; set; } = null!;

    protected RegisterFormModel Model { get; set; } = new();

    protected bool IsSaving { get; set; }

    protected string? ErrorMessage { get; set; }
    protected EditContext FormContext { get; private set; } = null!;
    private ValidationMessageStore _serverErrors = null!;

    protected override void OnInitialized()
    {
        FormContext = new EditContext(Model);
        _serverErrors = new ValidationMessageStore(FormContext);
        FormContext.OnFieldChanged += OnFieldChanged;
    }

    private void OnFieldChanged(object? sender, FieldChangedEventArgs e)
    {
        _serverErrors.Clear(e.FieldIdentifier);
        ErrorMessage = null;
        FormContext.NotifyValidationStateChanged();
    }

    protected void OnPasswordChanged(string value)
    {
        Model.Password = value;
        FormContext.NotifyFieldChanged(FormContext.Field(nameof(Model.Password)));
        if (!string.IsNullOrEmpty(Model.ConfirmPassword))
        {
            FormContext.NotifyFieldChanged(FormContext.Field(nameof(Model.ConfirmPassword)));
        }
    }

    protected void OnConfirmPasswordChanged(string value)
    {
        Model.ConfirmPassword = value;
        FormContext.NotifyFieldChanged(FormContext.Field(nameof(Model.ConfirmPassword)));
    }

    protected async Task RegisterAsync()
    {
        if (IsSaving)
        {
            return;
        }

        ErrorMessage = null;
        _serverErrors.Clear();
        FormContext.NotifyValidationStateChanged();
        if (!FormContext.Validate())
        {
            return;
        }

        IsSaving = true;

        try
        {
            var request = new RegisterRequest(
                Model.FirstName,
                Model.LastName,
                Model.Email,
                Model.PhoneNumber,
                Model.Password
            );

            using var response = await Http.PostAsJsonAsync(
                "api/Auth/register",
                request
            );

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    var errors = await response.Content.ReadFromJsonAsync<RegisterErrorResponse>();
                    ShowRegistrationErrors(errors);
                }
                else
                {
                    ErrorMessage = "Brugeren kunne ikke oprettes. Prøv igen senere.";
                }

                return;
            }

            Navigation.NavigateTo("/login?registered=true");
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Vi kunne ikke få forbindelse til serveren. Prøv igen senere.";
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "Oprettelsen tog for lang tid. Prøv igen senere.";
        }
        catch (JsonException)
        {
            ErrorMessage = "Brugeren kunne ikke oprettes. Prøv igen senere.";
        }
        catch (Microsoft.JSInterop.JSException)
        {
            ErrorMessage = "Browseren kunne ikke læse dine loginoplysninger. Kontrollér at browserlagring er tilladt, og prøv igen.";
        }
        finally
        {
            IsSaving = false;
        }
    }

    private void ShowRegistrationErrors(RegisterErrorResponse? errors)
    {
        if (errors?.Codes is not { Length: > 0 })
        {
            ErrorMessage = "Brugeren kunne ikke oprettes. Kontrollér oplysningerne og prøv igen.";
            return;
        }

        var messages = errors.Codes.Select(code => code switch
        {
            "DuplicateEmail" or "DuplicateUserName" =>
                (nameof(Model.Email), "Der findes allerede en bruger med denne e-mail."),
            "InvalidEmail" => (nameof(Model.Email), "Indtast en gyldig e-mailadresse."),
            "InvalidUserName" => (nameof(Model.Email), "E-mailadressen indeholder tegn, der ikke kan bruges."),
            "PasswordTooShort" => (nameof(Model.Password), "Adgangskoden skal være mindst 6 tegn lang."),
            "PasswordRequiresUpper" => (nameof(Model.Password), "Adgangskoden skal indeholde mindst ét stort bogstav."),
            "PasswordRequiresLower" => (nameof(Model.Password), "Adgangskoden skal indeholde mindst ét lille bogstav."),
            "PasswordRequiresDigit" => (nameof(Model.Password), "Adgangskoden skal indeholde mindst ét tal."),
            "PasswordRequiresNonAlphanumeric" => (nameof(Model.Password), "Adgangskoden skal indeholde mindst ét specialtegn."),
            "PasswordRequiresUniqueChars" => (nameof(Model.Password), "Adgangskoden skal indeholde flere forskellige tegn."),
            _ => (string.Empty, string.Empty)
        }).Distinct();

        foreach (var (field, message) in messages)
        {
            if (string.IsNullOrEmpty(field))
            {
                ErrorMessage = "Brugeren kunne ikke oprettes. Prøv igen senere.";
            }
            else
            {
                _serverErrors.Add(FormContext.Field(field), message);
            }
        }

        FormContext.NotifyValidationStateChanged();
    }

    protected void Cancel()
    {
        Navigation.NavigateTo("/");
    }

    protected sealed class RegisterFormModel
    {
        [Required(ErrorMessage = "Fornavn er påkrævet.")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Efternavn er påkrævet.")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "E-mail er påkrævet.")]
        [EmailAddress(ErrorMessage = "Indtast en gyldig e-mailadresse.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Telefonnummer er påkrævet.")]
        [RegularExpression(@"^[0-9]+$",
            ErrorMessage = "Telefonnummer må kun indeholde tal.")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Adgangskode er påkrævet.")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Du skal bekræfte adgangskoden.")]
        [Compare(
            nameof(Password),
            ErrorMessage = "Adgangskoderne er ikke ens."
        )]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public void Dispose()
    {
        FormContext.OnFieldChanged -= OnFieldChanged;
    }
}
