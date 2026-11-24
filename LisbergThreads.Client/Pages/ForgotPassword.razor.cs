using LisbergThreads.Client.Services;
using LisbergThreads.Contracts.Authentication;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LisbergThreads.Client.Pages;

public partial class ForgotPassword : IDisposable
{
    private readonly CancellationTokenSource _navigationCancellation = new();
    [Inject] public HttpClient Http { get; set; } = null!;
    protected EmailFormModel Model { get; } = new();
    protected EditContext FormContext { get; private set; } = null!;
    private ValidationMessageStore _serverErrors = null!;
    protected bool IsSending { get; set; }
    protected string? ErrorMessage { get; set; }
    protected string? SuccessMessage { get; set; }

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
        SuccessMessage = null;
        FormContext.NotifyValidationStateChanged();
    }

    protected async Task SendAsync()
    {
        if (IsSending) return;
        ErrorMessage = null;
        SuccessMessage = null;
        _serverErrors.Clear();
        if (!FormContext.Validate()) return;
        IsSending = true;
        var cancellationToken = _navigationCancellation.Token;
        try
        {
            using var response = await Http.PostAsJsonAsync("api/Auth/forgot-password",
                new ForgotPasswordRequest(Model.Email), cancellationToken);
            if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                ErrorMessage = "Nulstilling af adgangskode er ikke tilgængelig lige nu. Prøv igen senere.";
                return;
            }
            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = await AccountFormErrors.ShowAsync(response, FormContext, _serverErrors, cancellationToken);
                return;
            }
            cancellationToken.ThrowIfCancellationRequested();
            SuccessMessage = "Hvis der findes en aktiv konto med denne e-mailadresse, modtager du et link til at nulstille adgangskoden. Kontrollér også din spammappe.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (HttpRequestException) { ErrorMessage = AccountFormErrors.Connection; }
        catch (OperationCanceledException) { ErrorMessage = AccountFormErrors.Timeout; }
        catch (JsonException) { ErrorMessage = "Anmodningen kunne ikke behandles. Prøv igen senere."; }
        catch (Microsoft.JSInterop.JSException) { ErrorMessage = "Browseren kunne ikke læse dine loginoplysninger. Kontrollér browserlagring og prøv igen."; }
        finally { IsSending = false; }
    }

    protected sealed class EmailFormModel
    {
        [Required(ErrorMessage = "E-mail er påkrævet.")]
        [EmailAddress(ErrorMessage = "Indtast en gyldig e-mailadresse.")]
        [StringLength(256, ErrorMessage = "E-mail må højst indeholde 256 tegn.")]
        public string Email { get; set; } = string.Empty;
    }

    public void Dispose()
    {
        _navigationCancellation.Cancel();
        _navigationCancellation.Dispose();
        FormContext.OnFieldChanged -= OnFieldChanged;
    }
}
