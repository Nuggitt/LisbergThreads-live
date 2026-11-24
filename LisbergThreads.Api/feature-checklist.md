# Lisberg Threads – feature checklist

Implementerede funktioner er krydset af. Testplan og manuel verifikation findes i [test-plan.md](test-plan.md); tests er ikke automatisk bekræftet, fordi en funktion er implementeret.

### Kodegennemgang 14. oktober 2026

De åbne implementeringspunkter er gennemgået i API, Contracts og klientens sider, router og styling. Gennemgangen er baseret på kildekoden; browser, betaling, deployment og manuelle testforløb er ikke afprøvet.

- Telefonnummer på admins kundedetaljer er implementeret: `CustomersController.GetCustomer` returnerer `CustomerDetailsDto.PhoneNumber`, og `Pages/Admin/Customers/Details.razor` viser nummeret eller "Ikke angivet".
- Betalingsstatus findes på ordremodellen, sendes via DTO'er og vises på både kundens og admins ordredetaljer. Checkout sætter automatisk status til `Paid`; dette bekræfter ikke en faktisk betaling. Betalingsintegration er stadig åben.
- `App.razor` bruger nu den fælles `RouteNotice`-komponent til danske 404-, 403- og loginvisninger med grøn styling og links til forsiden, produkter og login. Solution-build er bestået; browser- og mobilverifikation er fortsat åben.
- Kurv, sidebar, checkout, ordre- og kundedetaljer samt Min konto, produktredigering, produktdetaljer og forsiden bruger nu fælles `PageState`-visninger med spinner og danske tekster. Eksisterende oversigter har spinner, empty states og fejlvisninger; forsiden bevarer sine adminredigerbare tekster ved tomt sortiment. Visningerne er gennemgået i koden, men manuel verifikation er stadig åben.
- Success-visninger og formularfejl bruger fælles `FeedbackNotice`, og indlæsningsfejl bruger sidernes fejlvisning/Prøv igen. Gennemgangen af danske fejlbeskeder er afsluttet i koden, inklusive browserlagring, ordrebekræftelse og global fejltekst. Manuel verifikation findes i testplanen.
- Success-beskeder bruger nu `FeedbackNotice` på produkt-, kurv-, ordre- og kontosider samt ved redigering af forsidetekst. Manuelle tests er fortsat åbne.
- Produktknapper og antal-felter har nu en minimumshøjde på 44 px. Produkt- og checkout-handlinger kan ombrydes, kurvens tabel har en scrollbar-ramme, og søgefeltet følger navigationens layout. Mobil-layout er gennemgået og tilpasset i koden; visuel mobilverifikation er fortsat åben.

## 📦 Produkter

- [x] Product CRUD — permanent sletning med begrænsninger
- [x] Opret produkt
- [x] Rediger produkt
- [x] Deaktiver produkt
- [x] Aktivér produkt direkte fra produktlisten
- [x] Produktoversigt
- [x] Produktdetaljer
- [x] Lagerbeholdning på produkter
- [x] Upload/vælg produktbillede ved oprettelse
- [x] Upload/vælg produktbillede ved redigering
- [x] Reaktiv produktsøgning
- [x] Søgning på produktnavn
- [x] Vis søgeresultater, mens brugeren skriver

### Flere produktbilleder

- [x] Understøt flere billeder pr. produkt
- [x] Vis billedgalleri på produktdetaljesiden
- [x] Mulighed for at bladre mellem produktbilleder
- [x] Vis valgt billede som stort hovedbillede
- [x] Vis øvrige billeder som thumbnails

## 🛒 Kurv

- [x] Tilføj produkt til kurv
- [x] Ændr antal i kurven
- [x] Fjern produkt fra kurven
- [x] Cart sidebar
- [x] Neutralt grønt tema på kurvens sidebar med varekort, total og matchende knapper
- [x] Vis samlet pris
- [x] Neutralt grønt tema på kurvsiden med varetabel, total og matchende handlingsknapper
- [x] Større mellemrum mellem Gå til kurv og Checkout
- [x] Opdatér kurv-state ved logout og brugerskift
- [x] Kurv-badget følger den aktuelle bruger eller gæst
- [x] Kurvside, sidebar og badge opdateres ved kurvændringer
- [x] Forældede hentninger kan ikke overskrive den nye brugers kurv
- [x] Bevar gæstekurven separat

## 💳 Checkout

- [x] Checkout
- [x] Neutralt grønt tema på checkoutformular og ordresammendrag
- [x] Opret ordre fra kurven
- [x] Tøm kurven efter gennemført ordre
- [x] Ordrebekræftelse
- [x] Ordrebekræftelse fungerer for både gæster og brugere
- [x] Vis ordrenummer
- [x] Vis bestilte produkter
- [x] Vis antal og pris
- [x] Vis samlet pris
- [x] Vis ordredato

## 📋 Ordrer

- [x] Mine ordrer /orders
- [x] Kunden kan se sin ordrehistorik
- [x] Kunden kan åbne en specifik ordre
- [x] Vis ordrestatus
- [x] Vis ordredato
- [x] Vis samlet pris
- [x] Kun ejeren kan tilgå sin ordre
- [x] Ordrestatus: Placed → Confirmed → Shipped → Completed
- [x] Ens farvede status-badges i kunde- og adminvisninger
- [x] Status-badges på ordrebekræftelsen
- [x] Neutralt grønt tema på kundens ordredetaljer med matchende vare-, status- og leveringspaneler

## 🛠️ Admin – ordrebehandling

- [x] Admin-side med alle ordrer
- [x] Se kunden på ordren
- [x] Se ordrelinjer
- [x] Se samlet pris
- [x] Se ordredato
- [x] Åbn detaljer for en ordre
- [x] Ændr ordrestatus
- [x] Filtrer ordrer efter status
- [x] Neutralt grønt tema på admins ordredetaljer med ordrelinjer, total, kundeoplysninger og statuspanel

## 👥 Admin – kunder

### Kundeoversigt

- [x] Kundeoversigt for Admin
- [x] Vis alle registrerede kunder
- [x] Vis kundens navn
- [x] Vis kundens e-mail
- [x] Åbn en kunde fra kundeoversigten

### Kundedetaljer

- [x] Kundedetaljeside
- [x] Vis kundens fulde navn
- [x] Vis kundens e-mail
- [x] Vis kundens telefonnummer
- [x] Vis kundens kontostatus
- [x] Vis hvornår kunden blev oprettet
- [x] Neutralt grønt tema på admins kundedetaljer, kundeoplysninger og ordrehistorik

### Kundens ordrer

- [x] Vis kundens ordrehistorik
- [x] Vis ordrenummer
- [x] Vis ordredato
- [x] Vis ordrestatus
- [x] Vis samlet pris
- [x] Klik på ordre → åbn eksisterende Admin-ordredetaljeside

## 🔎 Admin – global søgning

- [x] Admin kan vælge mellem Produkter og Admin
- [x] Admin kombinerer kunde- og ordresøgning
- [x] Søg efter kunder på navn og e-mail
- [x] Søg efter ordrer på ordrenummer
- [x] Søg efter ordrer via kundenavn og kundens e-mail
- [x] Vis relevante resultater, mens Admin skriver
- [x] Vis tydeligt, om et søgeresultat er en kunde eller ordre
- [x] Klik på kunde → åbn kundedetaljesiden
- [x] Klik på ordre → åbn Admin-ordredetaljesiden
- [x] Enter åbner første relevante kunde/ordre
- [x] Behold almindelig produktsøgning i samme søgebar
- [x] Kun Admin kan vælge Admin-søgning
- [x] Almindelige kunder har kun produktsøgning
- [x] Friske kunde- og ordredata hentes ved valg af søgetype

## 🔐 Brugere og sikkerhed

- [x] Brugerregistrering
- [x] Login med JWT
- [x] Customer-rolle
- [x] Admin-rolle
- [x] Beskyttede API-endpoints

## 🧰 Admin – produkter

- [x] Aktivér/deaktiver produkt direkte fra produktlisten
- [x] Lagerbeholdning kan angives ved Create
- [x] Lagerbeholdning kan ændres ved Edit
- [x] Produktbilleder kan vælges/uploades ved Create og Edit
- [x] Vis tydelig status for aktivt/deaktiveret produkt
- [x] Vis lagerantal direkte på produktlisten
- [x] Filtrer mellem aktive, udsolgte og deaktiverede produkter
- [x] Admin kan redigere produktdata direkte fra produktdetaljesiden
- [x] Bekræftelsesdialog ved deaktivering
- [x] Permanent sletning af deaktiverede produkter
- [x] Advarselsdialog før permanent sletning
- [x] API afviser permanent sletning af aktive produkter
- [x] API afviser sletning af produkter med ordre-/kurvlinjer i databasen
- [x] Afsluttede ordrer beskytter også produktet mod sletning

Gæstekurve ligger lokalt i browseren og indgår ikke i API’ets slettekontrol. Uploadede billedfiler bevares ved produktsletning.

## 📊 Lagerstyring

- [x] Vis lagerstatus på produktsiden
- [x] Kontrollér lager ved checkout
- [x] Reducér lager, når en ordre gennemføres
- [x] Forhindr køb af flere varer end lagerbeholdningen
- [x] Vis Udsolgt, når lager = 0
- [x] Forhindr, at udsolgte produkter lægges i kurven

## 👤 Min konto

- [x] Side til Min konto
- [x] Vis navn
- [x] Vis e-mail
- [x] Rediger profiloplysninger
- [x] Log ud
- [x] Skift adgangskode
- [x] Glemt adgangskode
- [x] Kompakt profilformular med neutralt grønt tema
- [x] Samme tema på formularen til adgangskodeskift

## 📝 Registrering og validering

- [x] Brugervenlige valideringsbeskeder ved registrering
- [x] Vis forståelige passwordkrav ved ugyldigt password
- [x] Oversæt Identity/API-fejl til menneskelige fejlbeskeder
- [x] Undgå rå exception-/backend-beskeder ved registrering

## 🏠 Forside

- [x] Hero-sektion med Lisberg Threads-branding
- [x] Kort introduktionstekst
- [x] Primær Se produkter-knap
- [x] Vis 4–6 udvalgte eller nyeste produkter
- [x] Produktcards med billede, navn, pris og lagerstatus
- [x] Se alle produkter-knap
- [x] Sådan fungerer det-sektion
- [x] Vis flowet Find produkt → Kurv → Checkout
- [x] Kort brand-/introsektion om Lisberg Threads
- [x] Footer med relevante links
- [x] Footer-links til Produkter, Min konto og Mine ordrer
- [x] Kontakt-/infosektion i footer
- [x] Admin kan redigere teksten på Home/forsiden
- [x] Rettet Razor-kompileringsfejl på Home

## 🧵 Om Lisberg Threads

En selvstændig side, hvor besøgende kan følge håndarbejdet bag produkterne og lære skaberen bag Lisberg Threads at kende. Sidens indhold og personlige historie udarbejdes med brugerens kone.

### Side og navigation

- [x] Opret en selvstændig Om Lisberg Threads-side
- [x] Lad Om Lisberg Threads i hovednavigationen og footer linke til den nye side
- [x] Tilpas siden til det neutrale grønne tema og mobil/desktop
- [ ] Præsentér arbejdsprocesserne med tekst og billeder fra det faktiske arbejde
- [x] Link fra siden til produkterne
- [x] Admin kan redigere tekster direkte på Om-siden
- [x] Admin kan redigere sidetitel, overtekster, afsnits- og trinoverskrifter, billedtekster, afslutning og linktekster på Om-siden
- [x] Admin kan uploade, udskifte og fjerne et billede ved hvert procestrin
- [x] Vis tre redigerbare felter til syning og tre til strikning, hver med tekst og billedplads
- [x] Admin kan uploade op til to historiebilleder
- [x] API beskytter redigering og upload med Admin-rolle og gemmer indhold med versionskontrol

Siden findes på `/om-lisberg-threads`. Den bruger eksisterende `ContentTexts` og kræver ingen ny migration. Procesafsnittene har redigerbare standardtekster; personlige tekster og billeder fra det faktiske arbejde mangler fortsat. Upload accepterer JPG, PNG og WebP på højst 5 MB. Fjernelse fra siden sletter ikke billedfilen.

### Syning – fra idé til færdigt produkt

- [x] Vis valg og bestilling af stof og øvrige materialer
- [x] Fortæl om valg af opskrift eller symønster
- [x] Vis opmåling og tilpasning af mål
- [x] Vis udklipning af stoffet
- [x] Vis arbejdet ved symaskinen og samling af produktet
- [x] Vis afsluttende detaljer og det færdige produkt

### Strikning – fra idé til færdigt produkt

- [x] Vis valg og bestilling af garn og øvrige materialer
- [x] Fortæl om valg af strikkeopskrift
- [x] Vis strikkeprøve, mål og tilpasning
- [x] Vis strikkeprocessen undervejs
- [x] Vis samling, hæftning og færdiggørelse
- [x] Vis det færdige strikkede produkt

### Historien bag Lisberg Threads

Muligt indhold, som afklares med brugerens kone før udarbejdelse:

- [ ] Fortæl hvordan hun begyndte at lære at sy
- [ ] Fortæl om hendes første symaskine
- [ ] Vis udviklingen fra den første symaskine til det udstyr, hun bruger i dag
- [ ] Fortæl hvordan hun begyndte at strikke, og hvordan hendes håndarbejde har udviklet sig
- [ ] Indsaml og godkend personlige tekster og billeder til siden

## ✉️ Kontakt

En selvstændig kontaktside, hvor kunder kan sende en besked til Lisberg Threads via en formular. Henvendelser leveres til en aftalt kontaktmail og besvares via e-mail. Der skal ikke vises et offentligt telefonnummer eller tilbydes telefonkontakt.

### Side og formular

- [x] Opret en kontaktside i det neutrale grønne tema med layout til mobil/desktop
- [x] Lad Kontakt i hovednavigationen og footer linke til kontaktsiden
- [x] Gør formularen tilgængelig for både gæster og kunder med login
- [x] Tilføj felter til navn, e-mail, emne og besked
- [x] Tilføj valgfrit ordrenummer ved spørgsmål om en eksisterende ordre
- [x] Tilføj valgfrit produktnavn eller produktlink ved produktspørgsmål
- [x] Vis kun relevante ekstra felter ud fra det valgte emne
- [x] Forklar at svaret sendes til den angivne e-mailadresse
- [x] Vis eventuel forventet svartid, når den er aftalt med brugerens kone
- [x] Undlad telefonfelt, offentligt telefonnummer og Ring til os-links på kontaktsiden

### Forslag til emner

- [x] Spørgsmål om et produkt
- [x] Størrelse, mål og pasform
- [x] Materialer, vask og pleje
- [x] Bestilling og ordrestatus
- [x] Levering og forsendelse
- [x] Ændring af en bestilling
- [x] Returnering eller reklamation
- [x] Særlige ønsker og forespørgsler
- [x] Andet

Emnevalgene er hjælp til at sortere henvendelser. Særlige ønsker og ændring af bestillinger er forespørgsler, ikke et løfte om at kunne opfylde dem.

### Afsendelse og feedback

- [x] Aftal og konfigurér modtagerens kontaktmail og afsenderopsætning
- [x] Send beskeden som e-mail via API'et med en delt kontakt-DTO
- [x] Brug konfigureret afsender og kundens e-mail som Reply-To, så hun kan svare direkte
- [x] Validér obligatoriske felter, e-mail, emnevalg og længdegrænser i API'et og formularen
- [x] Tilføj beskyttelse mod spam og gentagne afsendelser
- [x] Vis afsendelsesstatus og forhindr dobbeltklik, mens beskeden sendes
- [x] Vis en ensartet succesbesked, når mailserveren har accepteret beskeden
- [x] Vis en forståelig dansk fejl ved afsendelsesproblemer, og bevar den indtastede besked

Siden findes på `/kontakt`. API'et bruger `POST /api/contact`; afsendelse kræver en gyldig SMTP-opsætning og `Contact:RecipientEmail`. `Contact:ResponseTime` er valgfri. Der er ingen ny databasetabel eller migration. Succes betyder, at mailserveren har accepteret beskeden, ikke at levering til indbakken er verificeret.

Spamkontrol består af et skjult felt samt højst tre afsendelsesforsøg pr. IP-adresse pr. ti minutter i den enkelte API-instans. Flere brugere bag samme IP deler grænsen. Formularen forhindrer gentagne klik under afsendelse og efter succes, indtil indholdet redigeres.

### Manuel opsætning før brug

- [x] Angiv `Contact:RecipientEmail` med den aftalte modtageradresse
- [x] Konfigurér `Smtp:Host`, `Smtp:Port`, `Smtp:EnableSsl`, `Smtp:FromAddress` og eventuelt `Smtp:UserName`/`Smtp:Password`
- [ ] Angiv eventuelt `Contact:ResponseTime`, når en svartid er aftalt
- [ ] Genstart API'et efter konfiguration, og test modtagelse og Reply-To

`appsettings.json` er ignoreret af Git. Kontaktindstillingerne er tilføjet lokalt, men skal også opsættes i andre miljøer; brug miljøvariabler (fx `Contact__RecipientEmail`) eller secrets til følsomme indstillinger. Gem ikke SMTP-adgangskoder i repositoryet. Lokalt er SMTP og modtager sat med `dotnet user-secrets` (Gmail på `smtp.gmail.com`, port 587, SSL, app-adgangskode). Brugeren har bekræftet, at afsendelse virker; Reply-To, spambegrænsning og fejlbesked er ikke bekræftet.

## 🎨 UX / Polish

### Login

- [x] Responsive loginfelter med passende max-bredde
- [x] Login med Enter

### Produktdetaljeside

- [x] Redesign produktdetaljesiden
- [x] Forbedr layout og visuel opdeling
- [x] Bedre spacing og alignment
- [x] Gør produktinformation mere overskuelig
- [x] Gør pris, lagerstatus og købsknap visuelt tydeligere
- [x] Tilpas produktdetaljesiden til billedgalleriet
- [x] Responsivt layout til mobil og desktop

### Create og Edit Product

- [x] Smalle formularer med maksimal bredde på 520 px
- [x] Sektioner til produktinformation og billeder
- [x] Kompakte felter til størrelse, pris og lager
- [x] Neutralt grønt tema, der matcher de øvrige produktsider
- [x] Danske overskrifter og knapper
- [x] Layout tilpasset mobil

### Produktoversigt

- [x] Lagerlabels med samme styling som Home
- [x] Grøn På lager, rødbrun Udsolgt og grå Ikke aktiv
- [x] Indlæsningsvisning med spinner
- [x] Empty state ved tomt sortiment
- [x] Empty state ved søgning/filter uden resultater
- [x] Fejlvisning med Prøv igen

### Ordre- og kundeoversigter

- [x] Indlæsningsvisninger med spinner
- [x] Empty state på Mine ordrer med Se produkter
- [x] Empty states på admins ordre- og kundeoversigter
- [x] Fejlvisninger med Prøv igen
- [x] Ens neutralt grønt tema
- [x] Finpuds admin-kundeoversigt, admin-ordreoversigt og Mine ordrer med grønne tabeller/kort, statusfilter og handlingsknapper

### Navigation

- [x] Hovednavigation: Lisberg Threads | Produkter | Om Lisberg Threads | Kontakt | Admin ▼
- [x] Fjern Mere-dropdownen
- [x] Behold Admin som dropdown til admin-funktioner

### Resterende generel polish

- [x] Ens success- og fejlvisninger på produktsiderne
- [x] Danske fejlbeskeder ved produktgem, aktivering og billed-upload uden rå exception-/API-tekst
- [x] Fejlhåndtering med Prøv igen på produktdetaljer og produktredigering
- [x] Ens feedback på kurv, sidebar, checkout, ordrestatus og kontosider
- [x] Danske kurv- og checkout-fejl uden rå API-tekst
- [x] Håndtér uklart checkout-resultat med advarsel og låst bestillingsknap i den aktuelle visning
- [x] Ens loading-, empty- og fejlvisninger på kurv, sidebar og checkout
- [x] Se produkter-knap ved tom kurv, inklusive sidebar og checkout
- [x] Ens loading- og fejlvisninger på kunde- og adminordredetaljer samt ordrebekræftelse
- [x] Ens loading-, empty- og fejlvisninger på admins kundedetaljer og kundens ordrehistorik
- [x] Gennemgå loading states på resterende sider
- [x] Gennemgå fejlbeskeder på resterende sider
- [x] Ens success-notifikationer på tværs af appen
- [x] Samlet gennemgang af mobilvisning
- [x] Ens knaphøjder og spacing på Products
- [x] Gennemgå empty states på resterende sider
- [x] 404-side
- [x] Unauthorized / 403-side

## 🏗️ Arkitektur

- [x] Blazor WebAssembly frontend
- [x] ASP.NET Core REST API
- [x] SQL Server
- [x] Entity Framework Core
- [x] DTO’er via LisbergThreads.Contracts

## 🧪 Testplan og manuel verifikation

Testplan, testcase-skabelon, manuelle testpunkter og resultater findes i [test-plan.md](test-plan.md). Implementeringsafkrydsninger i denne fil er ikke dokumentation for beståede tests.

Følg [den trinvise guide til resterende manuel verifikation](manuel-verifikation.md). Den samlede gennemgang af mobilvisning og spacing er meldt færdig af brugeren den 21. oktober 2026; de detaljerede funktionstests registreres fortsat særskilt.

## 🚀 Senere / Nice-to-have

- [ ] Kategorier
- [ ] Filtrering efter kategori
- [ ] Sortering efter pris/navn
- [ ] E-mail med ordrebekræftelse
- [ ] Admin dashboard
- [ ] Salgsstatistik
- [ ] Deployment
- [ ] CI/CD

## 💳 Eventuelt senere

- [ ] Betalingsløsning, fx Stripe
- [x] Betalingsstatus på ordre
- [ ] Kvittering

Betalingsstatus er implementeret som data og visning. Checkout markerer aktuelt ordren som `Paid` uden bekræftelse fra en betalingsudbyder; reel betalingshåndtering er ikke implementeret.

## 🎯 Foreslået rækkefølge herfra

- [x] 404- og 403-side
- [x] Ens success-notifikationer og resterende fejlbeskeder
- [x] Resterende loading- og empty states
- [x] Om Lisberg Threads-side med arbejdsprocesser for syning og strikning samt redigerbar historie
- [ ] Udfyld og godkend Om-sidens personlige tekster og billeder
- [x] Kontaktside med e-mailformular, emnevalg og uden telefonkontakt
- [x] Konfigurér kontaktmail og SMTP og verificér faktisk mailmodtagelse
- [x] Samlet gennemgang af mobilvisning og spacing
- [x] Telefonnummer på admins kundedetaljer, hvis det ønskes
- [ ] Resterende manuel verifikation — se [test-plan.md](test-plan.md)
- [ ] Deployment
