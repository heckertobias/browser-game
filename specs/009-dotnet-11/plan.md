# 009 – Plan

Gehört zu: [spec.md](spec.md)

## Überblick

Nur Server, Build und Images. Keine Verträge, kein Client, kein Spielverhalten.

```
server/global.json            SDK 10.0.404 → 11.0.1xx (rollForward patch)
server/Directory.Build.props  net10.0 → net11.0
server/nuget.config           neu: nuget.org mit minPublishAgeHours = 168 (AK-2)
server/**/*.csproj            Pakete 10.0.x → 11.0.x, nur per `dotnet package update`
server/dotnet-tools.json      dotnet-ef 10.0.13 → 11.0.x
Dockerfile                    dotnet/sdk:11.0 und dotnet/aspnet:11.0-noble-chiseled (per Digest)
AGENTS.md, README.md          Regel für NuGet-Pakete, Voraussetzung .NET 11 SDK
```

Die CI liest das SDK aus `global.json` (`setup-dotnet` mit `global-json-file`) und braucht keine Änderung.

## Voraussetzungen und Zeitpunkt

- .NET 11.0.0 / SDK 11.0.100 erschien am 2026-11-10; frühestens ab **2026-11-17** (7 Tage, ADR 0002).
- `Npgsql.EntityFrameworkCore.PostgreSQL` 11.0.0 ist noch nicht erschienen (Stand 2026-11-13: rc.3). Der Wechsel
  beginnt erst, wenn es eine finale 11.x gibt, die mindestens 7 Tage alt ist. Eine Vorabversion nehmen wir nicht.
- Support von .NET 11 endet am 2028-11-14; der Wechsel auf .NET 12 ([010](../010-dotnet-12/spec.md)) muss davor
  liegen.

## Entschiedene Fragen (Plan)

1. **`nuget.config` zuerst:** Sie entsteht vor allen Paket-Updates, damit schon diese Updates das Mindestalter
   beachten. Angelegt mit `dotnet nuget add source https://api.nuget.org/v3/index.json --name nuget.org
   --min-publish-age-hours 168 --configfile server/nuget.config`; geerbte Quellen werden mit `<clear />` entfernt,
   damit nur diese eine Quelle gilt.
2. **Pakete nur per Werkzeug (AK-2):** `dotnet package update` ohne `--version` je Projekt. Das Werkzeug wählt die
   neueste Version, die mindestens 7 Tage alt ist, und trägt sie fest ein. Kein Bearbeiten der `.csproj` von Hand.
3. **Ausnahme (AK-3):** Für einen dringenden Sicherheitsfix wird die Version ausdrücklich angegeben
   (`dotnet package add <Paket> --version <x>`); der PR nennt Paket, Version und Grund. Das steht in `AGENTS.md`.
4. **`System.Security.Cryptography.Pkcs`:** Das direkte Pinning wegen MimeKit (Kommentar im `.csproj`) bleibt nur,
   wenn `NuGetAudit` ohne es wieder anschlägt; sonst fällt der Eintrag weg.
5. **Neue Analyzer-Warnungen:** `AnalysisLevel` `latest-recommended` nutzt mit .NET 11 die Regeln von .NET 11
   (bisher fälschlich .NET 9). Mit `TreatWarningsAsErrors` brechen neue Warnungen den Build. Sie werden im Code
   behoben, nicht unterdrückt; eine Unterdrückung nur mit Begründung im Kommentar.
6. **Migrationen:** Keine neue Migration, solange `dotnet ef migrations has-pending-model-changes` (CI-Schritt
   „Migrationen passen zum Modell“) für beide Provider und beide Kontexte grün bleibt. Die `ProductVersion`-Annotation
   in den bestehenden Migrationen bleibt unverändert.
7. **Images per Digest:** Wie bisher Tag plus `@sha256:…`; der Digest wird von der veröffentlichten Image-Version
   übernommen, die mindestens 7 Tage alt ist (Dependabot-PR #49 für `dotnet/sdk`, falls er wieder offen ist).
8. **Lokale Entwicklung:** Bis Homebrew ein SDK 11 anbietet, laufen Server-Build und -Tests lokal im SDK-Container.

## Abdeckung der Akzeptanzkriterien

| AK | Wie erfüllt | Wie getestet |
|---|---|---|
| 1 | Build ohne Warnungen, alle Tests | CI (`server`, `server-postgres`, `client`, `docker`) grün; **manuell:** RC in der Testumgebung (Anmelden, Laufen, Chat, Tastenbelegung, HUD, Admin) |
| 2 | `nuget.config` mit `minPublishAgeHours`, Regel in `AGENTS.md` | **manuell:** `dotnet package update` meldet eine zu junge Version als ausgeschlossen; `nuget.config` im Repo geprüft |
| 3 | ausdrückliche Version mit Begründung | Regel in `AGENTS.md`; **manuell:** `dotnet package add <Paket> --version <neu>` nimmt die Version |
| 4 | chiseled-Images, `USER $APP_UID` unverändert | CI `docker` (Build, Trivy-Scan); **manuell:** im RC laufen beide Container gesund wie bisher |

## Constitution-Check

- [x] **Server ist Autorität:** unverändert.
- [x] **Typen nur aus `contracts/`:** keine Vertragsänderung.
- [x] **Offline-Fortschritt:** nicht betroffen.
- [x] **Keine unnötigen neuen Abhängigkeiten:** nur neue Hauptversionen der vorhandenen.
- [x] **Spielwerte:** nicht betroffen.

## Risiken / Abweichungen

- **Npgsql hinkt hinterher:** Ohne finale Npgsql-11-Version kein Start (siehe oben).
- **Neue Warnungen:** Umfang unbekannt; können T2 vergrößern.
- **Lokales SDK:** Homebrew hat noch kein SDK 11; lokal nur im Container (wie schon jetzt bei 10.0.404).
- **Werkzeug-Updates:** Ob `dotnet tool update` das Mindestalter beachtet, sagt die Doku nicht; für `dotnet-ef` wird
  das Alter der gewählten Version von Hand geprüft.
