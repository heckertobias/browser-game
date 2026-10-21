# ADR 0001 – Tech-Stack

**Status:** akzeptiert

## Kontext

Echtzeit-MMO im Browser mit öffentlichen, schnell ladenden Seiten. Typen sollen zwischen Server
und Client nicht doppelt gepflegt werden.

## Entscheidung

- **Server:** C# / ASP.NET Core
- **Öffentliche Seiten:** Razor (schneller erster Seitenaufbau, kein React-SSR)
- **Client:** TypeScript, React für UI, Canvas für die Spielwelt
- **UI-Framework:** daisyUI, gemeinsam für Spiel-UI und Landing
- **Verträge:** OpenAPI (HTTP) und Protobuf (WebSocket) als einzige Typquelle, Code wird generiert

## Konsequenzen

- Ein Codegen-Schritt im Build ist Pflicht.
- React-SSR / Server Components fallen weg.
- Razor-Seiten und React-Client teilen sich nur das Styling (daisyUI/Tailwind), keine Komponenten.
