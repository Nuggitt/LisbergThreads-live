# LisbergThreads

En webshop til håndlavede produkter, der samler produktkatalog, kundelogin, ordrebehandling og lagerstyring i én løsning.

Projektet forbinder en Blazor-brugerflade med et ASP.NET Core REST API og en SQL Server-database. Fokus er på købsforløbet fra produktvalg til ordrebehandling, med validering og adgangskontrol på serveren.

## Funktioner

- Produktkatalog med søgning, produktbilleder og lagerstatus.
- Kundekonti med login, profil og nulstilling af adgangskode.
- Kurv og checkout for både gæster og kunder med en konto.
- Administration af produkter, kunder og ordrer med rollebaseret adgang.
- Lagerkontrol og beskyttelse mod samtidige køb med EF Core-transaktioner og versionskontrol.
- Kontaktformular med SMTP-mailafsendelse og begrænsning af gentagne henvendelser.
- MobilePay-integration med reservation ved godkendelse og hævning ved afsendelse.

## Teknologier og struktur

Løsningen bruger **.NET 10**, **Blazor WebAssembly**, **ASP.NET Core**, **Entity Framework Core**, **SQL Server** samt **ASP.NET Core Identity og JWT**.

| Projekt | Ansvar |
|---|---|
| `LisbergThreads.Client` | Brugerflade, sider, komponenter og brugerinteraktion. |
| `LisbergThreads.Api` | REST API, servervalidering, adgangskontrol og databaseadgang. |
| `LisbergThreads.Contracts` | DTO'er og kontrakter delt mellem klient og API. |

Ordreflowet er `Placed → Confirmed → Shipped → Completed`. Varer i kurven reserverer ikke lager; lagerkontrollen gentages, før ordren gennemføres.

## Lokal opstart

Du skal bruge .NET 10 SDK og adgang til en SQL Server-database. Kommandoerne nedenfor køres fra repositoryets rod; fileksemplet bruger Bash.

### 1. Hent projektet

```bash
git clone https://github.com/Nuggitt/LisbergThreads-live.git
cd LisbergThreads-live
dotnet restore LisbergThreads.Api/LisbergThreads.sln
dotnet tool restore
```

### 2. Konfigurér API'et

```bash
cp LisbergThreads.Api/appsettings.Local.example.json LisbergThreads.Api/appsettings.Local.json
```

Udfyld `ConnectionStrings:RemoteConnection` med din udviklingsdatabase og `Jwt:Key` med en tilfældig, privat nøgle på mindst 32 bytes. Filen er ignoreret af Git. Du kan også bruge User Secrets eller miljøvariabler.

SMTP-indstillingerne skal konfigureres for at sende kontakt- og nulstillingsmails. Til lokal nulstilling af adgangskode skal `PasswordReset:ClientBaseUrl` være `https://localhost:7148`. Behold `MobilePay:Enabled` som `false`, indtil betalingsmiljøet er konfigureret.

### 3. Opdatér udviklingsdatabasen og byg løsningen

Følgende migrationskommando ændrer skemaet i den database, du har konfigureret. Brug en udviklingsdatabase, og tag backup af eksisterende data før opdatering.

```bash
dotnet ef database update --project LisbergThreads.Api -- --environment Development
dotnet build LisbergThreads.Api/LisbergThreads.sln
```

HTTPS-profilerne kræver et lokalt udviklingscertifikat, som browseren har tillid til. Det kan opsættes med `dotnet dev-certs https --trust`.

### 4. Start API og klient

Start API'et i én terminal:

```bash
dotnet run --project LisbergThreads.Api --launch-profile https
```

Start klienten i en anden terminal:

```bash
dotnet run --project LisbergThreads.Client --launch-profile https
```

- Klient: <https://localhost:7148>
- API og Swagger: <https://localhost:7013/swagger>

Rollerne oprettes ved API-opstart. Administratoradgang tildeles særskilt via privat konfiguration; der medfølger ingen fælles administratorlogin.

## Betaling og publicering

MobilePay kræver en erhvervsaftale, API-nøgler og afprøvning i testmiljøet før aktivering. Betaling, mail og hosting er endnu ikke verificeret i produktion.

Projektet er forberedt til Windows/ASP.NET-hosting hos Simply. Et push til GitHub opdaterer repositoryet; upload til webhotellet udføres separat.

Se [opsætning, publicering og manuelle testtrin](LisbergThreads.Api/DEPLOYMENT.md) samt [håndtering af konfiguration og hemmeligheder](LisbergThreads.Api/SECURITY.md).
