using System.ComponentModel.DataAnnotations;

namespace LisbergThreads.Contracts.Authentication;

public record ForgotPasswordRequest(
    [Required(ErrorMessage = "E-mail er påkrævet.")]
    [EmailAddress(ErrorMessage = "Indtast en gyldig e-mailadresse.")]
    [StringLength(256, ErrorMessage = "E-mail må højst indeholde 256 tegn.")]
    string Email
);
