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
- [ ] **T4** (AK-3, AK-4): `/ws`-Endpoint, `GameLoop` (20 Hz) mit `Welcome` und `Snapshot` pro Empfänger. Bei
  Disconnect wird der Spieler entfernt. Frame-Limit 1 KB, zufällige Namen und Farben.
- [ ] **T5** (AK-1): Razor Page `/` mit daisyUI-Button „Spielen“, statische Auslieferung `/play` mit SPA-Fallback,
  `/health`.

## Client
- [ ] **T6**: Gerüst `client/` mit Vite, React, TS, Tailwind und daisyUI, ESLint, Vitest. Skripte `gen`
  (buf generate) und `dev`/`build`/`test`/`lint`/`typecheck`. Vite-Proxy `/ws` → ASP.NET, Build-Ausgabe nach
  `server/BrowserGame.Server/wwwroot/play`.
- [ ] **T7** (AK-6, AK-7): Canvas-Rendering mit 640×360, ganzzahliger Skalierung, Letterbox, Kamera auf eigene
  Figur, Gitter-Hintergrund, Quadrate mit Namen. Vitest für die Skalierungs- und Letterbox-Berechnung.
- [ ] **T8** (AK-2, AK-2b): `actions.ts` mit WASD-Standard-Belegung, Pointer Events mit Halten-und-Folgen,
  Totzone, Stopp bei `pointerup`/`pointercancel`/`blur`. Vitest für die Richtungsberechnung.
- [ ] **T9** (AK-3, AK-4, AK-5): WebSocket-Client, Fixed-Step 50 ms mit `seq`, Prediction und Reconciliation,
  Interpolation der anderen Spieler, Entfernen fehlender Spieler, HUD (Name, Verbindungsstatus).
  Vitest für die Reconciliation.

## Tests / Prüfung
- [ ] **T10** (AK-1, AK-3, AK-4): Integrationstests mit `WebApplicationFactory`, die `/` und `/play` prüfen, sowie
  2 WebSocket-Clients für Sichtbarkeit < 1 s und Verschwinden nach Close.

## CI/CD & Betrieb
- [ ] **T11**: Multi-Stage-`Dockerfile` (node → dotnet/sdk → aspnet chiseled, non-root), Images per Digest gepinnt,
  `HEALTHCHECK` auf `/health`, `.dockerignore`.
- [ ] **T12**: `ci.yml` erweitern und alle neuen Jobs in `ci-ok.needs` eintragen:
  - `server`: `dotnet build`, `dotnet test`, `dotnet list package --vulnerable` (schlägt bei Treffer fehl)
  - `client`: `npm ci`, `gen`, `typecheck`, `lint`, `test`, `build`, `npm audit --audit-level=high`
  - `docker`: Build mit Trivy-Scan (Fail ab `HIGH`); auf `main` Push nach `ghcr.io/heckertobias/browser-game`
    (`sha-<commit>`, `main`) plus `actions/attest-build-provenance`. Nur dieser Job bekommt
    `packages: write`, `attestations: write`, `id-token: write`.
  - Neue Third-Party-Actions (`docker/*`, Trivy) in die Allowlist in `scripts/github-setup.sh` eintragen,
    das Skript ausführen und SHAs pinnen.
  - In `scripts/github-setup.sh` `CODEQL_LANGUAGES` um `csharp` und `javascript-typescript` erweitern, Skript ausführen.
  - Lösung für „Auto-Merge löst keine CI auf `main` aus“ (ADR 0002) festlegen und umsetzen.
- [ ] **T13**: `dependabot.yml` um `nuget` (`/server`), `npm` (`/client`) und `docker` (`/`) erweitern, mit gleichem
  Schema (wöchentlich, Minor/Patch gruppiert, Cooldown).
- [ ] **T14**: README-Abschnitte „Lokal starten“ (Server + Vite-Dev) und „Docker“ (Build, Run, Image aus GHCR).

## Abschluss
- [ ] **T15**: Alle Akzeptanzkriterien geprüft, manuelle Prüfungen hier mit Datum dokumentiert:
  - AK-1, AK-2, AK-2b (Maus + Touch), AK-3 (2 Tabs), AK-6 (≥ 3 Fenstergrößen), AK-7.
  - Danach Spec-Status auf `umgesetzt`.
