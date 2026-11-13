# 009 – Tasks

Gehört zu: [plan.md](plan.md)

Erst beginnen, wenn .NET 11 und eine finale `Npgsql.EntityFrameworkCore.PostgreSQL` 11.x jeweils mindestens 7 Tage
alt sind (frühestens 2026-11-17).

## Werkzeug
- [ ] **T1** (AK-2, AK-3): `server/nuget.config` mit nuget.org und `minPublishAgeHours` 168 (per
  `dotnet nuget add source`, geerbte Quellen geleert); Regel für NuGet-Pakete und die Ausnahme in `AGENTS.md`.

## Server
- [ ] **T2** (AK-1, AK-4): SDK in `global.json`, `net11.0` in `Directory.Build.props`, Images im `Dockerfile` (per
  Digest); neue Analyzer-Warnungen beheben; Build und Tests grün.
- [ ] **T3** (AK-1, AK-2): Pakete per `dotnet package update` (ohne `--version`), `dotnet-ef` im Tool-Manifest;
  Pkcs-Pinning prüfen; Migrationen ohne offene Modelländerungen; Build und Tests grün.

## Doku
- [ ] **T4**: `README.md` (Voraussetzung .NET 11 SDK) und weitere Stellen, die .NET 10 als aktuelle Version nennen.

## Tests / Prüfung
- [ ] **T5**: RC in der Testumgebung: Anmelden, Laufen, Chat, Tastenbelegung, HUD, Admin; beide Container gesund.
  `dotnet package update` schließt eine zu junge Version aus (AK-2).

  Danach alle AKs abhaken, Spec-Status auf `umgesetzt`, Roadmap aktualisieren.
