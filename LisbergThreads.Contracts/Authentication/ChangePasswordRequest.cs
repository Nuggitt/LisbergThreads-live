using System.ComponentModel.DataAnnotations;

namespace LisbergThreads.Contracts.Authentication;

public record ChangePasswordRequest(
    [Required(ErrorMessage = "Nuværende adgangskode er påkrævet.")]
    string CurrentPassword,
    [Required(ErrorMessage = "Ny adgangskode er påkrævet.")]
    string NewPassword
);
