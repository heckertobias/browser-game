# 019 – Tasks

Gehört zu: [plan.md](plan.md)

Reihenfolge: Verträge → Server → Karte → Client → Assets → Tests/Prüfung. Jeder Task bringt seine Tests mit.
Erst beginnen, wenn 018 umgesetzt ist.

## Verträge
- [ ] **T1** (AK-1, AK-3): `game.proto`: `MapObject`, `AreaMap.objects` (Feld 7); `buf lint` und `buf breaking` grün.

## Server
- [ ] **T2** (AK-3, AK-7): `MapFile.Parse` liest `objects` (bekannte Arten, Fehlerfälle mit Fundstelle); `AreaMap.Objects`;
  `GameLoop` schickt sie im `Welcome`. xUnit `MapFileTests`; Integration: eigene Karte über `Game:MapPath` → Objekte im
  `Welcome`, Figur läuft über eine Kachel mit Baum.

## Karte
- [ ] **T3** (AK-4): `generate-map.py` verteilt Objekte (Plan-Entscheidung 7); `meadow.json` neu erzeugt (Boden
  unverändert); xUnit `MeadowMapTests` um die Prüfungen aus Entscheidung 6 erweitert.

## Client
- [ ] **T4** (AK-1): `world/objects.ts` (`objectsFromProto`, `objectImage`, `ObjectIndex`, `footY`), Konstante
  `OBJECT_CULL_MARGIN`; Vitest `objects.test.ts`.

## Assets
- [ ] **T5** (AK-1, AK-5, AK-6): Atlas `client/src/assets/tiles/nature.png` und `nature.json` (Baum, Busch, Stein, hohes
  Gras, je mit Fußpunkt) mit `import-sprites.mjs atlas` und `atlas-nature.json` (Pack-Tileset stellt der User bereit);
  Zeile in `LICENSES.md`; `licenses.test.ts` und `assets.test.ts` grün; Vitest: jede Art hat einen Eintrag, Bäume
  höher als `PLAYER_SIZE`, hohes Gras höchstens 10, kein Eintrag ragt weiter als `OBJECT_CULL_MARGIN`.

## Client (Zeichnen)
- [ ] **T6** (AK-1, AK-2, AK-5, AK-6): `render.ts`: Objekte, Ziele und Figuren nach Fußlinie, Namen und HP-Balken
  zuletzt; `Game` baut den `ObjectIndex`, `setNatureAtlas`, Laden im `App`; Vitest `render-objects.test.ts`.

## Tests / Prüfung
- [ ] **T7**: Manuelle Prüfung in der Testumgebung (PC und Handy, zwei Geräte am selben Ort):
  - beide sehen dieselben Objekte an denselben Stellen; Wege, See und Startbereich bleiben frei
  - Figur hinter einem Baum wird von der Krone verdeckt, davor nicht; Namen bleiben lesbar
  - durch hohes Gras laufen: nur die Beine verdeckt
  - durch alle Objekte hindurchlaufen
  - Objekte scharf, Bild flüssig

  Danach alle AKs abhaken, Spec-Status auf `umgesetzt`, Roadmap aktualisieren.
