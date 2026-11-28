# 029 – Größere Figur auf dem Handy

**Status:** entwurf  <!-- entwurf | bereit | in arbeit | umgesetzt -->

Setzt voraus: [017 – Spielfigur als Pixel-Art](../017-spielfigur/spec.md) (Figur als Pixel-Art)

## Ziel

Beim Test von 017 wirkte die Figur auf dem Smartphone zu klein. Auf dem Handy soll sie gut erkennbar sein, ohne dass
das Sichtfeld zu eng wird und ohne dass sie unscharf wird.

## User Stories

- Als **Spieler am Handy** möchte ich meine Figur und die anderen gut erkennen, damit ich sehe, wer wohin läuft und
  schaut.

## Akzeptanzkriterien

- **AK-1:** Gegeben ich spiele am Handy (hochkant oder quer), dann ist die Figur gut erkennbar. Das genaue Maß legt
  der Plan fest.
- **AK-2:** Gegeben die Figur wird größer gezeichnet, dann bleibt sie scharf (wie 017 AK-3).
- **AK-3:** Gegeben die Figur wird größer, dann passen Name, HP-Balken und Treffer-Hilfen weiter zu ihr.

## Out of Scope

- Neue Grafiken oder ein anderes Raster.

## Offene Fragen

- Soll die Figur größer werden, das Sichtfeld kleiner (Zoom, [ADR 0010](../../docs/decisions/0010-sichtfeld-kurze-seite.md)) oder beides?
- Nur auf dem Handy oder überall?
- Ändert sich damit die Trefferfläche im Kampf (013) oder nur die Darstellung?

<!-- Keine Technik in dieser Datei. Das gehört in plan.md. -->
