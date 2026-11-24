using System;
using System.ComponentModel.DataAnnotations;

namespace LisbergThreads.Contracts.Authentication;

public record RegisterRequest(
    [Required(ErrorMessage = "Fornavn er påkrævet.")]
    string FirstName,
    [Required(ErrorMessage = "Efternavn er påkrævet.")]
    string LastName,
    [Required(ErrorMessage = "E-mail er påkrævet.")]
    [EmailAddress(ErrorMessage = "Indtast en gyldig e-mailadresse.")]
    string Email,
    [Required(ErrorMessage = "Telefonnummer er påkrævet.")]
    [RegularExpression(@"^[0-9]+$", ErrorMessage = "Telefonnummer må kun indeholde tal.")]
    string PhoneNumber,
    [Required(ErrorMessage = "Adgangskode er påkrævet.")]
    string Password
);
