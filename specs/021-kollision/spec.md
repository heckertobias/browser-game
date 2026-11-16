# 021 – Kollision

**Status:** entwurf  <!-- entwurf | bereit | in arbeit | umgesetzt -->

Setzt voraus: [018 – Boden statt Gitter](../018-boden/spec.md) (feste Karte mit Bergen und See)

## Ziel

Berge und Wasser lassen sich nicht mehr betreten. Damit hat das Gebiet aus 018 echte Grenzen, und der See ist ein
Hindernis.

## User Stories

- Als **Spieler** möchte ich, dass Berge und Wasser mich aufhalten, damit sich die Welt fest und glaubwürdig anfühlt.

## Akzeptanzkriterien

- **AK-1:** Gegeben ich laufe auf einen Berg oder ins Wasser zu, dann bleibt meine Figur am Rand stehen und betritt
  die Fläche nicht.
- **AK-2:** Gegeben ich bin im Spiel, dann kann ich das Gebiet nicht über den Bergrand verlassen.
  ([001](../001-walking-skeleton/spec.md) AK-7 „keine Grenze“ wird damit angepasst.)
- **AK-3:** Gegeben ich laufe schräg gegen ein Hindernis, dann gleitet meine Figur am Hindernis entlang, statt
  stehen zu bleiben.
- **AK-4:** Gegeben meine gespeicherte Position (003) liegt auf einer nicht begehbaren Fläche (Berg, Wasser oder
  jenseits des Kartenrands), wenn ich das Spiel betrete, dann stehe ich am Startpunkt.

## Out of Scope

- Schwimmen.
- Kollision mit anderen Spielern oder Gegnern.

## Offene Fragen

- [ ] Blockieren Deko-Objekte ([019](../019-deko/spec.md)) schon in dieser Spec (je Objektart), oder erst in einer
  Folge-Spec?
- [ ] Gilt die Kollision auch für Gegner (014) und Fernkampf-Geschosse (013)?

## Entschiedene Fragen

- **Gleiten:** Läuft die Figur schräg gegen ein Hindernis, gleitet sie daran entlang (AK-3).
- **Ungültige gespeicherte Position:** Die Figur wird beim Betreten des Spiels zum Startpunkt versetzt (AK-4). Das
  gilt für alte Positionen aus der Zeit ohne Karte genauso wie nach späteren Kartenänderungen.

<!-- Keine Technik in dieser Datei. Das gehört in plan.md. -->
