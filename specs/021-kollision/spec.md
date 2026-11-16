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

## Out of Scope

- Schwimmen.
- Kollision mit Deko-Objekten ([019](../019-deko/spec.md)), anderen Spielern oder Gegnern.

## Offene Fragen

- [ ] Gleitet die Figur an einem Hindernis entlang, wenn sie schräg dagegen läuft, oder bleibt sie ganz stehen?
- [ ] Was passiert mit Figuren, deren gespeicherte Position (003) in einem Berg oder im See liegt?
- [ ] Gilt die Kollision auch für Gegner (014) und Fernkampf-Geschosse (013)?

<!-- Keine Technik in dieser Datei. Das gehört in plan.md. -->
