# Browser Game

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
| `server/` | C# / ASP.NET Core (Spielserver, Razor-Landing) |
| `client/` | TypeScript, React, Canvas, daisyUI |

## Tech-Stack

- **Server:** C# / ASP.NET Core, Razor für öffentliche Seiten
- **Client:** TypeScript, React (UI), Canvas (Spielwelt), daisyUI
- **Verträge:** OpenAPI für HTTP, Protobuf für WebSocket – Typen werden nur dort definiert und generiert
