# Idriftsættelse på Simply

Mål: `https://lisbergthreads.dk`, hvor ASP.NET Core serverer både REST API og Blazor-klienten fra samme adresse. MobilePay er deaktiveret, indtil erhvervsaftale og API-adgang er klar. Der er ikke foretaget upload, databaseopdatering eller rigtige betalinger under klargøringen.

## Før offentlig drift

Alle tre projekter bruger .NET 10 LTS (`net10.0`) og Microsoft-pakker i version 10.0.13. Brug .NET 10 SDK ved build. Projektet er opgraderet fra .NET 9, som mistede sikkerhedssupport den 10. november 2026. Se [Microsofts supportpolitik](https://dotnet.microsoft.com/en-us/platform/support/policy). En selvstændig publicering medbringer sin runtime og skal publiceres igen ved sikkerhedsopdateringer.

Køb et Windows/ASP.NET-webhotel og en SQL Server/MSSQL-database. Applikationen benytter SQL Server; en MySQL-database kan ikke bruges med den nuværende EF-opsætning. Aktivér HTTPS for `lisbergthreads.dk` og `www.lisbergthreads.dk`; brug samme primære domæne hele vejen gennem checkout. Indstil servermiljøet til `Production`.

Simply beskriver [self-contained publicering](https://www.simply.com/dk/support/faq/asp/361-deploy-net-med-self-contained-deployment-scd/) med `win-x86`. Scriptet bruger denne arkitektur som standard; brug kun `win-x64`, hvis dit webhotel er sat op til det. [OutOfProcess](https://www.simply.com/en/support/faq/asp/827-multiple-asp-net-core-5-apps-on-the-same-web-hosting/) vælges i publiceringen.

## Privat serverkonfiguration

Kopiér `appsettings.Local.example.json` til `appsettings.Local.json` i API'ets mappe på serveren, ved siden af programmets exe-fil. Udfyld kun denne private kopi. Filen skal aldrig ligge i klientens `wwwroot`, indgå i Git eller deles i en chat. Scriptet uploader den ikke. Miljøvariabler kan også anvendes og har højere prioritet end filen. Genstart programmet efter ændringer.

| Indstilling | Indhold |
|---|---|
| `ConnectionStrings:RemoteConnection` | Simplys SQL Server-navn, database og særskilt databasebruger. Brug krypteret forbindelse med et gyldigt servercertifikat. Brug ikke udviklingsserverens `sa`-login. |
| `Jwt:Key` | Ny, tilfældig nøgle, mindst 32 bytes. Genbrug aldrig en nøgle fra repositoryets historik. |
| `Jwt:Issuer` / `Jwt:Audience` | `LisbergThreads` / `LisbergThreads.Client`. |
| `Smtp:Host` | `websmtp.simply.com` ved afsendelse fra Simplys webservere. |
| `Smtp:Port` / `Smtp:EnableSsl` | `587` / `true` (STARTTLS). |
| `Smtp:UserName` / `Smtp:Password` | En oprettet Simply-mailkonto og dens private adgangskode. |
| `Smtp:FromAddress` | Afsenderadresse på det domæne, mailkontoen tilhører. |
| `Contact:RecipientEmail` | Den indbakke, som skal modtage kontaktbeskeder. |
| `PasswordReset:ClientBaseUrl` | `https://lisbergthreads.dk`. |
| `MobilePay:ClientBaseUrl` | `https://lisbergthreads.dk`, eller det relevante HTTPS-testdomæne. |
| `Bootstrap:AdminUserIds` | Som udgangspunkt tom. Eksisterende Admin-roller bevares i databasen. |

En JWT-nøgle kan genereres lokalt med `openssl rand -base64 64`. Indsæt resultatet direkte i den private serverkonfiguration; undlad at gemme det i shellhistorik, Git eller dokumentation. Miljøvariabler bruger dobbelte understregninger, fx `Jwt__Key` og `Smtp__Password`.

For nye administratorer: registrér først den rigtige konto, kontrollér dens identitet og kopiér brugerens database-id til den private `Bootstrap:AdminUserIds`-liste. Genstart én gang, og tøm derefter listen igen. Automatisk tildeling sker ud fra et bestemt bruger-id. Der er ingen administratoradresser eller administratoradgangskoder i kildekoden.

Lokalt kan du bruge `dotnet user-secrets` eller en ignoreret `appsettings.Local.json`. User secrets gælder kun i Development og installeres ikke på webhotellet. Den lokale JWT-nøgle blev udskiftet under sikkerhedsklargøringen; log ind igen efter genstart.

Simplys [SMTP-vejledning](https://www.simply.com/dk/support/faq/mail/23-udgaende-mailserver-til-scripts/) gælder afsendelse fra deres servere. Ved lokal afprøvning af en Simply-mailkonto bruges `smtp.simply.com` i stedet. Opsæt domænets SPF, DKIM og DMARC i Simplys kontrolpanel.

## Database og billeder

Tag backup før ændringer. De nye migrationer opretter `MobilePayPayments` og tilføjer `Orders.RowVersion`; de udfører ingen betalinger og ændrer ikke eksisterende ordres status.

Generér et SQL-script fra repositoryets rod, efter at den private forbindelse er konfigureret:

```sh
dotnet tool restore
dotnet ef migrations script --idempotent --project LisbergThreads.Api/LisbergThreads.Api.csproj --output /tmp/lisbergthreads-migrations.sql
```

Gennemgå scriptet og anvend det på den korrekte SQL Server-database før det nye API startes. Flyt eventuelle eksisterende data med en separat databasebackup; migrationer kopierer ikke kunder, produkter eller ordrer. Programmet anvender ikke migrationer automatisk ved opstart.

Billeder gemmes som standard i `App_Data/uploads/products`, og nøgler til nulstilling af adgangskoder i `App_Data/keys`. Giv applikationen skriveadgang. Kopiér de eksisterende produktbilleder til billedmappen med uændrede filnavne. URL'erne forbliver `/uploads/products/...`. Brug `Uploads:ProductsPath` og `DataProtection:KeysPath`, hvis mapperne skal ligge et andet sted. Bevar mapperne og den private konfiguration ved nye publiceringer, og tag backup af dem. På Windows krypteres Data Protection-nøgler med DPAPI for applikationens Windows-identitet.

## MobilePay-aftale og aktivering

Opret en erhvervsaftale med Vipps MobilePay til ePayment/onlinebetaling. En privat MobilePay-konto eller et telefonnummer er ikke tilstrækkeligt. Hent nøglerne fra erhvervsportalen og gennemfør [ePayment-checklisten](https://developer.vippsmobilepay.com/docs/APIs/epayment-api/checklist/).

Udfyld følgende privat:

- `MobilePay:ClientId`
- `MobilePay:ClientSecret`
- `MobilePay:SubscriptionKey`
- `MobilePay:MerchantSerialNumber`
- `MobilePay:BaseUrl`: først `https://apitest.vipps.no/`; efter godkendte test `https://api.vipps.no/` med produktionsnøgler.
- `MobilePay:Enabled`: `true`, når opsætningen er komplet.

Integrationen bruger DKK og MobilePay-wallet med `WEB_REDIRECT`. Først gemmes et betalingsforsøg med serverens aktuelle priser. Kunden godkender beløbet hos MobilePay. Serveren kontrollerer betalingsreference, valuta, beløb, aktive varer og lager igen. Derefter gemmes ordre, lagertræk og betalingsresultat i én databasetransaktion. Hvis lageret er utilstrækkeligt, annulleres reservationen; hvis annulleringen fejler midlertidigt, forsøger serveren igen. Kurve og ventende betalinger reserverer ikke lager.

Ordren oprettes med `Placed` og betalingsstatus `Reserved`. Ved `Confirmed -> Shipped` hæver API'et det reserverede beløb og gemmer `Paid`. Ordreflowet er fortsat `Placed -> Confirmed -> Shipped -> Completed`. Gentagne forespørgsler bruger samme betalingsreference og idempotensnøgler. Ældre ordrer uden en verificeret MobilePay-betaling kan ikke hæves automatisk; kontrollér disse manuelt. Refusion og manuel annullering af gennemførte ordrer håndteres i erhvervsportalen; de skal også afstemmes med lager og ordrebehandling. Der er ikke tilføjet et retur-/refusionsflow til administrationssiden.

Serveren kontrollerer uafsluttede betalingsforsøg i baggrunden, også når kunden lukker appen uden at vende tilbage. IIS kan standse eller genstarte en app; afklar driftsindstillingerne med Simply, og hold appen aktiv med en ekstern kontrol af `/health`. Efter en genstart genoptages de gemte forsøg. Kontrollér erhvervsportalens reservationer løbende, og håndtér dem inden MobilePays frister. Der er ikke registreret webhooks eller sat ekstern overvågning op.

## Publicering

Kør fra repositoryets rod med Bash, fx i Linux eller Git Bash:

```sh
bash LisbergThreads.Api/scripts/publish-simply.sh
```

Upload indholdet af den viste `site`-mappe til webhotellets applikationsrod. Publiceringen indeholder både API og klient samt en Windows-runtime. Den indeholder ikke private appsettings, User Secrets eller udviklingsopsætningen. Tilføj din private `appsettings.Local.json` direkte på serveren, og behold den ved efterfølgende uploads. Kontrollér den genererede `web.config`, som skal starte `LisbergThreads.Api.exe` med OutOfProcess. Stop appen under udskiftning af filer, og genstart efter upload.

## Manuel verifikation før åbning

Build og pakkekontrol erstatter ikke disse manuelle test. Brug MobilePays testmiljø og testapp til betalingsforløbene.

1. Åbn `/`, `/kontakt`, `/checkout` og en direkte produktadresse over HTTPS. Genindlæs siderne. Klientens API-kald skal gå til samme domæne. En ukendt `/api/...`-adresse skal give 404.
2. Send en gyldig kontaktbesked. Forvent succes efter SMTP-accept, mail i den rigtige indbakke samt Reply-To til kundens adresse. Ugyldig mail skal afvises. Ved forkert SMTP-opsætning skal teksten bevares, og der må ikke vises succes. Fjerde henvendelse inden for ti minutter fra samme IP skal afvises med 429.
3. Gennemfør et testkøb både som gæst og som kunde. Forvent én ordre med korrekte databasepriser, `Placed`/`Reserved`, og ét lagertræk. Gentag/statusopdatér betalingen: ingen ekstra ordre eller ekstra lagertræk. Mine ordrer må kun vise egne ordrer; gæsteordren kræver korrekt token eller Admin-adgang.
4. Afbryd eller lad betalingen udløbe. Forvent ingen ordre og intet lagertræk; kurven bevares. Luk browseren efter godkendelse: baggrundskontrollen skal stadig færdiggøre ordren. Genstart API'et under et ventende forsøg: kontrollen skal genoptages.
5. Lad to kunder betale for sidste vare. Kun én ordre må gennemføres. Den anden reservation skal annulleres, og lageret må aldrig blive negativt. Prøv også gentagne produktlinjer, antal 0/negative antal og deaktiverede produkter via API'et.
6. Skift som Admin `Placed -> Confirmed -> Shipped -> Completed`. Ved `Shipped` skal erhvervsportalen vise én hævning, og ordren skal vise `Paid`. Gentagne klik og parallelle requests må ikke give dobbelt hævning eller føre status baglæns. En kunde må ikke ændre ordrestatus.
7. Test login, nulstilling af adgangskode og billed-upload efter en genstart. Forsøg at hente `/appsettings.Local.json` og `/App_Data/keys/...`: der må ikke udleveres private filer. Kontrollér klientens downloadede filer og serverloggen for hemmeligheder.
