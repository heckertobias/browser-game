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
| `scripts/` | Hilfsskripte: `github-setup.sh` (Repo-Härtung), `release.sh`, `release-notes.sh` |
| `.github/` | CI-Workflows, Dependabot |
| `deploy/` | Compose für den Betrieb (Version über `GAME_VERSION`) |
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

## Betrieb (Compose)

[`deploy/compose.yaml`](deploy/compose.yaml) ist ein Beispiel für den Betrieb hinter
[nginx-proxy](https://github.com/nginx-proxy/nginx-proxy) (Netz `proxy`); kein Dienst veröffentlicht einen Port.
Wie ausgerollt wird, liegt außerhalb des Repos ([ADR 0012](docs/decisions/0012-deploy-ausserhalb-des-repos.md)).

- `.env` neben der Compose: `GAME_VERSION`, `VIRTUAL_HOST` (Spiel) und `ADMIN_VIRTUAL_HOST` (Admin, nur intern
  auflösen lassen).
- Spiel und Admin teilen sich das Volume `data` (SQLite). Es gehört ins Backup; das Spiel schreibt dafür nachts um
  03:50 eine konsistente Kopie nach `/data/backup/game.db`.
- **Erster Admin:** Beim ersten Start schreibt das Admin-Interface einen einmaligen Einrichtungslink (48 h gültig)
  in sein Log. Wer das Container-Log lesen kann, kann also den ersten Admin anlegen – das ist so gewollt (Betreiber).
  Startet das Admin ohne eingerichteten Admin neu, gibt es einen neuen Link; der alte wird ungültig.
- Mailversand (Bestätigungen, Passwort-Reset, Einladungen) richtet man danach im Admin unter „SMTP“ ein.
- Rollback: eine ältere `GAME_VERSION` ausrollen. Die Datenbank wird nicht zurückgesetzt (kein Downgrade der
  Migrationen).

## Release

Jede Umsetzung läuft auf einem Branch `impl/NNN-name` mit Draft-PR. Auf dem Branch entstehen Release Candidates, auf
`main` das Release:

```bash
scripts/release.sh 0.3.0-rc.1   # vom impl-Branch: Release Candidate für die Testumgebung
scripts/release.sh 0.3.0        # von main, nach dem Merge
scripts/release.sh --dry-run 0.3.0-rc.1   # nur prüfen, nichts tun
```

Das Skript prüft den Stand (sauber, synchron mit `origin`, CI grün) und pusht den signierten Tag. Es wartet, bis die
CI beide Images veröffentlicht und das GitHub Release angelegt hat. Netzwerkfehler beim Abfragen der CI hält es aus.

**Deploy-Hook:** Gibt es eine ausführbare Datei `~/.config/browser-game/deploy` (oder `$BROWSER_GAME_DEPLOY`), rollt
`release.sh` die Version damit aus. Der Hook liegt außerhalb des Repos und wird im Repo-Verzeichnis aufgerufen
(`BROWSER_GAME_REPO` ist gesetzt):

| Aufruf | Wann | Fehler |
|---|---|---|
| `deploy --check` | vor dem Tag | bricht ab, bevor etwas passiert |
| `deploy --dry-run --unreleased X.Y.Z` | `release.sh --dry-run` | – |
| `deploy X.Y.Z` | nach CI und GitHub Release | Release bleibt; Hook erneut aufrufen |

Ohne Hook endet `release.sh` nach dem Veröffentlichen.

- `v0.3.0` veröffentlicht `:0.3.0` und `:latest`; das Release wird nur von `main` aus gebaut.
- `v0.3.0-rc.1` veröffentlicht nur `:0.3.0-rc.1` und ein GitHub Release als Vorabversion. Bei einem Fehler folgt
  `-rc.2`.

Die Release-Notes entstehen aus den Commits seit dem letzten echten Release (RCs zählen nicht), gruppiert nach Typ
(`feat`, `fix`, `docs`, …); Vorschau lokal: `scripts/release-notes.sh v0.3.0` (der Tag muss lokal existieren).

## Repo-Regeln

Details und Begründung: [ADR 0002](docs/decisions/0002-repo-und-ci-cd.md), [ADR 0009](docs/decisions/0009-spec-branches-rc-und-deploy-per-api.md), [ADR 0011](docs/decisions/0011-branch-namen.md), [ADR 0012](docs/decisions/0012-deploy-ausserhalb-des-repos.md)

- Gearbeitet wird auf Branches mit PR: `spec/NNN-name` (Spec schreiben, Squash), `impl/NNN-name` (Umsetzung,
  Draft-PR, Merge-Commit), `chore/kurzer-name` (Tooling/Doku, Squash);
  `main` nimmt nur PRs mit grüner CI. Commits müssen **signiert** sein.
- Issues und Pull Requests können nur Collaborators eröffnen.
- Dependabot-Updates laufen als PR durch die CI; Patch- und Minor-Updates werden automatisch gemergt, Updates ohne wirksamen Cooldown (z. B. Digests) erst nach 7 Tagen (Label `wartet-7-tage`), Sicherheitsupdates (außer Major) sofort.
- Sicherheitslücken bitte vertraulich melden: [SECURITY.md](SECURITY.md)
