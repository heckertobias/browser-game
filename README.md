# Browser Game

[![CI](https://github.com/heckertobias/browser-game/actions/workflows/ci.yml/badge.svg)](https://github.com/heckertobias/browser-game/actions/workflows/ci.yml)

2D-Pixel-Art-MMORPG im Browser: frei begehbare Welt mit Echtzeit-Bewegung und Tab-Targeting-Kampf –
und gleichzeitig ein Browsergame, das weiterläuft, wenn man nicht online ist.

## Arbeitsweise: Spec-Driven Development

Code entsteht hier **nie direkt aus einer Idee**, sondern immer über eine Spec.
Die Specs in `specs/` sind die Quelle der Wahrheit – Code ist ihre Umsetzung.

```
Idee → spec.md (WAS & WARUM) → plan.md (WIE) → tasks.md (Schritte) → Code → Spec abhaken
```

Details: [`specs/README.md`](specs/README.md)

## Struktur

| Pfad | Inhalt |
|---|---|
| `specs/constitution.md` | Grundprinzipien, gelten für jedes Feature |
| `specs/000-vision/` | Gesamtvision des Spiels |
| `specs/NNN-feature/` | Ein Ordner pro Feature: `spec.md`, `plan.md`, `tasks.md` |
| `specs/_template/` | Vorlagen für neue Features |
| `contracts/` | Maschinenlesbare Verträge: OpenAPI (HTTP), Protobuf (WebSocket) |
| `docs/decisions/` | Architekturentscheidungen (ADRs) |
| `scripts/` | Hilfsskripte, z. B. `github-setup.sh` (Repo-Härtung) |
| `.github/` | CI-Workflows, Dependabot |
| `deploy/` | Compose für den Betrieb (Vorlage + erzeugte Datei) |
| `server/` | C# / ASP.NET Core: Spielserver, Admin-Interface, gemeinsame Datenschicht |
| `client/` | TypeScript, React, Canvas, daisyUI |

## Tech-Stack

- **Server:** C# / ASP.NET Core, Razor für öffentliche Seiten
- **Client:** TypeScript, React (UI), Canvas (Spielwelt), daisyUI
- **Verträge:** OpenAPI für HTTP, Protobuf für WebSocket – Typen werden nur dort definiert und generiert

## Lokal starten

Voraussetzungen: .NET 10 SDK, Node 24.

```bash
# Terminal 1 – Spielserver (http://localhost:5080)
dotnet run --project server/BrowserGame.Server

# Terminal 2 – Client mit Hot Reload (http://localhost:5173/play/, /ws wird an den Server weitergeleitet)
cd client && npm ci && npm run dev

# Optional, Terminal 3 – Admin-Interface (http://localhost:5090), nutzt dieselbe SQLite-Datei
dotnet run --project server/BrowserGame.Admin
```

Die Startseite unter `http://localhost:5080/` verlinkt auf `/play`; das funktioniert dort erst nach
`npm run build` (der Build landet in den `wwwroot`-Ordnern von Spiel und Admin).
Ohne SMTP-Einstellungen landen Mail-Links in der Entwicklung im Log des Spiels.

Tests: `dotnet test` in `server/` (SQLite; mit `TEST_DB_PROVIDER=Postgres` und `TEST_POSTGRES=Host=…;Username=…;Password=…`
gegen PostgreSQL), `npm test` in `client/`.

## Docker

Ein Dockerfile, zwei Images (ADR 0006), beide für **amd64 und arm64** (z. B. Raspberry Pi 3/4/5 mit 64-Bit-OS):

```bash
docker build --target game  -t browser-game .        # Spiel (Standardziel)
docker build --target admin -t browser-game-admin .  # Admin-Interface
```

Beide laufen read-only und ohne Capabilities; Daten liegen im Volume `/data` (SQLite), `App__PublicUrl` ist Pflicht:

```bash
docker run -d -p 8080:8080 --read-only --cap-drop=ALL --security-opt no-new-privileges \
  -v browsergame-data:/data -e App__PublicUrl=http://localhost:8080 browser-game
```

Veröffentlichte Images: `ghcr.io/heckertobias/browser-game:<version>` und `ghcr.io/heckertobias/browser-game-admin:<version>`,
jeweils mit Build-Provenance-Attestation:

```bash
gh attestation verify oci://ghcr.io/heckertobias/browser-game:<version> --owner heckertobias
```

## Betrieb (Compose / Arcane)

[`deploy/compose.yaml`](deploy/compose.yaml) ist das, was läuft – sie wird von `scripts/release.sh` aus
[`deploy/compose.template.yaml`](deploy/compose.template.yaml) erzeugt. Strukturänderungen (neue Dienste, Variablen,
Volumes) gehören in die **Vorlage**; live gehen sie mit dem nächsten Release, wenn die passenden Images existieren.

- Betrieb hinter nginx-proxy (Netz `proxy`); kein Dienst veröffentlicht einen Port.
- `.env` des Projekts: `VIRTUAL_HOST` (Spiel) und `ADMIN_VIRTUAL_HOST` (Admin, nur intern auflösen lassen).
- Spiel und Admin teilen sich das Volume `data` (SQLite). Es gehört ins Backup; das Spiel schreibt dafür nachts um
  03:50 eine konsistente Kopie nach `/data/backup/game.db`.
- **Erster Admin:** Beim ersten Start schreibt das Admin-Interface einen einmaligen Einrichtungslink (48 h gültig)
  in sein Log. Wer das Container-Log lesen kann, kann also den ersten Admin anlegen – das ist so gewollt (Betreiber).
  Startet das Admin ohne eingerichteten Admin neu, gibt es einen neuen Link; der alte wird ungültig.
- Mailversand (Bestätigungen, Passwort-Reset, Einladungen) richtet man danach im Admin unter „SMTP“ ein.

## Release

```bash
scripts/release.sh 0.2.0
```

Das Skript prüft den Stand (sauber, `main`, synchron, CI grün) und pusht den signierten Tag `v0.2.0`. Es wartet,
bis die CI beide Images (`:0.2.0` + `:latest`) veröffentlicht und das GitHub Release angelegt hat, und erzeugt erst
dann `deploy/compose.yaml` aus der Vorlage. Git Sync in Arcane rollt sie anschließend aus.

Die Release-Notes entstehen aus den Commits seit dem letzten Tag, gruppiert nach Typ (`feat`, `fix`, `docs`, …);
Vorschau lokal: `scripts/release-notes.sh v0.2.0` (der Tag muss lokal existieren).

## Repo-Regeln

Details und Begründung: [ADR 0002](docs/decisions/0002-repo-und-ci-cd.md)

- Gearbeitet wird direkt auf `main`; Commits müssen **signiert** sein.
- Issues und Pull Requests können nur Collaborators eröffnen.
- Dependabot-Updates laufen als PR durch die CI; Patch- und Minor-Updates werden automatisch gemergt.
- Sicherheitslücken bitte vertraulich melden: [SECURITY.md](SECURITY.md)
