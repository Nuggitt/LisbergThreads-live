using System.ComponentModel.DataAnnotations;

namespace LisbergThreads.Contracts.Authentication;

public record UpdateProfileRequest(
    [Required(ErrorMessage = "Fornavn er påkrævet.")]
    [StringLength(100, ErrorMessage = "Fornavn må højst indeholde 100 tegn.")]
    string FirstName,
    [Required(ErrorMessage = "Efternavn er påkrævet.")]
    [StringLength(100, ErrorMessage = "Efternavn må højst indeholde 100 tegn.")]
    string LastName,
    [Required(ErrorMessage = "E-mail er påkrævet.")]
    [EmailAddress(ErrorMessage = "Indtast en gyldig e-mailadresse.")]
    [StringLength(256, ErrorMessage = "E-mail må højst indeholde 256 tegn.")]
    string Email,
    [Required(ErrorMessage = "Telefonnummer er påkrævet.")]
    [RegularExpression(@"^[0-9]+$", ErrorMessage = "Telefonnummer må kun indeholde tal.")]
    string PhoneNumber
);
