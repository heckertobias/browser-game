# 010 – Tasks

Gehört zu: [plan.md](plan.md)

Erst beginnen, wenn 009 umgesetzt ist und .NET 12 sowie eine finale `Npgsql.EntityFrameworkCore.PostgreSQL` 12.x
jeweils mindestens 7 Tage alt sind.

## Zeitplan
- [ ] **T1** (AK-3, AK-4): Tag X und Supportende von .NET 11 aus der Support-Übersicht in den Plan eintragen; Termin
  „Wechsel bis X + 28 Tage“ in die Roadmap-Zeile von 010.

## Server
- [ ] **T2** (AK-1, AK-2, AK-4): SDK in `global.json`, `net12.0` in `Directory.Build.props`, Images im `Dockerfile` (per
  Digest); neue Analyzer-Warnungen beheben; Build und Tests grün.
- [ ] **T3** (AK-1, AK-2): Pakete per `dotnet package update` (ohne `--version`), `dotnet-ef` im Tool-Manifest;
  Migrationen ohne offene Modelländerungen; Build und Tests grün.

## Doku
- [ ] **T4** (AK-4): `README.md` (Voraussetzung .NET 12 SDK) und weitere Stellen, die .NET 11 als aktuelle Version
  nennen.

## Tests / Prüfung
- [ ] **T5** (AK-1, AK-3): RC in der Testumgebung: Anmelden, Laufen, Chat, Kampf, Tastenbelegung, HUD, Admin; beide
  Container gesund. Release spätestens X + 28 Tage.

  Danach alle AKs abhaken, Spec-Status auf `umgesetzt`, Roadmap aktualisieren.
