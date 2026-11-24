using Microsoft.AspNetCore.Components.Forms;
using System.Text.Json;

namespace LisbergThreads.Client.Services;

internal static class AccountFormErrors
{
    public const string Generic = "Oplysningerne kunne ikke gemmes. Kontrollér felterne og prøv igen senere.";
    public const string Connection = "Vi kunne ikke få forbindelse til serveren. Prøv igen senere.";
    public const string Timeout = "Forespørgslen tog for lang tid. Prøv igen.";

    public static async Task<string?> ShowAsync(HttpResponseMessage response,
        EditContext context, ValidationMessageStore errors, CancellationToken cancellationToken)
    {
        if (response.StatusCode != System.Net.HttpStatusCode.BadRequest)
        {
            return Generic;
        }

        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return Generic;
        }

        string? summary = null;
        var hasError = false;
        if (root.TryGetProperty("codes", out var codes) && codes.ValueKind == JsonValueKind.Array)
        {
            foreach (var code in codes.EnumerateArray())
            {
                var (field, message) = code.ValueKind == JsonValueKind.String
                    ? MapCode(code.GetString()) : (string.Empty, Generic);
                if (field.Length == 0)
                {
                    summary = message;
                }
                else
                {
                    errors.Add(context.Field(field), message);
                }
                hasError = true;
            }
        }
        else if (root.TryGetProperty("errors", out var fields) && fields.ValueKind == JsonValueKind.Object)
        {
            // Brug kun kendte felter og lokale beskeder, aldrig vilkårlig servertekst.
            foreach (var field in fields.EnumerateObject())
            {
                var separator = field.Name.LastIndexOf('.');
                var fieldName = field.Name[(separator + 1)..];
                var message = fieldName switch
                {
                    "FirstName" => "Indtast et fornavn på højst 100 tegn.",
                    "LastName" => "Indtast et efternavn på højst 100 tegn.",
                    "Email" => "Indtast en gyldig e-mailadresse på højst 256 tegn.",
                    "PhoneNumber" => "Indtast et telefonnummer, der kun indeholder tal.",
                    "CurrentPassword" => "Indtast din nuværende adgangskode.",
                    "NewPassword" => "Indtast en ny adgangskode på mindst 6 tegn.",
                    _ => null
                };
                if (message is null)
                {
                    summary = Generic;
                }
                else
                {
                    errors.Add(context.Field(fieldName), message);
                }
                hasError = true;
            }
        }
        context.NotifyValidationStateChanged();
        return hasError ? summary : Generic;
    }

    private static (string Field, string Message) MapCode(string? code) => code switch
    {
        "DuplicateEmail" or "DuplicateUserName" => ("Email", "Der findes allerede en bruger med denne e-mail."),
        "InvalidEmail" => ("Email", "Indtast en gyldig e-mailadresse."),
        "InvalidUserName" => ("Email", "E-mailadressen indeholder tegn, der ikke kan bruges."),
        "PasswordMismatch" => ("CurrentPassword", "Den nuværende adgangskode er forkert."),
        "PasswordTooShort" => ("NewPassword", "Adgangskoden skal være mindst 6 tegn lang."),
        "PasswordRequiresUpper" => ("NewPassword", "Adgangskoden skal indeholde mindst ét stort bogstav."),
        "PasswordRequiresLower" => ("NewPassword", "Adgangskoden skal indeholde mindst ét lille bogstav."),
        "PasswordRequiresDigit" => ("NewPassword", "Adgangskoden skal indeholde mindst ét tal."),
        "PasswordRequiresNonAlphanumeric" => ("NewPassword", "Adgangskoden skal indeholde mindst ét specialtegn."),
        "PasswordRequiresUniqueChars" => ("NewPassword", "Adgangskoden skal indeholde flere forskellige tegn."),
        "InvalidToken" => (string.Empty, "Linket er ugyldigt eller udløbet. Bed om et nyt link til at nulstille adgangskoden."),
        "ConcurrencyFailure" => (string.Empty, "Oplysningerne er blevet ændret imens. Genindlæs siden og prøv igen."),
        _ => (string.Empty, Generic)
    };
}
