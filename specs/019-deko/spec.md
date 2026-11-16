# 019 – Deko-Objekte

**Status:** entwurf  <!-- entwurf | bereit | in arbeit | umgesetzt -->

Setzt voraus: [018 – Boden statt Gitter](../018-boden/spec.md)

## Ziel

In der Welt stehen **Objekte** wie Bäume, Steine oder Büsche. Grafiken stammen aus Ninja Adventure oder
folgen seinem Stil und liegen im eigenen Format aus 017: Jedes Objekt hat einen Namen und eine Beschreibung,
unabhängig vom Aufbau des Packs.

## User Stories

- Als **Spieler** möchte ich Bäume, Steine und Ähnliches sehen, damit die Welt belebt wirkt.
- Als **Spieler** möchte ich mich an markanten Objekten orientieren können.

## Akzeptanzkriterien

- **AK-1:** Gegeben ich bin im Spiel, dann sehe ich an festen Stellen Deko-Objekte; alle Spieler sehen sie
  an denselben Stellen.
- **AK-2:** Gegeben ein beliebiger Bildschirm (007), dann sind die Objekte scharf dargestellt.
- **AK-3:** Gegeben die Karte aus [018](../018-boden/spec.md), dann stehen die Objekte in derselben bearbeitbaren
  Kartenbeschreibung; eine geänderte Beschreibung ändert die Objekte im Spiel, ohne Codeänderung.
- **AK-4:** Gegeben das erste Gebiet, dann stehen dort Bäume und Büsche, Wege und See bleiben frei.
- **AK-5:** Gegeben eine Figur steht hinter einem ganz verdeckenden Objekt (z. B. oberhalb eines Baums), dann wird
  sie davon verdeckt; steht sie davor, ist sie vor dem Objekt zu sehen. Namen über Figuren bleiben immer sichtbar.
- **AK-6:** Gegeben eine Figur steht in einem teilweise verdeckenden Objekt (z. B. hohes Gras), dann ist nur ihr
  unterer Teil verdeckt.
- **AK-7:** Gegeben ein beliebiges Objekt, dann kann ich durch es hindurchlaufen.

## Out of Scope

- Objekte, mit denen man interagiert (ernten, öffnen, zerstören).
- **Animationen** (wiegendes Gras, Fackeln, Wellen): kommen später, auch für den Boden.
- **Kollision** mit Objekten: Jede Objektart kann später blockieren (z. B. Baumstamm ja, Grasbüschel nein); das
  Blockieren selbst kommt mit [021](../021-kollision/spec.md) oder einer Folge-Spec.
- Editor für die Karte (später).

## Offene Fragen

_Keine._

## Entschiedene Fragen

- **Durchlaufen im ersten Lauf**, aber Objekte sollen später je Objektart blockieren können.
- **Verdeckung je Objektart:** ganz (nach Tiefe, z. B. Baumkrone) oder teilweise (z. B. hohes Gras über den Beinen).
- **Positionen:** in der Kartenbeschreibung aus 018; im ersten Gebiet einmal nach einem Schema verteilt, später per
  Editor änderbar.
- **Keine Animationen** im ersten Lauf.

<!-- Keine Technik in dieser Datei. Das gehört in plan.md. -->
