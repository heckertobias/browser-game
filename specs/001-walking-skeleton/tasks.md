# 001 – Tasks

Gehört zu: [plan.md](plan.md)

Reihenfolge: Verträge → Server → Client → Tests/Prüfung → CI/CD.
Voraussetzung lokal: .NET 10 SDK (`brew install dotnet`), Node (vorhanden), buf (vorhanden).

## Verträge
- [x] **T1** (AK-3, AK-5): `contracts/proto/browsergame/v1/game.proto` wie im Plan, `buf.yaml` + `buf.gen.yaml` im Repo-Root.
  CI-Job `contracts` (`bufbuild/buf-action`, SHA-gepinnt) mit `buf lint`, in `ci-ok.needs` eintragen.

## Server
- [x] **T2**: Projekte `server/BrowserGame.Server` (ASP.NET Core) und `server/BrowserGame.Server.Tests` (xUnit) plus
  Solution. Codegen über `Grpc.Tools` (`GrpcServices="None"`); `dotnet build` erzeugt die Contract-Typen.
- [x] **T3** (AK-2, AK-5, AK-7): `World` mit Input-Warteschlange (max. 10), einer Eingabe pro Tick, Kürzen auf
  Länge ≤ 1, ohne Grenzen. xUnit-Tests laut Abdeckungstabelle.
- [x] **T4** (AK-3, AK-4): `/ws`-Endpoint, `GameLoop` (20 Hz) mit `Welcome` und `Snapshot` pro Empfänger. Bei
  Disconnect wird der Spieler entfernt. Frame-Limit 1 KB, zufällige Namen und Farben.
- [x] **T5** (AK-1): Razor Page `/` mit daisyUI-Button „Spielen“, statische Auslieferung `/play` mit SPA-Fallback,
  `/health`.

## Client
- [x] **T6**: Gerüst `client/` mit Vite, React, TS, Tailwind und daisyUI, ESLint, Vitest. Skripte `gen`
  (buf generate) und `dev`/`build`/`test`/`lint`/`typecheck`. Vite-Proxy `/ws` → ASP.NET, Build-Ausgabe nach
  `server/BrowserGame.Server/wwwroot/play`.
- [x] **T7** (AK-6, AK-7): Canvas-Rendering mit 640×360, ganzzahliger Skalierung, Letterbox, Kamera auf eigene
  Figur, Gitter-Hintergrund, Quadrate mit Namen. Vitest für die Skalierungs- und Letterbox-Berechnung.
- [x] **T8** (AK-2, AK-2b): `actions.ts` mit WASD-Standard-Belegung, Pointer Events mit Halten-und-Folgen,
  Totzone, Stopp bei `pointerup`/`pointercancel`/`blur`. Vitest für die Richtungsberechnung.
- [x] **T9** (AK-3, AK-4, AK-5): WebSocket-Client, Fixed-Step 50 ms mit `seq`, Prediction und Reconciliation,
  Interpolation der anderen Spieler, Entfernen fehlender Spieler, HUD (Name, Verbindungsstatus).
  Vitest für die Reconciliation.

## Tests / Prüfung
- [x] **T10** (AK-1, AK-3, AK-4): Integrationstests mit `WebApplicationFactory`, die `/` und `/play` prüfen, sowie
  2 WebSocket-Clients für Sichtbarkeit < 1 s und Verschwinden nach Close.
  Dazu AK-5 über WebSocket sowie ungültige und zu große Nachrichten (`GameServerTests`).

## CI/CD & Betrieb
- [x] **T11**: Multi-Stage-`Dockerfile` (node → dotnet/sdk → aspnet chiseled, non-root), Images per Digest gepinnt,
  `HEALTHCHECK` auf `/health`, `.dockerignore`.
- [x] **T12**: `ci.yml` erweitern und alle neuen Jobs in `ci-ok.needs` eintragen:
  - [x] `server`: `dotnet build`, `dotnet test`; verwundbare Pakete über NuGet Audit + `TreatWarningsAsErrors`
  - [x] `client`: `npm ci`, `gen`, `typecheck`, `lint`, `test`, `build`, `npm audit --audit-level=high`
  - [x] `docker`: Build mit Trivy-Scan (Fail ab `HIGH`); auf `main` Push nach `ghcr.io/heckertobias/browser-game`
    (`sha-<commit>`, `main`) plus `actions/attest-build-provenance`. Nur dieser Job bekommt
    `packages: write`, `attestations: write`, `id-token: write`.
  - [x] Neue Third-Party-Actions (`docker/*`, Trivy) in die Allowlist in `scripts/github-setup.sh` eingetragen,
    Skript ausgeführt, SHAs gepinnt.
  - [x] `CODEQL_LANGUAGES` um `csharp` und `javascript-typescript` erweitert.
  - [x] „Auto-Merge löst keine CI auf `main` aus“: nächtlicher CI-Lauf (`schedule`) auf `main` (ADR 0002).
- [x] **T13**: `dependabot.yml` um `nuget` (`/server`), `npm` (`/client`) und `docker` (`/`) erweitern, mit gleichem
  Schema (wöchentlich, Minor/Patch gruppiert, Cooldown).
- [x] **T14**: README-Abschnitte „Lokal starten“ (Server + Vite-Dev) und „Docker“ (Build, Run, Image aus GHCR).

## Abschluss
- [ ] **T15**: Alle Akzeptanzkriterien geprüft, manuelle Prüfungen hier mit Datum dokumentiert:
  - AK-1, AK-2, AK-2b (Maus + Touch), AK-3 (2 Tabs), AK-6 (≥ 3 Fenstergrößen), AK-7.
  - Danach Spec-Status auf `umgesetzt`.
  - Manuelle Prüfungen (v0.1.0 auf dem Pi, `browsergame.endofweb.internal`):
    - [x] AK-1: Startseite → „Spielen“ öffnet das Spiel (2026-10-24, Tobias)
    - [x] AK-3: zwei Browser sehen sich gegenseitig (2026-10-24, Tobias)
    - [ ] AK-2: WASD, auch diagonal
    - [ ] AK-2b: Halten-und-Folgen mit Maus und Touch
    - [ ] AK-4: Tab schließen → Figur verschwindet beim anderen
    - [ ] AK-6: ≥ 3 Fenstergrößen, gleicher Ausschnitt, Ränder statt mehr Welt
    - [ ] AK-7: in eine Richtung laufen, keine Grenze, Gitter bewegt sich
