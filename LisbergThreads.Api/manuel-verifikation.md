# Trinvis manuel verifikation

Guiden bruges sammen med [test-plan.md](test-plan.md). Alle forløb nedenfor er **ikke kørt**, indtil du selv har registreret resultatet. Den samlede mobil- og spacinggennemgang er allerede meldt færdig; kontrollér fortsat konkrete funktioner på mobil, hvor det er angivet. Udvalgte browserforløb planlægges suppleret med Playwright som beskrevet nedenfor. Betaling og deployment indgår ikke i dette forløb.

## Planlagte værktøjer: Playwright og SonarQube

Vi bruger gratis lokale muligheder, hvis de tekniske forudsætninger kan opfyldes. [Playwright](https://github.com/microsoft/playwright) er tilgængeligt under Apache 2.0-licensen. [SonarQube Community Build](https://www.sonarsource.com/products/sonarqube/downloads/) er gratis og understøtter C#; lokal drift kræver maskinressourcer og opsætning. Betalte abonnementer og cloudtjenester er ikke nødvendige for dette planlagte forløb. Oplysningerne er kontrolleret den 21. oktober 2026.

- [ ] Kontrollér lokal kompatibilitet og forudsætninger før installation; registrér versioner og eventuelle blokeringer
- [ ] Klargør Playwright og brug først den synlige browser og [codegen](https://playwright.dev/docs/codegen) til et fælles eksempel med login, produktsøgning og kurv
- [ ] Tilføj eksplicitte kontroller af forventede resultater; optagede klik alene dokumenterer ikke korrekt funktionalitet
- [ ] Udvid med checkout, adgangskontrol og relevante mobilbredder; brug adskilte browserkontekster for gæst, Customer og Admin samt gentagelige testdata
- [ ] Gem rapport og ved fejl relevante traces/skærmbilleder uden at committe tokens eller persondata; knyt resultatet til guidens MV-ID'er
- [ ] Brug Playwrights netværksstyring til udvalgte fejlscenarier i afsnit 9 og 12; markér tydeligt, når et svar er simuleret og serveradfærden derfor ikke er verificeret
- [ ] Klargør lokal SonarQube Community Build og SonarScanner for .NET, analysér solutionen, og registrér relevante fund i testplanen

Forløb, du selv udfører og vurderer i browseren, registreres som manuelle. Scripts med automatiske kontroller registreres som automatiserede systemtests. Visuel vurdering, faktisk mailmodtagelse og databasekontrol udføres fortsat særskilt, hvor Playwright-forløbet ikke dækker dem. SonarQube analyserer kode uden at afprøve brugerforløb og erstatter hverken disse kontroller, Postman eller unit tests. Coverage kræver udførte tests og en særskilt coverage-rapport; SonarQube måler den ikke ved blot at scanne koden.

Opsætning og automatiserede tests er planlagt, men ikke implementeret eller kørt. Den konkrete opsætningsguide udarbejdes, når det lokale miljø er undersøgt.

## 1. Klargør miljø og resultatark

1. Brug et lokalt testmiljø med SQL Server og testdata. Købsforløbene ændrer lager og opretter ordrer; mailtests skal bruge en aftalt testindbakke.
2. Start API og klient fra Rider som normalt. Kontrollér, at produktlisten henter data. Notér klientadresse, API-adresse, dato og commit (`git rev-parse HEAD`). Brug miljøets faktiske adresser i alle API-kald nedenfor.
3. Hav en Admin og to forskellige Customer-konti klar: kunde A og kunde B. Opret kunderne gennem registreringen; brug en eksisterende Admin.
4. Brug adskilte browserprofiler til Admin, kunde A og gæst. Brug endnu en profil eller log ind som kunde B, når ejerskab skal testes. To almindelige faner deler normalt login og browserlagring.
5. Opret som Admin disse produkter med tydelige testnavne. Notér deres ID'er fra adresser eller API-svar.

| Produkt | Pris | Lager | Status | Formål |
|---|---:|---:|---|---|
| TEST-A | 100 kr. | 5 | Aktiv | Kurv og checkout |
| TEST-B | 75 kr. | 0 | Aktiv | Udsolgt |
| TEST-C | 50 kr. | 3 | Deaktiveret | Afvisning af køb |
| TEST-D | 25 kr. | 1 | Aktiv | Samtidige køb |
| TEST-SLET | 10 kr. | 1 | Aktiv | Sletning uden ordre-/kurvlinjer |

6. Hav to gyldige billeder, en tekstfil og et billede over 5 MB klar. Brug kun testindhold.
7. Kopiér denne resultatrække til afsnittet Testresultater i testplanen for hvert forløb. Gem ikke passwords, JWT eller gæsteordrens token i dokumentationen.

| ID | Miljø/commit/dato | Faktisk resultat | Status | Fejl og gentest |
|---|---|---|---|---|
| MV-01 | Udfyld | Udfyld efter test | Ikke kørt | Udfyld ved behov |

Brug status Bestået, Fejlet eller Blokeret. Afkryds kun de tilsvarende punkter i testplanen, som du faktisk har udført. Hvis en deltest fejler, skriv trin, forventet resultat og faktisk resultat, og fortsæt med uafhængige forløb.

## 2. Konto og grundlæggende adgang — MV-01

1. Registrér kunde A med tomme felter, ugyldig e-mail og et password, der ikke opfylder de viste krav. Forvent forståelige valideringsbeskeder uden rå serverfejl.
2. Registrér med gyldige oplysninger. Log ud og ind igen, også med Enter. Forvent adgang til Min konto.
3. Prøv forkert password. Forvent en dansk fejl uden login.
4. Redigér profil, gem og genindlæs. Forvent bevarede oplysninger og grøn bekræftelse.
5. Skift password. Log ud; det gamle password skal afvises og det nye accepteres.
6. Test Glemt adgangskode via testindbakken, hvis mail er konfigureret. Følg linket og kontrollér login med det nye password. Ellers registreres denne del som Blokeret.
7. Åbn en ukendt adresse. Forvent dansk 404 og fungerende links.
8. Åbn en beskyttet side som gæst. Forvent loginvisning. Åbn derefter en Admin-side som kunde A ved at kopiere sidens adresse fra Admin-profilen. Forvent 403 uden Admin-indhold.
9. Kontrollér, at kunde A hverken har Admin-menu eller Admin-søgning.

## 3. Produkter, søgning og billeder — MV-02

1. Kontrollér TEST-A og TEST-B som gæst: pris og lagerlabel skal passe; TEST-B skal vise Udsolgt og må ikke kunne lægges i kurven.
2. Søg på hele og dele af TEST-A's navn, derefter på noget uden match. Forvent løbende relevante resultater og en forståelig tom-visning.
3. Opret et ekstra testprodukt som Admin med lager og billede. Redigér navn, pris, størrelse og lager; genindlæs og kontrollér værdierne som gæst.
4. Tilføj flere billeder. Åbn produktdetaljer og vælg thumbnails/bladr mellem billederne. Forvent korrekt hovedbillede.
5. Forsøg upload af tekstfil og et billede over 5 MB. Forvent dansk fejl og bevarede eksisterende billeder.
6. Deaktivér testproduktet: kontrollér bekræftelsesdialog og annullér først; gentag og bekræft. Kontrollér status og adminfiltrene for aktive, udsolgte og deaktiverede. Aktivér igen.
7. Åbn samme produkt til redigering i to faner. Gem en ændring i den første, og forsøg derefter at gemme fra den anden uden genindlæsning. Forvent konfliktbesked, bevarede formularrettelser og ingen overskrivning af den først gemte ændring.
8. Forsøg permanent sletning af aktive TEST-SLET direkte via API i afsnit 11: forvent afvisning. Deaktivér derefter produktet og slet via brugerfladen; kontrollér advarslen og at produktet er væk efter genindlæsning.
9. Gentag sletteforsøget på et deaktiveret produkt med en gemt kundekurvlinje og senere et produkt med en gennemført ordre. Forvent afvisning, også hvis ordren er Completed. Brug ikke kun en lokal gæstekurv som slettebeskyttelse.

## 4. Kurv og lagergrænser — MV-03

Udfør trin 1–6 både som gæst og som kunde A med tom kurv og TEST-A på lager 5.

1. Åbn kurvside, sidebar og checkout uden varer. Forvent tom-visning og Se produkter; sidebar skal lukkes ved navigation.
2. Læg 2 TEST-A i kurven. Forvent grøn bekræftelse, antal 2 og total 200 kr. i kurv og sidebar samt korrekt badge.
3. Tilføj yderligere 3. Forvent samlet antal 5 og total 500 kr.
4. Forsøg at tilføje 1 mere. Forvent afvisning uden ændring af antal og total.
5. Forsøg antal 0, -1 og 6 gennem brugerfladen, hvor felterne tillader det. Ugyldige antal skal blokeres; en eventuel fjernelseshandling ved 0 kontrolleres særskilt. Den direkte API-test i afsnit 11 skal også udføres.
6. Ændr antal i kurv og sidebar, og fjern varen. Forvent samme antal og total overalt og tom-visning efter fjernelse. Kontrollér sidebar med tastatur og kurvens handlinger på mobil.
7. Læg igen en vare i kurven, og kontrollér som Admin, at lageret stadig er 5. Varer i kurven reserverer ikke lager.
8. Kurv ved logout og brugerskift er tidligere bekræftet. Gentag kun ved ændringer eller mistanke om fejl; markér ikke denne tidligere verifikation som ny test.

## 5. Gennemført køb som kunde og gæst — MV-04 og MV-05

1. Sæt TEST-A til pris 100 kr. og lager 5. Tøm testkundens kurv og læg 2 i den.
2. Åbn checkout og forsøg at bestille med tomme leveringsfelter og ugyldig e-mail. Forvent danske valideringsfejl og ingen gennemført ordre.
3. Udfyld gyldige testoplysninger og kontrollér 2 × 100 kr. = 200 kr. Gennemfør én bestilling.
4. Kontrollér bekræftelsens ordrenummer, dato, produkter, antal, priser, total, leveringsoplysninger og status Placed. Notér ordre-ID uden eventuelt token.
5. Kontrollér tom kurv og korrekt badge. Genindlæs produktet som Admin: lager skal være 3.
6. Åbn Mine ordrer og derefter ordren som kunde A. Forvent samme data. Kontrollér den samme ordre i Admin-visningen.
7. Gentag trin 1–5 som gæst i separat profil efter nulstilling af testlageret. Gæstens ordrebekræftelse skal fungere med dens bekræftelsesadgang; den er ikke en registreret kundes ordrehistorik.
8. Brug Prøv igen ved en fejl på ordrebekræftelsen som beskrevet i afsnit 9. Det må kun genhente ordren og må ikke oprette en ny.

Betalingsstatus kan vise Paid, fordi checkout sætter den automatisk. Det er ikke dokumentation for en virkelig betaling; betalingsintegration er ikke implementeret.

## 6. Afvist checkout og samtidige køb — MV-06

1. Læg 2 TEST-A i kurven, mens lageret er mindst 2. Sænk derefter lageret til 1 som Admin, uden at genindlæse checkout.
2. Forsøg at bestille. Forvent lagerfejl, intet lagertræk og ingen ny gennemført ordre. En Pending-kurv er ikke en gennemført ordre.
3. Kontrollér lager og ordreoversigt efter genindlæsning. Ret kurven til 1 og kontrollér, at checkout kan gennemføres.
4. Gentag med et produkt, som Admin deaktiverer efter tilføjelsen til kurven. Forvent afvisning uden delvis ordre eller lagertræk. Gentag de afviste forløb som både gæst og kunde.
5. Sæt TEST-D til lager 1. Brug to adskilte browserprofiler, læg 1 i hver kurv og gør begge checkoutformularer klar.
6. Afsend begge bestillinger så tæt på hinanden som muligt, eventuelt med en hjælper. Forvent præcis ét gennemført køb, én afvisning og slutlager 0. Hvis afsendelserne tydeligt skete efter hinanden, er samtidighed ikke verificeret; brug de samtidige API-kald i afsnit 11.
7. Kontrollér produktets StockQuantity og de relevante ordrer i SQL Server med en læseforespørgsel eller Riders databasevisning. Forvent aldrig negativt lager eller ordrelinjer fra et delvist, afvist køb. Notér lager før/efter og de oprettede ordre-ID'er.

## 7. Ordreejerskab og Admin — MV-07

1. Kopiér adressen til kunde A's ordre. Åbn den som kunde B og som gæst uden bekræftelsestoken. Forvent ingen ordreoplysninger. Kontrollér også API'et i afsnit 11.
2. Åbn gæsteordren uden token, med forkert token og med korrekt token. Kun korrekt bekræftelsestoken eller Admin-adgang må give gæsten adgang.
3. Åbn Admin-kundeoversigten, find kunde A og kontrollér navn, e-mail, telefon eller Ikke angivet, kontostatus, oprettelsesdato og ordrehistorik. Åbn en ordre derfra.
4. Søg som Admin på kundenavn, e-mail og ordrenummer. Kontrollér resultattyper, klik og Enter. Skift mellem produkt- og Admin-søgning og kontrollér, at nye kunde-/ordredata bliver hentet.
5. Skift en testordre gennem Placed → Confirmed → Shipped → Completed. Forvent grøn bekræftelse og samme status/badge efter genindlæsning som kunde og Admin.
6. Kontrollér ordrestatusfilter, et filter uden resultater og en kunde uden ordrer.
7. Forsøg at springe fra Placed til Shipped og at gå fra Completed tilbage til Confirmed direkte via API. Forvent afvisning og uændret ordrestatus.

## 8. Om-siden — MV-08

1. Åbn /om-lisberg-threads via menu, forside og footer som gæst og kunde. Forvent siden uden redigeringskontroller.
2. Åbn som Admin. Kontrollér præcis tre redigerbare sytrin og tre strikketrin.
3. Redigér sidetitel, overtekster, afsnits- og trinoverskrifter, brødtekst, afslutning og linktekster. Genindlæs: teksterne skal være gemt, og links skal stadig åbne de korrekte sider.
4. Upload to historiebilleder og billeder til procestrin. Redigér billedtekst; kontrollér placering og alt-tekst via browserens Inspektér efter genindlæsning.
5. Udskift og fjern ét billede. Genindlæs: kun den valgte plads skal være ændret. Fjernelse fra siden behøver ikke slette filen på serveren.
6. Forsøg ugyldig fil og billede over 5 MB. Forvent dansk fejl og bevaret eksisterende billede.
7. Redigér samme tekst eller billedplads i to faner; gem først den ene og derefter den anden. Forvent konflikt frem for overskrivning af den nyeste gemte ændring.
8. Gentag produkt-upload fra afsnit 3 for at kontrollere, at den fælles uploadfunktion fortsat virker.

Personlige tekster og faktiske arbejdsbilleder kræver separat indsamling og godkendelse; test af redigeringsfunktionen afslutter ikke den opgave.

## 9. Loading, fejl, feedback og uklart checkout — MV-09

1. Åbn browserens udviklerværktøjer med F12 → Network, vælg langsom forbindelse og genindlæs hver relevant side: produkter, produktdetaljer/-redigering, kurv/sidebar, checkout, kunde-/adminordrer, kundedetaljer, Min konto og Om-siden. Forvent spinner under ventetid og korrekt indhold bagefter.
2. Blokér den relevante API-request i Network eller stop test-API'et, mens klienten er åben. Navigér til siderne igen. Forvent forståelig dansk fejl uden rå exceptiontekst eller falsk succes. Offline-funktionen alene er ikke tilstrækkelig, hvis browseren stadig kan nå den lokale server; kontrollér, at requesten faktisk fejler.
3. Fjern blokeringen/start API'et igen og brug Prøv igen eller den viste genindlæsningsmulighed. Forvent korrekte aktuelle data. På ordrebekræftelsen må dette ikke sende en ny checkout-request.
4. Afbryd specifikt hentning af kurvantal. Forvent ? frem for falsk 0. Genetablér og opdatér kurven: korrekt badge skal vende tilbage.
5. Forsøg login under en serverfejl. Forvent en anden forståelig besked end ved forkert password.
6. Test uklart checkout-resultat på en særskilt testordre ved at afbryde forbindelsen, mens checkout-requesten er undervejs. Bekræft i Network, at det faktisk er checkout-kaldet, der afbrydes. Forvent advarsel og låst bestillingsknap i den aktuelle visning. Undersøg først Admin-ordrer og lager; send ikke bestillingen igen, før resultatet er afklaret.
7. Notér, om serveren nåede at gennemføre ordren. En afbrydelse før afsendelse beviser ikke håndtering af et tabt svar efter gennemført køb; registrér den del som Blokeret, hvis du ikke kan fremprovokere den sikkert.
8. Udfør igen et almindeligt profilgem, en kurvændring, produktgem og statusændring. Forvent ens grøn feedback og fungerende normalforløb efter fejlene.
9. Kontrollér konkrete handlinger med lange produktnavne på mobil: checkout, kundens/admins ordredetaljer, kundeliste, søgning, kurv/sidebar og produktformularer. Knapper og data skal kunne tilgås; tabeller må scrolle inden for deres ramme.

Avancerede fejl som ulæselige svar, utilgængelig localStorage, JavaScript-fejl i sidebar og global opstartsfejl kræver kontrolleret fejlsimulering. Brug afsnit 12 og registrér hver del særskilt; de er ikke bestået alene på baggrund af dette afsnit.

## 10. Kontakt og mail — MV-10

1. Aftal testmodtager og konfigurér Contact:RecipientEmail samt SMTP i det lokale miljø. Genstart API'et. Notér kun, at opsætningen er udført; skriv ikke hemmeligheder i testplanen. Uden opsætning er faktisk maillevering Blokeret.
2. Åbn /kontakt som gæst og kunde. Kontrollér menu-/footerlinks, ingen telefonkontakt og eventuel aftalt svartid.
3. Vælg alle ni emner. Ordrefelt skal vises ved bestilling, levering, ændring og returnering; produktfelt ved produkt, størrelse, pleje og særlige ønsker; Andet har ingen af de to.
4. Udfyld et ekstra felt, og skift emne, så feltet skjules. Send senere en gyldig testbesked og kontrollér i mailen, at den skjulte værdi ikke er med.
5. Forsøg tomme obligatoriske felter og ugyldig e-mail. Forvent validering og ingen mail. Test også API-grænserne i afsnit 11.
6. Send en gyldig testbesked. Prøv dobbeltklik og et nyt klik efter succes uden at ændre teksten. Forvent én afsendelse. Kontrollér i testindbakken emne, indhold og Reply-To; prøv Svar uden at sende, og kontrollér modtageradressen.
7. Redigér indholdet før næste gyldige afsendelse. Send i alt fire gyldige requests fra samme IP inden for ti minutter. Den fjerde skal give 429; behold formularteksten. Tidligere forsøg kan have brugt grænsen: vent ti minutter før en ny isoleret måling. Grænsen deles af brugere bag samme IP i én API-instans.
8. Test Website-spamfeltet direkte via API i en separat måling uden aktiv rateblokering. Forvent afvisning uden mail.
9. I testmiljøet: afprøv manglende SMTP-konfiguration, forkert SMTP-login, en kontrolleret SMTP-timeout og afbrudt API. Forvent ingen falsk succes og bevaret formulartekst. Gendan gyldig opsætning bagefter. Registrér timeout som Blokeret, hvis den ikke kan fremprovokeres kontrolleret.
10. Gentest Glemt adgangskode efter kontaktopsætningen; den må ikke sende nulstillingsmail til kontaktmodtageren.

Succes i formularen betyder accepteret af mailserveren. Modtagelse og Reply-To skal bekræftes særskilt i indbakken.

## 11. Direkte API-kontroller — MV-11

Disse kontroller er manuelle API-tests og kræver fx Postman eller et tilsvarende værktøj. De undersøger serverregler, som brugerfladen kan forhindre dig i at ramme.

1. Åbn F12 → Network i et gyldigt browserforløb. Find den relevante request, og brug Copy as cURL → Import i Postman. Gem en lokal collection med variabler til API-adresse og produkt-/ordre-ID. Brug testdata og opbevar tokens lokalt uden Git.
2. Behold en uændret kopi som reference. Ændr kun ét forhold ad gangen i kopien. Gendan lager/testkurv før gentagelser, så et tidligere køb ikke ændrer forventningen.
3. Kontrollér både HTTP-status, response-data og databaseeffekt. En succesfuld statuskode alene beviser ikke korrekt lager eller adgangskontrol.

| Request/udgangspunkt | Ændring og forventet resultat |
|---|---|
| POST /api/cart/items og PUT /api/cart/items/{productId} som kunde | Prøv antal 0, -1 og over lager, samt tilføjelse hvor eksisterende kurvantal gør summen for stor. Forvent afvisning og uændret kurv. |
| POST /api/cart/checkout | Prøv tom Items, antal 0/-1, udsolgt/deaktiveret produkt og gentagne linjer med samme ProductId, fx 3 + 3 ved lager 5. Forvent 400 og ingen gennemført ordre eller lagertræk. Gentag som gæst uden Authorization og som kunde med gyldig Authorization. |
| POST /api/cart/checkout | Ændr produktprisen som Admin efter kurvtilføjelse. Send en gyldig bestilling; forvent den pris, som kunden så i kurven (prisen fra tilføjelsestidspunktet), så kunden ikke betaler en anden pris end den viste. Klientens sendte priser bruges ikke. |
| To gyldige checkout-requests mod TEST-D med lager 1 | Send fra to adskilte klienter så samtidigt som muligt med hver sin kundes/gæsts oplysninger. Forvent én succes, én lagerafvisning og lager 0; kontrollér database og ordreoversigt. Et sekventielt collection-run dokumenterer ikke samtidighed. |
| GET /api/orders/{orderId} | Brug kunde A's ordre med kunde B's Authorization og uden query-token. Forvent 403 uden ordredata; kunde B skal forblive logget ind og kunne åbne Min konto. Uden login og token forventes 401. Ejeren og Admin skal få 200. Ukendt ID giver 404. |
| GET /api/orders/{guestOrderId}?token=... | Fjern Authorization. Test uden token, et andet gyldigt formateret GUID og korrekt token: forvent henholdsvis 401, 401 og 200. Del ikke den gyldige tokenadresse i dokumentationen. |
| Admin-requests til kunder, ordrer, produkter og indhold | Importér først et gyldigt Admin-kald. Fjern Authorization: forvent 401; erstat med kunde A's token: forvent 403. Kontrollér, at svaret ikke indeholder beskyttede data, og at intet ændres. |
| DELETE /api/products/{id}/permanent som Admin | Aktivt produkt samt deaktiverede produkter med gemte kurv-/ordrelinjer skal afvises. Kontrollér, at produktet stadig findes. |
| PUT /api/orders/{orderId}/status som Admin | Behold bodyformatet fra den importerede request. Prøv Placed → Shipped og Completed → Confirmed: forvent afvisning og uændret status. |
| PUT /api/content/about/{key}, POST /api/uploads/content-image | Gentag uden login og som Customer: forvent 401/403 og ingen ændring. Send også ugyldig billedfil og over 5 MB som Admin; forvent serverafvisning. Gentag for produkt-upload. |
| POST /api/contact | Prøv ukendt Topic, tomme krævede felter og ugyldig e-mail. Test maksimum og maksimum + 1 for Name 100, Email 254, OrderNumber 120, Product 500 og Message 5000. Brug ellers gyldig request, og planlæg pauser pga. rategrænsen. Ugyldige værdier skal afvises uden mail. |
| POST /api/contact | Sæt Website til en ikke-tom værdi i ellers gyldig body. Forvent afvisning uden mail. |

Hvis du ikke har værktøj eller et kontrolleret setup til en række, registrér den som Blokeret med årsagen. Behold dens punkt åbent i testplanen.

## 12. Avanceret fejlsimulering — MV-12

Udfør kun i en isoleret testbrowser og testmiljø. Brug browserens response overrides eller en lokal HTTP-proxy, der kan ændre/blokere svar. Registrér værktøj og præcis ændring, så testen kan gentages. Hvis værktøjet ikke er tilgængeligt, markér deltesten Blokeret; ændr ikke applikationskoden for at foregive et bestået resultat.

1. Erstat login-API'ets succes-body med tomt eller ugyldigt JSON. Forvent dansk svarfejl uden falsk login og uden misvisende besked om forkert password.
2. Erstat ordrehentningens svar med ugyldigt JSON, og simulér derefter timeout. Navigér også fra én ordre til en anden under langsom hentning. Forvent relevant fejl og ingen visning af den gamle ordre som den nye.
3. Brug debugger eller testbrowserens lagringsbegrænsninger til at få de relevante localStorage-operationer til at fejle. Kontrollér i debuggeren, at operationen faktisk fejler; en tom lagring er ikke det samme. Gentag login, profilgem, passwordskift, nulstilling og logout. Forvent dansk fejl uden falsk succes.
4. Gennemfør gæstecheckout med fejl ved den efterfølgende lokale kurvrydning. Kontrollér, at serveren kun har én ordre, og at ordrebekræftelsen vises med advarsel om, at kurven ikke er tømt. Afklar ordren før nye købsforsøg.
5. Fremprovokér en fejl i sidebarens JavaScript-kald via debuggeren. Forvent, at kurven eller den valgte destination kan åbnes som almindelig side.
6. Blokér en nødvendig opstartsressource i testbrowseren og kontrollér den danske opstartsfejl. Fremprovokér separat en kontrolleret global fejl med debuggeren; kontrollér Genindlæs og at en checkoutfejl ikke opfordrer til en ny bestilling. Fjern alle blokeringer/overrides efter testen.

## 13. Afslut og registrér

1. Overfør hvert faktisk resultat til testplanen og afkryds de dækkede delpunkter. Ved fejl: beskriv præcise trin, forventet/faktisk resultat og eventuel dokumentation uden persondata eller tokens.
2. Gentest rettelser. Efter en ændring i checkout/lager gentages MV-03–MV-07; efter adgangsændringer gentages relevante API-kontroller. Gentag øvrige berørte områder efter behov.
3. Resterende manuel verifikation kan først krydses af, når de relevante deltests er udført og accepteret. Blokerede eller udsatte punkter skal fremgå eksplicit.
4. Kontaktopsætning, godkendelse af personlige Om-tekster, automatiske tests og deployment har særskilte punkter. Et gennemført manuelt forløb afslutter dem ikke automatisk.
