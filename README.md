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
| `server/` | C# / ASP.NET Core (Spielserver, Razor-Landing) |
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
```

Die Startseite unter `http://localhost:5080/` verlinkt auf `/play`; das funktioniert dort erst nach
`npm run build` (der Build landet in `server/BrowserGame.Server/wwwroot`).

Tests: `dotnet test` in `server/`, `npm test` in `client/`.

## Docker

```bash
docker build -t browser-game .
docker run --rm -p 8080:8080 --read-only --cap-drop=ALL --security-opt no-new-privileges browser-game
```

Die CI veröffentlicht jedes `main` als `ghcr.io/heckertobias/browser-game:main` (und `:sha-<commit>`),
inklusive Build-Provenance-Attestation:

```bash
gh attestation verify oci://ghcr.io/heckertobias/browser-game:main --owner heckertobias
```

## Repo-Regeln

Details und Begründung: [ADR 0002](docs/decisions/0002-repo-und-ci-cd.md)

- Gearbeitet wird direkt auf `main`; Commits müssen **signiert** sein.
- Issues und Pull Requests können nur Collaborators eröffnen.
- Dependabot-Updates laufen als PR durch die CI; Patch- und Minor-Updates werden automatisch gemergt.
- Sicherheitslücken bitte vertraulich melden: [SECURITY.md](SECURITY.md)
