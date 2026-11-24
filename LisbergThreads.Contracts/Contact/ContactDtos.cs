using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;

namespace LisbergThreads.Contracts.Contact;

public static class ContactTopics
{
    public static IReadOnlyDictionary<string, string> All { get; } = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
    {
        ["product"] = "Spørgsmål om et produkt", ["size"] = "Størrelse, mål og pasform",
        ["care"] = "Materialer, vask og pleje", ["order"] = "Bestilling og ordrestatus",
        ["delivery"] = "Levering og forsendelse", ["change"] = "Ændring af en bestilling",
        ["return"] = "Returnering eller reklamation", ["custom"] = "Særlige ønsker og forespørgsler", ["other"] = "Andet"
    });
    public static bool UsesOrder(string? topic) => topic is "order" or "delivery" or "change" or "return";
    public static bool UsesProduct(string? topic) => topic is "product" or "size" or "care" or "custom";
}

public sealed class ContactRequestDto : IValidatableObject
{
    [Required(ErrorMessage = "Navn er påkrævet."), StringLength(100, ErrorMessage = "Navnet må højst være 100 tegn.")]
    public string Name { get; set; } = string.Empty;
    [Required(ErrorMessage = "E-mail er påkrævet."), EmailAddress(ErrorMessage = "Indtast en gyldig e-mailadresse."), StringLength(254, ErrorMessage = "E-mail må højst være 254 tegn.")]
    public string Email { get; set; } = string.Empty;
    [Required(ErrorMessage = "Vælg et emne."), StringLength(30)]
    public string Topic { get; set; } = string.Empty;
    [StringLength(120, ErrorMessage = "Ordrenummer må højst være 120 tegn.")]
    public string? OrderNumber { get; set; }
    [StringLength(500, ErrorMessage = "Produktnavn eller link må højst være 500 tegn.")]
    public string? Product { get; set; }
    [Required(ErrorMessage = "Skriv en besked."), StringLength(5000, ErrorMessage = "Beskeden må højst være 5000 tegn.")]
    public string Message { get; set; } = string.Empty;
    [StringLength(200)]
    public string? Website { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!ContactTopics.All.ContainsKey(Topic ?? string.Empty))
            yield return new ValidationResult("Vælg et gyldigt emne.", new[] { nameof(Topic) });
    }
}

public sealed record ContactInfoDto(bool IsAvailable, string? ResponseTime);
