using System.Net;

namespace LisbergThreads.Client.Services;

internal static class ProductFeedback
{
    public static string Error(HttpResponseMessage response, string fallback, bool cart = false) => response.StatusCode switch
    {
        HttpStatusCode.Unauthorized => "Din session er udløbet. Log ind igen og prøv igen.",
        HttpStatusCode.Forbidden => "Du har ikke adgang til at udføre denne handling.",
        HttpStatusCode.NotFound => "Produktet findes ikke længere eller er ikke tilgængeligt.",
        HttpStatusCode.BadRequest when cart => "Antallet kunne ikke tilføjes. Kontrollér lageret og antallet, der allerede ligger i kurven.",
        HttpStatusCode.BadRequest => "Kontrollér produktets navn, pris, lagerantal og billeder, og prøv igen.",
        HttpStatusCode.Conflict => "Produktet er ændret eller bruges i en ordre eller kurv. Genindlæs siden, før du prøver igen.",
        _ => fallback
    };

    public const string UnconfirmedSave = "Ændringen kunne ikke bekræftes. Kontrollér produktlisten, før du prøver at gemme igen.";
    public const string UnconfirmedCart = "Tilføjelsen kunne ikke bekræftes. Kontrollér kurven, før du prøver igen.";
}
