using System.Collections.ObjectModel;

namespace LisbergThreads.Contracts.Content;

public static class HomeTextDefaults
{
    public static IReadOnlyDictionary<string, string> Values { get; } =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
        {
            ["HeroEyebrow"] = "Håndlavet med kærlighed",
            ["HeroTitle"] = "Små masker.",
            ["HeroTitleAccent"] = "Stor kærlighed.",
            ["HeroIntroduction"] = "Velkommen indenfor hos Lisberg Threads. Her finder du strik og syet børnetøj, skabt i hånden med tid og omtanke — til de små og til dig, der holder af håndarbejde.",
            ["HeroNote"] = "Fra mine hænder til jeres hverdag.",
            ["CraftHandmade"] = "Syet og strikket i hånden",
            ["CraftEveryday"] = "Til små hverdagsøjeblikke",
            ["CraftShop"] = "En personlig, lille butik",
            ["NewEmptyTitle"] = "Der er håndarbejde på vej",
            ["NewEmptyText"] = "Her kommer nye kreationer, når de er klar til at finde hjem. Kig gerne ind igen.",
            ["PopularEmptyTitle"] = "De første favoritter er på vej",
            ["PopularEmptyText"] = "Her samler vi de mest bestilte produkter, når de har fundet vej til deres nye hjem.",
            ["StoryEyebrow"] = "Et menneske bag hver maske",
            ["StoryTitle"] = "Velkommen til",
            ["StoryTitleSecond"] = "min lille butik.",
            ["StoryIntroduction"] = "Jeg står selv bag symaskinen og strikkepindene hos Lisberg Threads. Her deler jeg det, jeg syr og strikker — med en særlig plads til hjemmelavet børnetøj og glæden ved at skabe med hænderne.",
            ["StoryInvitation"] = "Jeg håber, du vil føle dig hjemme, kigge dig omkring og finde noget, du har lyst til at tage med ind i jeres hverdag.",
            ["StoryThanks"] = "Tak, fordi du kigger forbi og støtter mit håndarbejde.",
            ["HowEyebrow"] = "Når du har fundet noget, du holder af",
            ["HowTitle"] = "Fra min butik til din kurv",
            ["HowFlow"] = "Find produkt → Kurv → Checkout",
            ["StepOneTitle"] = "Find din favorit",
            ["StepOneText"] = "Udforsk produkterne, se billederne, og find den størrelse, der passer til dig.",
            ["StepTwoTitle"] = "Læg i kurven",
            ["StepTwoText"] = "Vælg antal, og saml dine favoritter. I kurven kan du justere antal og se den samlede pris.",
            ["StepThreeTitle"] = "Gennemfør din ordre",
            ["StepThreeText"] = "Fortsæt til checkout fra kurven. Når ordren er oprettet, får du en ordrebekræftelse med overblik over dit køb.",
            ["GuestNote"] = "Du kan også bestille som gæst.",
            ["FooterTagline"] = "Strik og børnetøj, skabt i hånden.",
            ["FooterInformationTitle"] = "Godt at vide",
            ["FooterInformation"] = "Du kan handle som gæst eller med en konto. Når du er logget ind, kan du finde din ordrehistorik under Mine ordrer.",
            ["FooterClosing"] = "Små masker. Stor kærlighed.",
            ["nye-produkterEyebrow"] = "Det nyeste i den lille butik",
            ["nye-produkterTitle"] = "Nye produkter",
            ["nye-produkterDescription"] = "Se de seneste tilføjelser, og find noget til en lille, du holder af.",
            ["populaere-produkterEyebrow"] = "Favoritter, der finder hjem",
            ["populaere-produkterTitle"] = "Populære produkter",
            ["populaere-produkterDescription"] = "Det håndarbejde, der er blevet bestilt flest af i butikken.",
        });
}
