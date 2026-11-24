# Lisberg Threads – testplan

Testplan, testcases og manuel verifikation for Lisberg Threads. Implementeringsstatus findes i [feature-checklist.md](feature-checklist.md).

Start med [den trinvise guide til resterende manuel verifikation](manuel-verifikation.md). Guiden angiver rækkefølge, testdata og forventede resultater; resultater registreres her. Brugeren har den 21. oktober 2026 meldt den samlede gennemgang af mobilvisning og spacing færdig. Det markerer ikke automatisk de enkelte funktionstests nedenfor som beståede.

## 🧭 Testplan og arbejdsfordeling

Testarbejdet dokumenteres i denne fil og kobles til kravene i [feature-checklist.md](feature-checklist.md). En implementeret funktion er ikke det samme som en bestået test. Et bestået build registreres heller ikke som en bestået funktionstest.

### Formål og omfang

- [ ] Beskriv formålet med testforløbet og hvilke funktioner, der indgår
- [ ] Prioritér risici og testområder, især checkout, lager og adgangskontrol
- [ ] Beskriv testmiljø, version/commit, testbrugere og testdata
- [ ] Aftal kriterier for at starte og afslutte testforløbet samt for deployment

### Testområder og anvendt teori

Planen tager udgangspunkt i Lisberg Threads' krav og risici. Teorien fra [Software Quality E2026, lektion 1–5](https://github.com/arturomorarioja-ek/SD_Software_Quality_E2026) bruges til at vælge og begrunde tests. Lektionsoversigterne er gennemgået den 15. oktober 2026; slides på Itslearning er ikke læst. Vi løser ikke undervisningens øvelser og kræver ikke, at hvert pensumemne får sin egen projektopgave.

| Testområde | Hvad vi skal undersøge | Teori vi anvender |
|---|---|---|
| Lager og kurv | Positive antal, eksisterende kurvantal, udsolgte/deaktiverede produkter og gentagne produktlinjer | Ækvivalensklasser, grænseværdianalyse, positive/negative tests |
| Checkout og database | Ordre og lager gemmes samlet; afviste og samtidige køb giver ikke delvise ændringer eller negativt lager | Integrationstest med reel SQL Server-testdatabase |
| Adgangskontrol | Gæst, Customer og Admin får korrekt adgang; kunden kan kun se egne ordrer | Positive/negative API-tests og systemtests |
| Produkter og ordrebehandling | CRUD-begrænsninger, kontrakter og gyldige/ugyldige statusskift | API-test, testdata valgt ud fra krav, relevante beslutningsgrene |
| Brugerflade og købsforløb | Navigation, mobilvisning, feedback og et gennemført køb som gæst og bruger | Systemtest og accepttest |
| Mindre afgrænset logik | En konkret eksisterende adfærd kan testes hurtigt og uafhængigt | Unit tests, AAA og parameteriserede tests; doubles kun ved et begrundet behov |
| Kodekvalitet og ændringer | Relevante analysefund og risiko for følgefejl efter rettelser | Statisk analyse, white-box-design og regressionstest |

### Lager og kurv – testdesign

- [ ] Brugeren vælger testdata ud fra gyldige/ugyldige antal og lagerets grænser
- [ ] Udbyg testdata med varer, der allerede ligger i kurven, samt gentagne produktlinjer
- [ ] Dokumentér forventet resultat for udsolgte og deaktiverede produkter
- [ ] Udfør relevante kontroller både gennem brugerfladen og direkte mod API'et

### Checkout – integration og købsforløb

- [ ] Test checkout som gæst og bruger med separate testdata
- [ ] Kontrollér ordre, ordrelinjer, priser, total og lagerændring efter et gennemført køb
- [ ] Kontrollér at afvist checkout ikke efterlader en gennemført ordre eller reduceret lager
- [ ] Test samtidige køb mod reel SQL Server-testdatabase
- [ ] Test forbindelsesfejl og uklart checkout-resultat uden at gentage en muligvis gennemført bestilling

### Adgangskontrol og API-kontrakter

- [ ] Brugeren designer positive og negative API-testcases for Admin, Customer og anonym adgang
- [ ] Kontrollér ejerskab af ordrer og adgang til gæsteordrer med/uden korrekt bekræftelsestoken
- [ ] Kontrollér statuskoder og response-data ud fra projektets kontrakter
- [ ] Organisér API-requests i en collection med miljøvariabler og gentagelige testdata

### Produkter og ordrebehandling

- [ ] Test produktoprettelse, redigering, aktivering, deaktivering og slettebegrænsninger
- [ ] Test gyldige statusskift og afvisning af ugyldige skift
- [ ] Test samtidige ændringer med forældet produktversion
- [ ] Kontrollér at ændringer vises korrekt i kunde- og adminvisninger

### Unit tests og kodekvalitet

- [ ] Vælg sammen en eksisterende afgrænset adfærd, hvor en unit test giver værdi
- [ ] Lav én eksempeltest sammen; brugeren skriver efterfølgende testcases
- [ ] Brug AAA, relevante parameteriserede data og uafhængigt setup/teardown
- [ ] Begrund eventuelle test doubles ud fra afhængighederne og den valgte teststil
- [ ] Test adfærd gennem tilgængelige grænseflader; eksponér ikke private detaljer kun for tests
- [ ] Udvælg relevante kodegrene og registrér faktisk målt coverage, hvis automatiske tests implementeres
- [ ] Gennemgå relevante statiske analysefund og registrér konkrete problemer

Automatiske tests og et testprojekt oprettes først, når brugeren konkret beder om implementeringen. Der er ikke oprettet eller kørt tests som del af planlægningen. Coverage er ikke målt og bruges ikke alene som bevis for korrekthed.

### Classic og London – valg og sammenligning

Vi skal arbejde med begge tilgange og begrunde valget i de konkrete testcases. Classic (klassisk/Detroit) fokuserer typisk på resultat og tilstand og bruger gerne rigtige samarbejdende objekter. London (mockist) isolerer typisk den testede klasse med mocks og kontrollerer relevante kald til dens afhængigheder. Classic udelukker ikke test doubles, og London betyder ikke, at alle interne metodekald skal kontrolleres.

- [ ] Vælg sammen en eksisterende adfærd med en relevant afhængighed, som egner sig til at sammenligne begge tilgange; undersøg den faktiske kode før valget
- [ ] Skriv én eksempeltest sammen med Classic: kontrollér det observerbare resultat med rigtige objekter og kun nødvendige test doubles
- [ ] Skriv én eksempeltest sammen af samme adfærd med London: brug mocks til afhængigheder og kontrollér de kald og argumenter, som er en del af adfærdens krav
- [ ] Sammenlign læsbarhed, opsætning, feedback ved fejl og følsomhed over for interne kodeændringer
- [ ] Begrund hvilke afhængigheder der erstattes, og hvorfor; undgå rigtige mails og eksterne netværkskald i unit tests
- [ ] Brugeren skriver efterfølgende egne testcases og begrunder valg af tilgang; assistenten giver feedback
- [ ] Anvend ækvivalenspartitioner og grænseværdier, hvor den valgte adfærd har relevante inputgrænser; dokumentér testdata og forventede resultater

Afgrænsede regler som gyldige ordrestatusskift er mulige kandidater til Classic. Samarbejdet med en mailafsender er en mulig kandidat til sammenligningen, hvis den eksisterende kode egner sig til det. Vi indfører ikke ekstra arkitekturlag alene for at demonstrere en teststil. Lager, databasetransaktioner og samtidige køb verificeres fortsat med integrationstests mod reel SQL Server; mocks kan ikke dokumentere disse egenskaber.

### Playwright og SonarQube – gratis lokale værktøjer

Playwright bruges til browserforløb og SonarQube Community Build til statisk kodeanalyse. Postman bruges fortsat til direkte API-tests, og unit tests dækker udvalgte afgrænsede regler. Se også [værktøjsforløbet i den manuelle guide](manuel-verifikation.md#planlagte-værktøjer-playwright-og-sonarqube).

- [ ] Undersøg lokal kompatibilitet og ressourcer; vælg versioner og dokumentér opsætningen, før værktøjerne tages i brug
- [ ] Klargør gratis lokal Playwright, og lav ét browserforløb sammen med codegen og eksplicitte kontroller af forventede resultater
- [ ] Udvælg gentagelige forløb til automatisering: login, søgning, kurv, checkout og adgangskontrol med separate browserkontekster og kontrollerede testdata
- [ ] Kør relevante browserforløb ved desktop- og mobilbredder; registrér MV-ID, version, rapport og fejlspor samt eventuelle resterende manuelle kontroller
- [ ] Klargør gratis lokal SonarQube Community Build og SonarScanner for .NET; analysér API, klient og Contracts gennem solution-build
- [ ] Gennemgå analysefund sammen: registrér konkrete fejl, sikkerhedsrelaterede fund og vedligeholdelsesproblemer; begrund afviste fund og prioritér rettelser
- [ ] Gentag analysen efter rettelser, og aftal relevante kvalitetskriterier ud fra projektet og de tilgængelige regler
- [ ] Importér en kompatibel coverage-rapport, når unit tests og coverage-indsamling er implementeret; skeln mellem manglende rapport og faktisk målt lav coverage

Playwright er [Apache 2.0-licenseret](https://github.com/microsoft/playwright), og [SonarQube Community Build er gratis](https://www.sonarsource.com/products/sonarqube/downloads/). [SonarScanner for .NET](https://docs.sonarsource.com/sonarqube-community-build/analyzing-source-code/dotnet-environments/getting-started-with-net) bruges sammen med buildet til .NET-analysen. Oplysningerne er kontrolleret den 21. oktober 2026. Der planlægges lokal drift uden betalte abonnementer; maskinressourcer og vedligeholdelse skal stadig afklares. Ingen installation, analyse eller testkørsel er udført som del af denne planændring.

SonarQube-fund er statisk analyse, ikke beståede funktionstests. Automatiserede Playwright-forløb registreres som systemtests, mens brugerens visuelle og øvrige manuelle kontroller registreres særskilt. Reel SQL Server-verifikation af lager og transaktioner er fortsat nødvendig.

### Brugerflade, regression og deployment

- [ ] Udfør de relevante testcases fra Manuel verifikation, inklusive mobil og desktop
- [ ] Kobl hvert gennemført forløb til krav og forventet brugeradfærd
- [ ] Gentest fejlrettelser og kør en udvalgt regressionstestpakke
- [ ] Vurdér resultaterne mod de aftalte kriterier før produktion
- [ ] Planlæg senere automatiseret regression i CI/continuous testing, hvis det giver værdi
- [ ] Kør og dokumentér en kort smoke test efter deployment

### Foreslået arbejdsrækkefølge

1. Aftal testmiljø, testdata og kriterier for færdiggørelse.
2. Brugeren designer lager- og kurvtestcases; vi gennemgår dem sammen før udførelse.
3. Test adgangskontrol og relevante API-kontrakter.
4. Test checkout, databaseændringer og samtidige køb.
5. Supplér med udvalgte unit tests, sammenlign Classic og London som beskrevet ovenfor, og gennemgå statisk analyse, når implementering af tests er aftalt.
6. Klargør og brug Playwright til udvalgte browserforløb samt SonarQube Community Build til statisk analyse; gennemfør resterende manuelle kontroller, gentest og regression.
7. Vurdér deployment og kontrollér det deployede miljø.

De detaljerede resultater registreres i testcase-skabelonen nedenfor. Teori angives ved den konkrete testcase som begrundelse for testvalget. Tokens og adgangskoder gemmes ikke i checklisten eller committed testmiljøfiler.

### Arbejdsfordeling

Brugeren skal selv have mulighed for at designe og udføre tests. Assistenten hjælper med struktur, forklaring af teorien og feedback.

- [ ] Udarbejd én eksempeltestcase sammen
- [ ] Vælg testområder, som brugeren selv skriver testcases til
- [ ] Brugeren udarbejder testcases; assistenten giver feedback
- [ ] Brugeren udfører tests og registrerer faktiske resultater
- [ ] Gennemgå fundne fejl og behov for gentest sammen

## 📝 Testcases og kobling til krav

De eksisterende punkter under Manuel verifikation er et overblik over testområder. De udbygges med konkrete testcases her, efterhånden som vi arbejder med undervisningens teori.

### Skabelon til en testcase

Kopiér skabelonen for hver testcase. Dette er en tom skabelon, ikke en udført test.

| Felt | Indhold |
|---|---|
| Testcase-ID og titel | Udfyldes |
| Krav / punkt i featurelisten | Udfyldes |
| Testniveau og testteknik | Udfyldes med begrundelse og henvisning til undervisningen |
| Prioritet / risiko | Udfyldes |
| Forudsætninger og testdata | Udfyldes |
| Testtrin | Udfyldes i rækkefølge |
| Forventet resultat | Udfyldes før testen udføres |
| Faktisk resultat og dokumentation | Udfyldes efter udførelse |
| Status | Ikke kørt / Bestået / Fejlet / Blokeret |
| Miljø, version/commit, dato og udført af | Udfyldes |
| Fejlreference og gentest | Udfyldes ved behov |

## 🧪 Manuel verifikation

### Kontakt

- [ ] Åbn /kontakt som gæst og bruger: samme formular uden telefonfelt eller telefonnummer
- [ ] Vælg alle ni emner: ordrenummer/produktfelt vises kun ved relevante emner, og skjulte værdier sendes ikke videre i mailen
- [ ] Test tomme felter, ugyldig e-mail, ukendt emne og længdegrænser i både UI og direkte mod POST /api/contact
- [ ] Konfigurér testmodtager og SMTP: succes vises først efter accepteret afsendelse; kontrollér modtagelse, emne, indhold og Reply-To
- [ ] Test dobbeltklik og et nyt klik efter succes: én afsendelse, indtil indholdet ændres
- [ ] Send fire forespørgsler fra samme IP inden for ti minutter: den fjerde afvises med 429, og formularteksten bevares
- [ ] Udfyld spamfeltet Website direkte i request: afvisning uden mailafsendelse
- [ ] Test manglende SMTP-konfiguration, forkert login, timeout og afbrudt API-forbindelse: ingen falsk succes; teksten bevares
- [ ] Kontrollér valgfri svartid, menu/footerlinks samt formular og tastaturbetjening på mobil og desktop
- [ ] Gentest glemt adgangskode: den eksisterende mailfunktion er fortsat uafhængig af kontaktmodtageren

### Om Lisberg Threads

- [ ] Kontrollér præcis tre felter til syning og tre til strikning; redigér tekst og billede, og kontrollér efter genindlæsning
- [ ] Redigér sidetitel, overtekster, historieoverskrifter, proces- og trinoverskrifter som Admin: genindlæs og kontrollér de gemte tekster
- [ ] Upload et billede og redigér billedteksten: kontrollér visning og billedets alt-tekst efter genindlæsning
- [ ] Redigér afslutningen og linktekster via Rediger sidens linktekster: links bevarer deres korrekte destinationer
- [ ] Åbn /om-lisberg-threads som gæst og Customer: se historie og procesafsnit uden redigerings-/uploadkontroller
- [ ] Åbn siden som Admin: redigér en tekst, genindlæs og kontrollér at den er gemt
- [ ] Upload to historiebilleder og billeder til sy- og strikketrin: kontrollér placering og visning efter genindlæsning
- [ ] Udskift og fjern et billede: ændringen bevares efter genindlæsning; andre billedpladser er uændrede
- [ ] Upload ugyldig fil eller over 5 MB: dansk fejl og eksisterende billede bevares; kontrollér også serverafvisning direkte via API
- [ ] Kald PUT /api/content/about/{key} og POST /api/uploads/content-image som Customer og anonym: afvises
- [ ] Gem samme tekst/billedplads fra to browserfaner med forældet version: konfliktbesked, og den seneste gemte ændring overskrives ikke
- [ ] Afbryd forbindelse under hentning/upload/gem: forståelig fejl og mulighed for genindlæsning uden falsk succes
- [ ] Kontrollér menu-, forside- og footerlinks samt layout og billed-upload på mobil/desktop
- [ ] Gentest produkt-upload efter fælles mappevalg og serverens 5 MB-grænse

### Afsluttende feedback- og fejlgennemgang

- [ ] Test login med forkert password, serverfejl og tomt/ulæseligt succes-svar: passende danske beskeder, ikke misvisende passwordfejl
- [ ] Simulér fejl i browserlagring under login, profilgem, adgangskodeskift, nulstilling og logout: dansk fejl, ingen falsk succes
- [ ] Afbryd hentning af kurvantal: badge viser ? frem for et misvisende 0; efter genetablering og kurvopdatering vises korrekt antal
- [ ] Simulér fejl i sidebarens JavaScript: kurven/den valgte destination kan åbnes som almindelig side
- [ ] Test ordrebekræftelse ved adgangsfejl, timeout og ulæseligt svar samt navigation mellem ordrer: relevant fejl og ingen forældet ordre
- [ ] Kontrollér dansk opstarts-/global fejlvisning og Genindlæs-knap; fejl under checkout skal ikke opfordre til at bestille igen
- [ ] Gentest almindelige success-forløb på produkt-, kurv-, ordre- og kontosider

Kurvskift er bekræftet af dig. Du har også oplyst, at din kone har gennemgået checkout, lager og adgangskontrol; den detaljerede testliste nedenfor er fortsat åben til senere gennemgang.

### Navigation

- [ ] Test navigation og søgning ved 320, 375, 768, 1024 og 1600 px: links, søgefelt og konto/kurv må ikke overlappe
- [ ] Test produktsøgning og Admin-søgning på mobil: resultater skal kunne læses, scrolles og vælges
- [ ] Test Om Lisberg Threads og Kontakt fra forsiden og en anden side: links åbner henholdsvis /om-lisberg-threads og /kontakt
- [ ] Kontrollér rækkefølgen i hovednavigationen og at Mere-dropdownen er fjernet
- [ ] Kontrollér at Admin-dropdownen kun vises for Admin, og at dens links fungerer
- [ ] Test hovednavigationen på mobil og desktop

### Checkout og ordrer

- [ ] Test checkouttema på mobil og desktop som gæst og bruger: felter, validering, ordresammendrag, total og gennemført bestilling
- [ ] Test checkout på mobil med lange produktnavne og priser: ordrelinjer og knapper ombrydes inden for sidens bredde
- [ ] Test spinner og fejlvisning på checkout, ordredetaljer og ordrebekræftelse med langsom eller afbrudt forbindelse
- [ ] Test Prøv igen på ordrebekræftelse: genhenter kun ordren og opretter ikke en ny ordre
- [ ] Afbryd forbindelse under checkout: dansk advarsel om uklart resultat, og bestillingsknappen låses i den aktuelle visning
- [x] Afvis checkout ved reduceret lager: lagerfejl uden ny gennemført ordre eller lagertræk; efter rettelse af antal kan kunden gennemføre købet — MV-06-A/B
- [ ] Test øvrige ændringer af kurven under checkout
- [ ] Kontrollér tomme leveringsfelter og ugyldig e-mail: danske valideringsbeskeder
- [ ] Test gæsteordre med utilgængelig localStorage: ordrebekræftelse vises med advarsel om, at kurven ikke er tømt
- [ ] Test indlæsningsfejl på kundens og admins ordredetaljer: dansk fejl og Prøv igen
- [x] Dokumentér test af checkout som gæst og bruger — MV-04-A og MV-05-A
- [x] Dokumentér test af ordreoprettelse — korrekt total, tømt kurv og lagertræk; kundens ordre findes i Mine ordrer
- [ ] Dokumentér test af lagerstyring
- [x] Kontrollér, at en bruger kun kan se egne ordrer — MV-07-A/B; afvisning af anden kunde logger ikke kunden ud
- [ ] Test farvede ordrestatus-badges
- [ ] Test kundens ordredetaljer på mobil og desktop: grønne paneler, lange produktnavne, total, levering og tilbageknap

### Produkter

- [ ] Test produktknapper og antal-felter på mobil og desktop: ens minimumshøjde og ingen overlap ved adminhandlinger
- [ ] Test indlæsning og fejl på produktredigering og produktdetaljer: fælles spinner/fejlvisning og Prøv igen
- [ ] Test Product CRUD
- [ ] Test produktsøgning
- [ ] Test flere produktbilleder
- [ ] Test produktbilledgalleri
- [ ] Test Create/Edit-layout på mobil og desktop
- [ ] Test bekræftelsesdialog ved deaktivering
- [ ] Test permanent sletning af ubrugt, deaktiveret produkt
- [ ] Test afvisning af sletning ved ordre-/kurvlinjer
- [ ] Test lagerlabels
- [ ] Test loading-, empty- og fejlvisninger samt Prøv igen
- [ ] Opret og rediger produkt: produktlisten viser en grøn bekræftelse efter gem
- [ ] Aktivér, deaktivér og slet produkt: succes vises grønt, afvisninger vises som danske fejl
- [ ] Tilføj som gæst og bruger: grøn bekræftelse og opdateret kurv; overskrid samlet lager inkl. kurv og kontrollér dansk fejl
- [ ] Afbryd API-forbindelsen på produktdetaljer og redigering: fejl vises; genetablér forbindelsen og brug Prøv igen
- [ ] Gem med forældet produktversion: dansk konfliktbesked og bevarede formularrettelser
- [ ] Upload ugyldig fil eller billede over 5 MB: forståelig fejl uden rå servertekst

### Admin

- [ ] Test kundedetaljer med langsom forbindelse, indlæsningsfejl og kunde uden ordrer: ens visninger og fungerende Prøv igen
- [ ] Skift ordrestatus: grøn bekræftelse; ugyldigt statusskift og forbindelsesfejl giver dansk fejl og mulighed for at hente ordren igen
- [ ] Test Admin-søgning efter kunder
- [ ] Test Admin-søgning efter ordrer
- [ ] Test kundedetaljesiden
- [ ] Test kundedetaljernes grønne tema på mobil og desktop: kontaktoplysninger, aktiv/deaktiveret status, ordrehistorik og navigation
- [ ] Test kundens ordrehistorik
- [ ] Test nye statusvisninger på ordre- og kundeoversigter
- [ ] Test finpudsede kunde- og ordreoversigter på mobil og desktop: grøn styling, læsbare tabeller, vandret scrolling og fungerende detaljelinks
- [ ] Test ordrestatusfilter efter temaændringen: korrekt filtrering og tom-visning
- [ ] Test admins ordredetaljer på mobil og desktop: grønne paneler, lange produktnavne, statusfelt, Opdater status og tilbageknap

### Authorization

- [ ] Test ukendt adresse: dansk 404-visning med fungerende links til forsiden og produkter
- [ ] Test beskyttet side uden login: loginvisning med fungerende login-knap
- [ ] Test Admin-side som Customer: 403-visning uden adgang til Admin-indhold
- [ ] Test 404-, 403- og loginvisninger på mobil og desktop
- [ ] Test authorization
- [ ] Kontrollér, at Customer ikke kan bruge Admin-søgning
- [ ] Kontrollér, at Customer ikke kan tilgå kundeoversigten
- [ ] Kontrollér, at Customer ikke kan tilgå kundedetaljer
- [ ] Kontrollér, at Customer ikke kan kalde Admin-endpoints
- [ ] Kontrollér, at Customer ikke kan slette produkter permanent

### Kurv

- [ ] Test kurvsidens grønne tema på mobil og desktop: antal, fjernelse, total og navigation til checkout
- [ ] Test sidebarens grønne tema på mobil og desktop: varekort, antal-knapper, fjernelse, total, navigation og tastaturbetjening af produktbillede
- [ ] Test kurvens tabel på mobil: vandret scrolling skal være inde i tabellen, og antal/fjern/checkout skal være tilgængelige
- [ ] Test tom kurv som gæst og bruger på kurvside, sidebar og checkout: Se produkter åbner produktlisten, og sidebar lukkes
- [ ] Test nye loading-, empty- og fejlvisninger på mobil og desktop
- [ ] Ændr antal og fjern vare i kurv og sidebar: ens grønne bekræftelser; lager- og forbindelsesfejl vises på dansk
- [ ] Afbryd kurvhentning og genetablér forbindelse: Prøv igen henter kurven korrekt
- [x] Test kurv-state ved logout
- [x] Test korrekt kurv ved brugerskift
- [x] Bekræft, at kurv-badget følger bruger/gæst

### Min konto

- [ ] Test fælles indlæsnings- og fejlvisning med Prøv igen samt formularlayout på mobil
- [ ] Kontrollér ens success- og fejlvisninger ved profilgem, login, registrering, adgangskodeskift og nulstilling
- [ ] Test login med forkert password og med serverfejl: forskellige, forståelige beskeder
- [ ] Test nyt layout på mobil og desktop
- [ ] Test profilredigering efter temaændringen
- [ ] Test adgangskodeskift efter temaændringen

## 🐞 Testresultater, fejl og gentest

### Brugerbekræftede manuelle resultater — registreret 21. oktober 2026

Resultaterne nedenfor er udført og bekræftet af brugeren i samtalen. Assistenten har ikke selv udført browserforløbene. ID'erne angiver afgrænsede deltests af forløbene i [manuel-verifikation.md](manuel-verifikation.md), ikke godkendelse af hele forløbet. Præcist commit, browser og miljøversion er ikke oplyst for kørslerne.

| Deltest | Bekræftet resultat | Status |
|---|---|---|
| MV-03-A – kurv og lagergrænse | Ved pris 100 og lager 5 kunne 2 + 3 stk. tilføjes; yderligere 1 blev afvist. Total var 500. Efter genindlæsning hos Admin var lager stadig 5. | Bestået |
| MV-05-A – gæstekøb | Køb af 2 stk. gav total 200, tømte kurven og reducerede lager fra 5 til 3. | Bestået |
| MV-04-A – kundekøb | Køb af 2 stk. gav total 200 og lager 3; brugeren godkendte forløbet, inklusive tømt kurv. Ordren fandtes i Mine ordrer. | Bestået |
| MV-06-A – lager reduceret før checkout | Efter kurvtilføjelse af 2 stk. blev lager sænket til 1. Checkout gav lagerfejl; lager forblev 1, og ingen ny gennemført ordre blev oprettet. | Bestået |
| MV-06-B – køb efter rettelse | Efter rettelse til 1 stk. godkendte brugeren købsforløbet med total 100, tømt kurv og lager 0. Produktet viste Udsolgt og blokerede tilføjelse. | Bestået |
| Prisændring efter kurvtilføjelse | Kunden lagde bukser til 500 kr. i kurven, og Admin ændrede prisen til 5 kr. En afprøvet ændring, der brugte den nye pris ved checkout, blev fjernet, fordi kunden ellers kunne betale en anden pris end den viste. Besluttet regel: kurvens gemte pris bruges ved checkout. | Besluttet – ikke gentestet efter fjernelsen |
| MV-07-A – kundens ordre uden login | Kundens ordreadresse krævede login og viste ikke ordreoplysninger i privat browservindue. | Bestået |
| MV-07-B – anden kundes adgang og gentest | Efter API-rettelsen blev ordren afvist med Du har ikke adgang til denne ordre. Den anden kunde kunne derefter åbne Min konto uden nyt login. | Bestået efter rettelse |
| MV-07-C – gyldige ordrestatusskift | Placed → Confirmed → Shipped → Completed blev gennemført som Admin og vist korrekt efter genindlæsning hos kunden. | Bestået |

### Brugerbekræftede manuelle resultater — registreret 26. oktober 2026

Brugeren har udført forløbene og bekræftet resultaterne i samtalen. Assistenten har ikke udført browserforløbene. Præcist commit, browser og miljøversion er ikke oplyst.

| Deltest | Bekræftet resultat | Status |
|---|---|---|
| MV-06-C – produkt deaktiveret før checkout som kunde | Bestillingen blev afvist efter deaktivering af produktet, og brugeren bekræftede, at der ikke blev oprettet nye ordrer. Brugeren bekræftede også, at lageret fortsat var 5. | Bestået |
| MV-06-D – produkt deaktiveret før checkout som gæst | Bestillingen blev afvist efter deaktivering af produktet, og brugeren bekræftede, at der ikke blev oprettet nye ordrer. Brugeren bekræftede også, at lageret fortsat var 5. | Bestået |
| MV-06-E – to køb mod lager 1 i separate browserprofiler | Brugeren bekræftede én succes, én afvisning, én ny ordre og slutlager 0 efter det beskrevne testforløb. Brugeren anslår cirka 2 sekunder mellem klikkene; første checkout kan derfor være afsluttet før det andet. Overlapning af serverkald og særskilt SQL-kontrol af lager, ordrer og ordrelinjer er ikke dokumenteret. Brugeren har valgt at gå videre uden yderligere samtidighedstest nu. | Observeret browserresultat bestået; yderligere samtidighedstest fravalgt nu; databasekontrol åben |

### Yderligere brugerbekræftet API-resultat

| Deltest | Bekræftet resultat | Status |
|---|---|---|
| MV-07-D – ugyldigt statusskift Shipped → Confirmed via Postman | Brugeren modtog beskeden: Ordrestatus kan ikke ændres fra Shipped til Confirmed. Brugeren bekræftede efter genindlæsning, at ordren stadig var Shipped. HTTP-statuskoden er ikke særskilt bekræftet. Dato/commit for kørslen er ikke oplyst. | Afvisning og uændret ordrestatus bestået; HTTP-statuskontrol åben |
| MV-07-E – ugyldigt statusskift Completed → Confirmed | Brugeren bekræfter at have udført den beskrevne test tidligere og godkender resultatet. Registreret på baggrund af brugerens bekræftelse; ikke gentaget eller udført af assistenten. Præcis HTTP-statuskode, testdato og commit er ikke særskilt oplyst. | Bestået – brugerbekræftet tidligere test |

### Assistentudførte API-kontroller — 2. november 2026

Udført mod det kørende lokale API på `https://localhost:7013` med enkeltstående Python HTTP-kald og det lokale ASP.NET-udviklingscertifikat. Gyldige Admin- og Customer-tokens blev leveret af brugeren; begge blev først kontrolleret med `GET /api/auth/me` (200). Tokens og personoplysninger fra svar er ikke gemt i dokumentationen. Arbejdstræets commit ved start var `c839bf0c09e0310b331713d0c642d721859a0831`; den kørende API-proces er ikke særskilt knyttet til et build-commit. Ingen direkte SQL-kontroller eller browserforløb er udført. Der er ikke tilføjet et automatisk testprojekt.

| Deltest | Faktisk resultat | Status |
|---|---|---|
| MV-11-A – gentagne produktlinjer som gæst | To linjer med samme aktive produkt og antal 3 + 3 ved lager 5 gav HTTP 400 med produktets lagerfejl. Efterfølgende produkthentning viste lager 5, og Admin-ordreoversigten havde samme sæt ordre-ID'er som før kaldet. | Bestået via API. |
| MV-11-C – gentagne produktlinjer som Customer | Testvaren blev lagt i kundens gemte kurv med antal 6 ved lager 6; Admin sænkede derefter lageret til 5. Checkout med linjerne 3 + 3 (samt eventuelle oprindelige kurvlinjer) gav HTTP 400 med testproduktets lagerfejl, uændret lager 5 og ingen ny gennemført ordre i Admin-oversigten. Testkurvlinjen havde stadig antal 6 efter afvisningen. Dermed blev lagerkontrollen nået frem for blot en 409 ved forskel mellem request og gemt kurv. | Bestået via API. |
| MV-11-B – anonym mod Admin-oversigter | `GET /api/orders`, `/api/customers` og `/api/products/all` gav HTTP 401 uden data fra oversigterne. | Bestået for de tre GET-endpoints. |
| MV-07-F – gæsteordreadgang | Uden token og med forkert token: HTTP 401 uden ordredata. Korrekt token: HTTP 200 med korrekt ordre-ID, total 100 og status Placed. Customer uden token: HTTP 403 uden ordredata. Admin uden token: HTTP 200 med korrekt ordre. | Bestået via API. |
| MV-11-D – Customer mod Admin | De tre ovenstående oversigter gav HTTP 403 uden Admin-data som Customer og HTTP 200 som Admin. Customer blev også afvist med 403 ved ordrestatusændring og produktlagerændring; lager forblev 4. `GET /api/auth/me` gav fortsat 200 bagefter. | Bestået for de fem kontrollerede endpoints. Klientens loginadfærd er ikke browserafprøvet her. |
| MV-07-G – Placed → Shipped | Admin-kald gav HTTP 400 og »Ordrestatus kan ikke ændres fra Placed til Shipped.« Efterfølgende hentning viste stadig Placed. | Bestået via API. |
| MV-03-B – oprydning af kundekurv | Kun den tilføjede testkurvlinje blev fjernet. Oprindelige produkt-ID'er, antal, enhedspriser, linjetotaler og kurvens total blev sammenlignet før/efter og var ens. | Bestået via API. |
| Valutavisning efter rettelse | Koden bruger `da-DK`, `kr.`, indlæsning af globaliseringsdata og valutaformatering i kurven. Browserstyringen havde ingen tilgængelige browsere, heller ikke efter brugerens gentagne browserlogin. | Kode gennemgået; browserverifikation fortsat blokeret. |

**Testdata og begrænsninger:** Kørslen oprettede to særskilte testprodukter; begge blev deaktiveret bagefter. Første forsøg blev afbrudt af testscriptets forkerte DTO-feltnavn `id` (kontrakten bruger `orderId`), uden gennemført checkout; dets testkurvlinje blev fjernet. Et efterfølgende stop skyldtes, at scriptet forventede et helt tomt 401-svar frem for API'ets standardfejl; kontrollen blev rettet til at undersøge fravær af ordredata og kørt igen. Dette var fejl i verifikationsscriptet, ikke konstaterede applikationsfejl.

To gennemførte gæstetestordrer blev oprettet til adgangskontrollerne: `b612af10-41df-44b4-bb51-b8bfeac9a1a1` og `819e9895-5241-4f36-af2d-4e92d533e184`. Begge er tydeligt mærket Verifikation og har total 100; de repræsenterer ingen faktisk betaling. Første ordre blev brugt til negative tokenkontroller, Admin/Customer-adgang og det ugyldige statusskift. Anden ordre blev brugt til korrekt bekræftelsestoken, da tokenet fra første checkout ikke var bevaret efter scriptets stop. Det anvendte produkts lager gik 5 → 4 → 3 ved disse to positive testkøb. Ordrerne er bevaret som testdata; testproduktet er deaktiveret. Kontroller af afviste køb sammenlignede lager og gennemførte ordrer via API; fravær af eventuelle løse ordrelinjer er ikke verificeret direkte i SQL Server. Samtidighed og andre endpoints er ikke dækket af denne kørsel.

Kun testdokumentationen er ændret; build er derfor ikke kørt. Punkt 1 og de udvalgte API-kontroller i punkt 2 er afsluttet. Punkt 3 kræver fortsat en browserkontrol af valutavisningen; øvrige åbne testområder er ikke automatisk godkendt.

### Registrerede fejl og begrænsninger

- **Valutavisning:** Kurven viste `$500.00` ved en korrekt beregnet total på 500. Klientens formatering er rettet til dansk kultur og `kr.`. Solution-build bestod med 0 fejl og 0 advarsler, men den ændrede valutavisning er ikke udtrykkeligt bekræftet i browseren; gentest er fortsat åben.
- **Adgang til anden kundes ordre:** API'et returnerede 401, som klienten behandlede som udløbet session. Rettet til 403 for en indlogget bruger uden ordreadgang. Build bestod med 0 fejl og 0 advarsler, og brugeren bekræftede efterfølgende korrekt afvisning samt bevaret login (MV-07-B).
- Den samlede lagerverifikation er fortsat åben: checkout med deaktiverede produkter er bekræftet afvist som både kunde og gæst uden nye ordrer og med uændret lager på 5 (MV-06-C/D). To køb mod lager 1 gav det forventede browserresultat (MV-06-E); teknisk samtidighed og særskilt databasekontrol er ikke dokumenteret. Gentagne produktlinjer er nu afvist korrekt via API som gæst og Customer (MV-11-A/C); direkte SQL-kontrol er fortsat ikke udført. Gyldige statusskift samt afvisning af Shipped → Confirmed og Completed → Confirmed er brugerbekræftet (MV-07-C/D/E). Placed → Shipped er senere afvist korrekt via API (MV-07-G); badgefarver og forbindelsesfejl er fortsat åbne.
- MV-07-D dokumenterer en brugerudført Postman-request med den forventede afvisningsbesked; yderligere API-kontroller udført 2. november er dokumenteret særskilt ovenfor. Ingen af resultaterne dokumenterer automatiserede Playwright-tests, unit tests eller SonarQube-analyse. Tidligere bekræftede kurvskift fremgår fortsat af Manuel verifikation.

- [ ] Registrér testresultater ved de tilhørende testcase-ID'er
- [ ] Beskriv fejl med trin til at genskabe dem, forventet/faktisk resultat og alvorlighed
- [ ] Gentest rettede fejl, og registrér resultatet
- [ ] Udvælg og udfør regressionstests efter ændringer

## 🚦 Test før og efter deployment

- [ ] Vurdér testresultaterne mod de aftalte kriterier før deployment til produktion
- [ ] Test applikationen i et deployment-/testmiljø med relevante testdata
- [ ] Udfør og dokumentér en kort smoke test efter deployment
