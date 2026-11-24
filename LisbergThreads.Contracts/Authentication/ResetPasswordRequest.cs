using System.ComponentModel.DataAnnotations;

namespace LisbergThreads.Contracts.Authentication;

public record ResetPasswordRequest(
    [Required(ErrorMessage = "E-mail er påkrævet.")]
    [EmailAddress(ErrorMessage = "Indtast en gyldig e-mailadresse.")]
    [StringLength(256, ErrorMessage = "E-mail må højst indeholde 256 tegn.")]
    string Email,
    [Required(ErrorMessage = "Nulstillingslinket er ugyldigt.")]
    string Token,
    [Required(ErrorMessage = "Ny adgangskode er påkrævet.")]
    string NewPassword
);
