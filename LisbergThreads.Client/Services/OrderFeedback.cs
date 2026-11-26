using System.Net;

namespace LisbergThreads.Client.Services;

internal static class OrderFeedback
{
    public static string CartError(HttpResponseMessage response) => response.StatusCode switch
    {
        HttpStatusCode.Unauthorized => "Din session er udløbet. Log ind igen og kontrollér kurven.",
        HttpStatusCode.Forbidden => "Du har ikke adgang til denne kurv.",
        HttpStatusCode.NotFound => "Varen findes ikke længere i kurven. Hent kurven igen.",
        HttpStatusCode.BadRequest => "Antallet kunne ikke ændres. Kontrollér at varen stadig er tilgængelig, og at antallet ikke overstiger lageret.",
        HttpStatusCode.Conflict => "Kurven eller lageret er ændret. Hent kurven igen og kontrollér varerne.",
        _ => "Kurven kunne ikke ændres. Hent kurven igen og kontrollér indholdet, før du prøver igen."
    };

    public static string CheckoutError(HttpResponseMessage response) => response.StatusCode switch
    {
        HttpStatusCode.Unauthorized => "Din session er udløbet. Log ind igen og kontrollér kurven.",
        HttpStatusCode.Forbidden => "Du har ikke adgang til at gennemføre denne ordre.",
        HttpStatusCode.BadRequest => "Ordren blev afvist. Udfyld alle kontakt- og leveringsoplysninger, og kontrollér varernes tilgængelighed og lagerantal i kurven.",
        HttpStatusCode.Conflict => "Kurv, lager eller levering er ændret. Kontrollér varerne og fragtprisen, og vælg pakkeshop igen. Genindlæs checkout, før du prøver igen.",
        HttpStatusCode.ServiceUnavailable => "Betaling eller levering er ikke tilgængelig lige nu. Prøv igen senere eller vælg en anden leveringsform.",

        _ => UnconfirmedCheckout
    };

    public const string UnconfirmedCart = "Ændringen kunne ikke bekræftes. Hent kurven igen og kontrollér indholdet, før du prøver igen.";
    public const string UnconfirmedCheckout = "Vi kunne ikke bekræfte, om ordren blev oprettet. Undlad at bestille igen, før du har kontrolleret Mine ordrer eller kontaktet Lisberg Threads.";
}
