# 027 – Importgrenzen auch bei `./../`-Pfaden

**Status:** entwurf  <!-- entwurf | bereit | in arbeit | umgesetzt -->

Gehört zu: [docs/project-structure.md](../../docs/project-structure.md) (Importgrenzen im Client)

## Ziel

Die Importgrenzen im Client verbieten relative Importe über Ordnergrenzen (`../…`). Ein Pfad wie `./../core/constants`
fällt aber nicht unter diese Regel. So kann Code eine Grenze überschreiten, ohne dass der Linter das meldet. Das gilt
in allen Features. Aufgefallen ist es beim Review von 017 T4.

## User Stories

- Als **Entwickler oder Agent** möchte ich, dass der Linter jeden Import über eine Ordnergrenze meldet, egal wie der
  Pfad geschrieben ist, damit die Grenzen aus `docs/project-structure.md` wirklich gelten.

## Akzeptanzkriterien

- **AK-1:** Gegeben ein Import mit einem Pfad, der über den eigenen Ordner hinaus nach oben führt (z. B. `./../x` oder
  `./a/../../x`), dann meldet der Linter ihn wie `../x`.
- **AK-2:** Gegeben ein Import innerhalb des eigenen Ordners (`./x`, `./a/x`), dann meldet der Linter nichts.

## Out of Scope

- Neue Grenzen zwischen Features oder Ordnern.

## Offene Fragen

- Reicht ein weiteres Muster in `no-restricted-imports`, oder braucht es eine Regel, die Pfade normalisiert?
- Gibt es heute schon Importe dieser Form im Code?

<!-- Keine Technik in dieser Datei. Das gehört in plan.md. -->
