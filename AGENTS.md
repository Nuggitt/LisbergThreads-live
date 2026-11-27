# Lisberg Threads – instruktioner til kodeassistenten

## Formål og samarbejde

Hjælp med at udvikle Lisberg Threads, og gør ændringerne forståelige for brugeren undervejs.
Kommunikér på dansk. Behold eksisterende engelske navne i kode og DTO'er.
Forklar kort, hvad der ændres og hvorfor; forklar tekniske begreber, når de er nødvendige.
Læs relevant kode, før du foreslår eller foretager ændringer. Skeln mellem det, du har
bekræftet i koden, og det, du antager.

## Oplysninger ved ændringer

Før hver sammenhængende gruppe af ændringer skal du oplyse:

1. **Projekt:** Det berørte projekt eller projektets rod.
2. **Fil:** Den konkrete filsti; brug et klikbart link, når filen findes.
3. **Klasse/komponent:** Det faktiske navn og eventuelt den relevante metode.
   Skriv "Ikke relevant", hvis filen ikke har en klasse eller komponent.
4. **Ændring:** Hvad der ændres, hvorfor, og hvilken adfærd brugeren får.

Ved flere filer kan oplysningerne samles i en kort tabel med én række pr. fil.
Undgå at gentage den samme forklaring for hver lille redigering.
Giv korte statusopdateringer ved længere arbejde.
Afslut med, hvad der er lavet, resultatet af build og relevante tests samt eventuelle
begrænsninger. Oplys nødvendige manuelle trin, hvis der er nogen.
Påstå aldrig, at noget er testet eller virker i browseren, hvis det ikke er afprøvet.

## Arkitektur

- `LisbergThreads.Api`: ASP.NET Core REST API, servervalidering, adgangskontrol og EF Core.
- `LisbergThreads.Client`: Blazor WebAssembly, sider, komponenter og brugerinteraktion.
- `LisbergThreads.Contracts`: DTO'er og kontrakter delt mellem API og klient.
- Database: SQL Server via Entity Framework Core.
- Applikationens target framework: .NET 10 LTS (`net10.0`).
- Solution: `LisbergThreads.Api/LisbergThreads.sln`.

Følg eksisterende arkitektur, navngivning og mønstre. Bevar opdelingen mellem
`.razor` og `.razor.cs`, hvor den allerede bruges.
Brug delte DTO'er til kommunikation mellem API og klient.
Indfør ikke unødvendige abstraktioner, ekstra arkitekturlag eller nye afhængigheder.
Hold ændringer fokuseret på opgaven, og bevar eksisterende, uvedkommende ændringer.
Hvis en opgave kræver ændringer i DTO'er eller databaseskema, forklar konsekvenserne
for både API, klient og eventuelle migrationer.

### Opdeling i API'et

- Controllers håndterer kun HTTP: route, `[Authorize]`, bruger-id fra claims, DTO ind og ud og statuskode.
- Forretningsregler, transaktioner og låse ligger i én service pr. område under
  `LisbergThreads.Api/Services/` (fx `Cart`, `Checkout`, `Orders`, `Products`, `Payments`), ikke én pr. endpoint.
  En controller uden forretningsregler må bruge `AppDbContext` direkte.
- Hver regel findes ét sted: produktvalidering i `ProductValidator`, ordretotal og statusskift i `Order`,
  lagertræk i `Product.DeductStock`, ordre og lager ved checkout i `CheckoutService`.
- Afviste regler kastes som `ServiceException` med `ServiceError` og dansk tekst; den oversættes til statuskode
  i `Extensions/ExceptionHandlingExtensions.cs`. Brug ikke try/catch med statuskoder i controllers.
- Brug konstanterne `OrderStatuses`, `PaymentMethods`, `PaymentStatuses` og `PaymentStates` frem for strenge.
- Registrér konkrete klasser i DI. Indfør ikke repositories, og kun interfaces ved et konkret behov,
  fx en ekstra implementation. Ny opsætning fra `Program.cs` lægges i extension-metoder under `Extensions/`.

## Ordrer

Det gyldige ordreflow er:

`Placed -> Confirmed -> Shipped -> Completed`

Brug de eksisterende `OrderStatuses`-konstanter. Håndhæv gyldige statusskift i API'et.
`Pending` bruges til en aktiv kurv og indgår ikke i ovenstående ordreflow.
Bevar eksisterende adgangskontrol for kunder, administratorer og gæsteordrer.

## Lager

- `Product.StockQuantity` i databasen er den autoritative lagerbeholdning.
- Lagerkontrol skal altid udføres server-side. Frontendkontrol er kun til brugeroplevelsen.
- Kontrollér positive antal, aktive produkter og samlet antal pr. produkt, inklusive
  gentagne produktlinjer og varer, der allerede ligger i kurven.
- Kontrollér lageret igen ved checkout; stol ikke på lagerantal eller priser fra klienten.
- Varer i kurven reserverer ikke lager. Lageret trækkes ved gennemført checkout.
- Gem ordre og lagerændring samlet, så et afvist køb ikke efterlader delvise ændringer.
- Bevar beskyttelsen mod samtidige køb, og undgå negativ lagerbeholdning.
- Reglerne gælder både gæster og brugere, der er logget ind.
- Vis "Udsolgt" ved lagerantal 0, og forhindr tilføjelse af udsolgte varer i brugerfladen.

## Verifikation

Efter kodeændringer skal du køre følgende fra projektets rod:

```text
dotnet build LisbergThreads.Api/LisbergThreads.sln
```

Brugeren ønsker selv at teste funktionaliteten manuelt. Opret ikke automatiske
tests eller et testprojekt, medmindre brugeren specifikt beder om det.
Ved adfærdsændringer skal du give en kort liste over relevante manuelle testtrin
og forventede resultater, især for checkout, lager og adgangskontrol.
Skeln tydeligt mellem et bestået build og funktionalitet, der er testet manuelt.
Hvis build ikke kan køres, oplys den konkrete årsag og hvad der fortsat
er uverificeret. Ret fejl fra dine ændringer, før du melder opgaven færdig.
Ved ændringer udelukkende i dokumentation er build ikke nødvendigt.
