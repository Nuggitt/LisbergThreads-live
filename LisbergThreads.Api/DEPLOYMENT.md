# Idriftsættelse på Simply

Mål: `https://lisbergthreads.dk`, hvor ASP.NET Core serverer både REST API og Blazor-klienten fra samme adresse. MobilePay holdes deaktiveret, indtil erhvervsaftale og API-adgang er klar. Betalinger og browserforløb skal afprøves særskilt før åbning for rigtige køb.

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
| `Checkout:AllowTestOrders` | `false` ved offentlig drift. Uden aktiveret MobilePay kan kunder derefter ikke gennemføre køb. |

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

Billeder gemmes som standard i `App_Data/uploads/products`, og nøgler til nulstilling af adgangskoder i `App_Data/keys`. Giv applikationen skriveadgang. Hvis eksisterende produkter senere flyttes, kopieres deres billeder med uændrede filnavne. Ved en tom produktdatabase uploades nye billeder gennem administrationen. URL'erne er `/uploads/products/...`. Brug `Uploads:ProductsPath` og `DataProtection:KeysPath`, hvis mapperne skal ligge et andet sted. Bevar mapperne og den private konfiguration ved nye publiceringer, og tag backup af dem. På Windows krypteres Data Protection-nøgler med DPAPI for applikationens Windows-identitet.

## Testcheckout uden MobilePay

I både Development og Production er `Checkout:AllowTestOrders` sat til `true` og `MobilePay:Enabled` til `false`, så projektet kan bruges som demonstration uden en MobilePay-aftale. Checkout viser **Gennemfør testordre** og opretter ordren uden at kontakte MobilePay. Ordren får betalingsmåde og betalingsstatus `Test`, og der reserveres eller hæves ingen penge.

Testordrer validerer kontaktoplysninger, aktive produkter, samlede antal og databasepriser på serveren. Ordre og lagertræk gemmes i samme transaktion. Gentagne forespørgsler med samme id opretter ikke ekstra ordrer. Testordrer følger det almindelige ordreflow, men afsendelse udløser ingen MobilePay-hævning.

Testcheckout kan slås fra ved at sætte `Checkout:AllowTestOrders` til `false` i den private serverkonfiguration. Hvis MobilePay aktiveres, anvendes den rigtige betalingsintegration, også hvis testflaget stadig er sat. Et køb uden betaling kan derfor ikke vælges fra browseren, når serveren bruger den rigtige MobilePay-integration.

Genstart API'et efter ændring af indstillingerne. Test manuelt både som gæst og som indlogget bruger: gennemfør én ordre, kontrollér `Placed`/`Test`, kurven og ét lagertræk. Genindlæs ordrebekræftelsen: ingen ekstra ordre eller lagerændring. Afprøv også manglende lager og ugyldige antal; de skal afvises uden en gennemført ordre. Som Admin skal du kunne følge ordreflowet til `Completed`, mens betalingsstatus forbliver `Test`.

Før offentlig drift sættes `Checkout:AllowTestOrders` til `false` i den private serverkonfiguration. Det tilsidesætter demonstrationsindstillingen ovenfor. MobilePay kan fortsat være deaktiveret, mens aftalen er under oprettelse; produktkatalog og login kan bruges, men checkout kan ikke gennemføres.

## MobilePay-aftale og aktivering

Opret en erhvervsaftale med Vipps MobilePay til ePayment/onlinebetaling. En privat MobilePay-konto eller et telefonnummer er ikke tilstrækkeligt. Hent nøglerne fra erhvervsportalen og gennemfør [ePayment-checklisten](https://developer.vippsmobilepay.com/docs/APIs/epayment-api/checklist/).

Bestil produktet **Payment integration (ePayment)** til den danske virksomhed. Den eksisterende kode anvender ePayment API'et direkte; følg [Vipps MobilePays opstartsvejledning](https://developer.vippsmobilepay.com/docs/getting-started/) for aftale og testadgang. Hold test- og produktionsnøgler adskilt. Afprøv godkendelse, afbrydelse, manglende lager og hævning i testmiljøet, før produktionsnøgler indsættes. API-nøgler udfyldes direkte i den private serverkonfiguration og indgår ikke i GitHub-workflowet.

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

### Automatisk publicering fra GitHub

Workflowet `.github/workflows/deploy-simply.yml` bygger løsningen og publiceringspakken ved pull requests til `main`. Et push eller en merge til `main` bygger og uploader også til Simply. Det kan desuden startes manuelt fra Actions med `main` valgt. Uploads kører én ad gangen og afbrydes ikke af et nyt push.

Upload bruger [Simplys explicit FTPS på port 21](https://www.simply.com/en/support/faq/ftp/76-setting-up-ftp-and-ftps/) og kontrollerer serverens TLS-certifikat. Uploadscriptet benytter kun Pythons standardbibliotek. Det overskriver applikationens publicerede filer, bevarer eksisterende `App_Data` og private appsettings og sletter ingen øvrige serverfiler. Ældre publiceringsfiler fjernes ikke automatisk; kontrollér pladsforbruget ved større eller mange publiceringer.

I repositoryets **Settings → Secrets and variables → Actions → Secrets** oprettes:

| Secret | Indhold |
|---|---|
| `SIMPLY_FTP_HOST` | `ftp.simply.com`, eller FTP-serveren oplyst af Simply. |
| `SIMPLY_FTP_USERNAME` | Webhotellets FTP-brugernavn. |
| `SIMPLY_FTP_PASSWORD` | Webhotellets private produktadgangskode. |

Valgfrie **Variables**: `SIMPLY_RUNTIME` (`win-x86` som standard) og `SIMPLY_REMOTE_DIR` (`/public_html` som standard). Brug kun `win-x64`, hvis webhotellet er konfigureret til det. Der skal ikke oprettes GitHub-secrets for databaseforbindelsen, login-nøglen eller MobilePay: serveren beholder sin private `appsettings.Local.json`.

Før første automatiske upload skal webhotellet bruge Windows/ASP.NET, HTTPS skal være aktivt, og den udfyldte private `appsettings.Local.json` skal ligge i `/public_html` ved siden af exe-filen. En tom login-nøgle forhindrer opstart. SMTP skal konfigureres før kontaktformular og nulstilling af adgangskode kan bruges. Bevar `Checkout:AllowTestOrders=false`, indtil den rigtige betaling er klar.

En første upload kan også foretages lokalt med FTPS-oplysninger i miljøvariabler og den private konfiguration valgt eksplicit:

```sh
python3 LisbergThreads.Api/scripts/deploy-simply.py /sti/til/site \
  --settings LisbergThreads.Api/artifacts/simply-config/appsettings.Local.json
```

`--settings` er kun til første opsætning eller en bevidst konfigurationsændring. GitHub-workflowet anvender ikke denne mulighed. Scriptet kontrollerer først, at pakken er komplet og uden private filer. `--check-only` udfører alene denne lokale pakkekontrol.

Under upload viser IIS en kort vedligeholdelsesside via `app_offline.htm`, så DLL- og exe-filer kan udskiftes. Ved afbrudt upload bliver siden liggende; en ny kørsel færdiggør uploaden og starter appen igen. Efter upload skal `https://lisbergthreads.dk/health` svare `Healthy`, før deployment markeres som bestået. En fejl her kræver kontrol af appens log hos Simply; uploaden rulles ikke automatisk tilbage.

Workflowet anvender ingen databasemigrationer og flytter ingen produkter, ordrer eller kunder. Ved fremtidige skemaændringer gennemgås og anvendes migrationsscriptet på databasen, før den tilhørende appversion publiceres.

Kontrollér manuelt ved den første publicering: begge administratorer kan logge ind, produkt- og ordrelisten er tom, et uploadet billede vises efter genstart, og checkout afviser køb, når både MobilePay og testordrer er slået fra. Ved næste ændring til `main` kontrolleres, at Actions bliver grøn, siden er opdateret, og eksisterende billeder samt login fortsat virker.

## Manuel verifikation før åbning

Build og pakkekontrol erstatter ikke disse manuelle test. Brug MobilePays testmiljø og testapp til betalingsforløbene.

1. Åbn `/`, `/kontakt`, `/checkout` og en direkte produktadresse over HTTPS. Genindlæs siderne. Klientens API-kald skal gå til samme domæne. En ukendt `/api/...`-adresse skal give 404.
2. Send en gyldig kontaktbesked. Forvent succes efter SMTP-accept, mail i den rigtige indbakke samt Reply-To til kundens adresse. Ugyldig mail skal afvises. Ved forkert SMTP-opsætning skal teksten bevares, og der må ikke vises succes. Fjerde henvendelse inden for ti minutter fra samme IP skal afvises med 429.
3. Gennemfør et testkøb både som gæst og som kunde. Forvent én ordre med korrekte databasepriser, `Placed`/`Reserved`, og ét lagertræk. Gentag/statusopdatér betalingen: ingen ekstra ordre eller ekstra lagertræk. Mine ordrer må kun vise egne ordrer; gæsteordren kræver korrekt token eller Admin-adgang.
4. Afbryd eller lad betalingen udløbe. Forvent ingen ordre og intet lagertræk; kurven bevares. Luk browseren efter godkendelse: baggrundskontrollen skal stadig færdiggøre ordren. Genstart API'et under et ventende forsøg: kontrollen skal genoptages.
5. Lad to kunder betale for sidste vare. Kun én ordre må gennemføres. Den anden reservation skal annulleres, og lageret må aldrig blive negativt. Prøv også gentagne produktlinjer, antal 0/negative antal og deaktiverede produkter via API'et.
6. Skift som Admin `Placed -> Confirmed -> Shipped -> Completed`. Ved `Shipped` skal erhvervsportalen vise én hævning, og ordren skal vise `Paid`. Gentagne klik og parallelle requests må ikke give dobbelt hævning eller føre status baglæns. En kunde må ikke ændre ordrestatus.
7. Test login, nulstilling af adgangskode og billed-upload efter en genstart. Forsøg at hente `/appsettings.Local.json` og `/App_Data/keys/...`: der må ikke udleveres private filer. Kontrollér klientens downloadede filer og serverloggen for hemmeligheder.

## Fælles repository og udviklingsmiljø på NAS

Brug `Nuggitt/LisbergThreads-live` som fælles repository. Den nye Git-historik er udgangspunkt for begge miljøer. Det tidligere `Nuggitt/LisbergThreads` må ikke merges eller bruges som kilde til gamle branches/tags i det nye repository, da den tidligere historik indeholder eksponeret konfiguration. Eksponerede nøgler skal være udskiftet i alle miljøer, der brugte dem; en frisk klon erstatter ikke dette.

| Branch | Anvendelse | Deployment |
|---|---|---|
| `feature/...` | Én samlet feature eller rettelse | Pull request til `develop`; GitHub bygger løsningen og publiceringspakken. |
| `develop` | Integration og manuel afprøvning | NAS-deployment til `https://lisbergthreads.nuggit.dk`, når NAS-konfigurationen er omlagt. |
| `main` | Godkendte ændringer | Eksisterende GitHub-workflow uploader til Simply på `https://lisbergthreads.dk`. |

Udvikl på en feature-branch, merge til `develop`, og afprøv resultatet på NAS'en. Når ændringerne er klar, oprettes en pull request fra `develop` til `main`. En akut rettelse på `main` skal også føres tilbage til `develop`. Push og pull requests til begge branches køres gennem GitHub-buildet; kun en kørsel på `main`, der ikke er en pull request, kan udføre Simply-upload. NAS'en har sin egen Linux/container-publicering og skal ikke bruge Simplys Windows-pakke.

NAS'en har ifølge den eksisterende driftsprocedure en kontrol hvert femte minut, container-build, health-kontrol, kontrol af hjemmesiden, rollback og Gotify-beskeder. Før omlægningen gennemgås den faktiske NAS-konfiguration og deploymentkode. Brug en frisk klon af `https://github.com/Nuggitt/LisbergThreads-live.git` på `develop`, og tilpas den eksisterende automatik til denne kilde. Genbrug de eksisterende driftsscripts og Docker-/Compose-filer efter gennemgang; kopier ingen hemmeligheder til Git. Hold automatisk deployment tilbage under selve skiftet, og lad aldrig to deploymentforløb køre samtidig.

Bevar NAS'ens database, uploadede billeder, private konfiguration, persistente nøgler og navngivne volumes/bind mounts. Bevar også Compose-projektnavnet, hvis det bruges til volumenavne. Kontrollér de faktiske billedstier, da billeder fra det gamle repository ikke indgår i den nye kildekopi. Dev og Simply skal fortsat have separate databaser, billeder og private indstillinger. Gamle commits fra NAS'ens tidligere repository er ikke nødvendigvis tilgængelige i den nye klon; migrationskontrol, gemt sidste succes og fejlmarkeringer skal håndtere det kontrolleret uden at sammenflette historikker eller omgå migrationsstoppet.

Nye eller ændrede migrationer skal fortsat stoppe automatisk NAS-deployment. Følg den eksisterende procedure med databasebackup, gennemgang/anvendelse af migrationer og manuel deployment. Kontrollér databasen før den første opstart fra den nye kilde. Simply-workflowet anvender heller ikke migrationer automatisk; skemaændringer skal være anvendt før publicering dér.

Afslut NAS-omlægningen med en manuel deployment af en konkret commit på `develop`. Kontrollér containernes health, hjemmesiden og API'et samt at eksisterende billeder og databaseindhold er bevaret. Registrér først den nye succesfulde commit efter kontrollen. Genoptag derefter femminutterskontrollen, og kontrollér dens repository, branch og næste kørsel. En fejlet commit skal fortsat kræve en ny commit eller den eksisterende manuelle genkørselsprocedure. Ændringen af NAS-konfigurationen foretages på NAS'en; oprettelse af `develop` i GitHub udfører ikke dette skift alene.
