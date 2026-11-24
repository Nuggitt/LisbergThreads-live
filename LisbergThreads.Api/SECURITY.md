# Sikker offentliggørelse

De aktuelle kildefiler indeholder ingen faste administratoradresser, administratoradgangskoder eller udfyldte betalingsnøgler. Administratorer håndteres i Identity-databasen; en valgfri, privat liste med bruger-id'er kan bruges til kontrolleret opsætning. API'et validerer JWT-konfigurationen ved start. Privat konfiguration kopieres ikke med ved publicering.

## Fund og nødvendige handlinger

Git-historikken indeholder tidligere versioner af `LisbergThreads.Api/appsettings.json` med en JWT-signaturnøgle og tidligere versioner af `Program.cs` med administratorernes private mailadresser. `.gitignore` fjerner ikke historiske commits. Det eksisterende repository kan derfor ikke offentliggøres sikkert blot ved at committe de nye filer.

Den lokale JWT-nøgle er udskiftet i den ignorerede `appsettings.Local.json`. En ny, særskilt produktionsnøgle skal konfigureres på serveren. Eventuelle andre miljøer, der benytter en historisk nøgle, skal også skifte den. Det ugyldiggør deres gamle login-tokens. Gennemgangen har ikke fundet administratorernes loginadgangskoder i de aktuelle kildefiler. Der er ikke ændret adgangskoder i databasen eller hos eksterne udbydere. Udskift enhver anden adgangskode eller nøgle, der tidligere har været delt eller committed; sletning af filen gør ikke en kompromitteret nøgle hemmelig igen.

API, klient og Contracts er opgraderet til .NET 10 LTS med Microsoft-pakker i version 10.0.13. Den tidligere .NET 9-version mistede sikkerhedssupport den 10. november 2026. Se [Microsofts supportpolitik](https://dotnet.microsoft.com/en-us/platform/support/policy). En NuGet-sårbarhedsscanning dækker ikke dette livscyklusproblem eller alle fejl i egen kode.

## Offentlig kildekopi uden historik

Kør fra repositoryets rod:

```sh
python3 LisbergThreads.Api/scripts/export-public-source.py
```

Scriptet opretter `LisbergThreads.Api/artifacts/lisbergthreads-public-source-<tidspunkt>.zip`. Det inkluderer de aktuelle kildefiler og tomme konfigurationseksempler. Det udelader Git-historik, private appsettings, miljøfiler, nøgler, databasebackups, uploads og buildmapper. Indholdet kontrolleres for udvalgte nøglemønstre og private mailadresser før eksport; kontrollen er ikke en fuldstændig sikkerhedsaudit.

Pak arkivet ud i en ny, tom mappe. Gennemgå filerne. Opret et nyt Git-repository fra den mappe og skub det til et nyt GitHub-repository. Brug GitHubs private noreply-adresse som commit-adresse, hvis din private adresse ikke skal offentliggøres. Kopiér ikke `.git` fra det gamle projekt, og skub ikke gamle branches eller tags. Opret og udfyld privat konfiguration separat efter behov. Det gamle repository bevares som privat arbejds-/backupkopi.

Hvis det eksisterende GitHub-repository skal genbruges, kræver det en særskilt gennemgang og rensning af samtlige branches, tags og øvrige referencer. Gamle pull requests, forks, artifacts og cacher kan bevare indhold. Der er ikke omskrevet historik, foretaget force-push eller ændret GitHubs synlighed under denne opgave. Rensning af historik erstatter aldrig udskiftning af eksponerede nøgler.

## Drift

- Læg kun tomme eksempler i Git. Brug `appsettings.Local.json`, User Secrets i Development eller serverens miljøvariabler til private værdier.
- Læg aldrig servernøgler i Blazor-klienten. Alt under klientens `wwwroot` kan downloades af besøgende.
- Brug en separat SQL-bruger og krypteret forbindelse i produktion. Opbevar ikke produktionsdatabasebackups i repositoryet.
- Bevar HTTPS, adgangskontrol, lagerkontrol og betalingsvalidering på serveren. Log ikke tokens, mailindhold eller betalingsnøgler.
- Kør `dotnet list LisbergThreads.Api/LisbergThreads.sln package --vulnerable --include-transitive` løbende, og hold runtime samt afhængigheder opdaterede.

SQL Server-driverens indirekte afhængighed af `System.Security.Cryptography.Pkcs` er fastlagt til den rettede version 10.0.13 på grund af CVE-2026-84008 og CVE-2026-70162.

Den afsluttende status i samtalen beskriver, hvilke kontroller der faktisk er kørt. Maillevering, MobilePay, browserforløb og Simply-drift skal afprøves manuelt efter opsætning; se `DEPLOYMENT.md`.
