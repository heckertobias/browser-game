# 021 – Kollision

**Status:** entwurf  <!-- entwurf | bereit | in arbeit | umgesetzt -->

Setzt voraus: [018 – Boden statt Gitter](../018-boden/spec.md) (feste Karte mit Bergen und See),
[019 – Deko-Objekte](../019-deko/spec.md) (blockierende Objekte), [013](../013-kampfsystem/spec.md) und
[014](../014-gegner/spec.md) (Geschosse und Gegner)

## Ziel

Berge, Wasser und blockierende Deko-Objekte lassen sich nicht mehr betreten. Damit hat das Gebiet aus 018 echte
Grenzen, und der See und z. B. Baumstämme sind Hindernisse – für Spieler, Gegner und Geschosse gleichermaßen.

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

- **AK-5:** Gegeben ein Deko-Objekt einer blockierenden Art (z. B. Baumstamm), dann kann ich es nicht betreten;
  Objekte einer nicht blockierenden Art (z. B. hohes Gras) kann ich weiter durchlaufen.
- **AK-6:** Gegeben ein Gegner (014) bewegt sich, dann gelten für ihn dieselben Hindernisse wie für mich.
- **AK-7:** Gegeben ein Fernkampf-Geschoss (013) trifft auf ein Hindernis, dann endet es dort und trifft nichts
  dahinter.

## Out of Scope

- Schwimmen.
- Kollision mit anderen Spielern oder Gegnern.

## Offene Fragen

_Keine._

## Entschiedene Fragen

- **Gleiten:** Läuft die Figur schräg gegen ein Hindernis, gleitet sie daran entlang (AK-3).
- **Ungültige gespeicherte Position:** Die Figur wird beim Betreten des Spiels zum Startpunkt versetzt (AK-4). Das
  gilt für alte Positionen aus der Zeit ohne Karte genauso wie nach späteren Kartenänderungen.
- **Alles gleich mit:** Deko-Objekte blockieren je Objektart schon in dieser Spec (AK-5); Gegner und Geschosse
  unterliegen denselben Hindernissen (AK-6, AK-7). Spieler und Gegner blockieren einander weiterhin nicht.

<!-- Keine Technik in dieser Datei. Das gehört in plan.md. -->
