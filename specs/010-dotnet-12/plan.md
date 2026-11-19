# 010 – Plan

Gehört zu: [spec.md](spec.md)

## Überblick

Wie [009](../009-dotnet-11/plan.md): nur Server, Build und Images. Keine Verträge, kein Client, kein Spielverhalten.

```
server/global.json            SDK 11.0.1xx → 12.0.1xx (rollForward patch)
server/Directory.Build.props  net11.0 → net12.0
server/**/*.csproj            Pakete 11.0.x → 12.0.x, nur per `dotnet package update` (nuget.config aus 009)
server/dotnet-tools.json      dotnet-ef 11.0.x → 12.0.x
Dockerfile                    dotnet/sdk:12.0 und dotnet/aspnet:12.0-noble-chiseled (per Digest)
README.md                     Voraussetzung .NET 12 SDK
```

## Zeitplan

| Datum | Ereignis |
|---|---|
| voraussichtlich 2027-11-09 | .NET 12.0.0 / SDK 12.0.100 erscheint (LTS); Tag X |
| X + 7 Tage | frühester Start (ADR 0002), sofern auch `Npgsql.EntityFrameworkCore.PostgreSQL` 12.x final und 7 Tage alt ist |
| **X + 28 Tage** | **Wechsel ausgerollt** (AK-3); also voraussichtlich bis 2027-12-07 |
| 2028-11-14 | Supportende .NET 11 (laut 009); harte Grenze für AK-4 |

Das genaue Datum von Tag X und das Supportende werden beim Start aus Microsofts Support-Übersicht
(„.NET and .NET Core Support Policy“) übernommen und hier eingetragen.

## Entschiedene Fragen (Plan)

1. **Wie 009.** Pakete nur per `dotnet package update` mit dem Mindestalter aus `server/nuget.config` (AK-2), Images per
   Tag plus Digest, chiseled, `USER $APP_UID`. Neue Analyzer-Warnungen werden im Code behoben, nicht unterdrückt.
2. **Termin in der Roadmap sichtbar.** Sobald Tag X feststeht, steht „Wechsel bis X + 28 Tage“ in der Roadmap
   (`specs/README.md`) in der Zeile von 010. So geht der Termin nicht unter.
3. **Keine Vorabversionen.** Release Candidates von .NET 12 oder Npgsql werden nicht eingesetzt, auch nicht, um den
   Termin zu halten (Risiken).
4. **Migrationen:** keine neue Migration, solange `has-pending-model-changes` für beide Provider grün bleibt (wie 009).

## Abdeckung der Akzeptanzkriterien

| AK | Wie erfüllt | Wie getestet |
|---|---|---|
| 1 | Build ohne Warnungen, alle Tests | CI (`server`, `server-postgres`, `client`, `docker`) grün; **manuell:** RC in der Testumgebung (Anmelden, Laufen, Chat, Kampf, Tastenbelegung, HUD, Admin) |
| 2 | `nuget.config` aus 009 unverändert, Images wie bisher | **manuell:** `dotnet package update` schließt eine zu junge Version aus; CI `docker` (Trivy) grün |
| 3 | Zeitplan, Termin in der Roadmap | **manuell:** Release-Datum der Version mit .NET 12 (Tag `vX.Y.Z`) liegt höchstens 28 Tage nach Tag X |
| 4 | .NET 11 vollständig ersetzt | **manuell:** `global.json`, `Directory.Build.props`, `Dockerfile` und alle `.csproj` nennen keine 11.x mehr; die Produktion läuft vor 2028-11-14 auf dem neuen Image |

## Constitution-Check

- [x] **Server ist Autorität:** unverändert.
- [x] **Typen nur aus `contracts/`:** keine Vertragsänderung.
- [x] **Offline-Fortschritt:** nicht betroffen.
- [x] **Keine unnötigen neuen Abhängigkeiten:** nur neue Hauptversionen der vorhandenen.
- [x] **Spielwerte:** nicht betroffen.

## Risiken / Abweichungen

- **Npgsql hinkt hinterher:** Bei 009 war Npgsql 11 eine Woche nach .NET 11 noch nicht final. Ist Npgsql 12 erst nach
  X + 21 Tagen final, lässt sich AK-3 ohne Vorabversion nicht halten. Dann verschiebt sich der Termin auf „7 Tage nach
  Npgsql 12 + eine Woche“; die harte Grenze bleibt AK-4. Die Abweichung wird hier und im PR festgehalten.
- **Neue Warnungen und Breaking Changes:** Umfang unbekannt; Puffer bis AK-4 ist rund ein Jahr.
