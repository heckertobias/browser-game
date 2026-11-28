# 018 – Tasks

Gehört zu: [plan.md](plan.md)

Reihenfolge: Verträge → Server (Karte) → Client → Assets → Tests/Prüfung. Jeder Task bringt seine Tests mit.
Erst beginnen, wenn 017 umgesetzt ist.

## Verträge
- [x] **T1** (AK-3, AK-6): `game.proto`: `GroundKind`, `AreaMap`, `Welcome.area` (Feld 10); `buf lint` und
  `buf breaking` grün; generierte Typen in Client und Server.

## Server
- [x] **T2** (AK-5, AK-6): `Map/Domain/AreaMap.cs` (`KindAt` mit Klemmen, `TileOf`) und `Map/Domain/MapFile.cs`
  (`Parse`, `MapFormatException`); xUnit `AreaMapTests`, `MapFileTests` (jeder Fehlerfall mit Fundstelle).
- [x] **T3** (AK-4): `scripts/generate-map.py` nach Plan-Entscheidung 11; daraus
  `server/BrowserGame.Server/Features/Game/Map/Maps/meadow.json`; `.csproj` kopiert sie in Ausgabe und Publish;
  xUnit `MeadowMapTests` (Größe, `origin`, Bergrand, Weg, See, Startkachel, Regeln aus Entscheidung 5).
- [ ] **T4** (AK-3, AK-6): `Map/Infrastructure/MapLoader.cs` (Datei oder `Game:MapPath`, ungültig → Start scheitert),
  Registrierung in `GameSetup`; `GameLoop` setzt `Welcome.area`. Integrationstests: zwei Clients bekommen dasselbe
  `area`; eigene Datei über `Game:MapPath`; ungültige Datei → Host startet nicht. Image-Build prüfen: Datei liegt im
  Container.

## Client
- [ ] **T5** (AK-1, AK-3, AK-5): `world/areaMap.ts` (`areaMapFromProto`, `kindAt`), `world/hash.ts`,
  `world/groundTiles.ts` (`edgeShape`, `groundTile`, `visibleTiles`, `GROUND_ENTRIES`), Konstanten; Vitest
  `areaMap.test.ts`, `groundTiles.test.ts`.
- [ ] **T6** (AK-1): `sprites/atlas.ts` (`parseAtlas`, `group`); Vitest `atlas.test.ts`.

## Assets
- [ ] **T7** (AK-1): Modus `atlas` in `import-sprites.mjs` mit `atlas-ground.json`; daraus
  `client/src/assets/tiles/ground.png` und `ground.json` (Gras, Varianten, je 13 Weg- und Wasserformen, Berg;
  Pack-Tilesets stellt der User bereit); Zeile in `LICENSES.md`; `assets.test.ts` prüft den Atlas; Vitest „jeder Name
  aus `GROUND_ENTRIES` steht im Atlas“.

## Client (Zeichnen)
- [ ] **T8** (AK-1, AK-2, AK-5, AK-7): `render.ts` zeichnet Kacheln statt Gitter (Gitter als Rückfall) und die
  Startmarkierung; `Game` übernimmt die Karte aus dem `Welcome`, `setGroundAtlas`, Laden im `App`; Vitest
  `render-ground.test.ts` (Abdeckung ohne Lücke für die Sichtfelder aus ADR 0010, Sichtfeld über den Kartenrand,
  Startmarkierung) und `game`-Test (Karte aus dem `Welcome` landet in den Render-Optionen).

## Tests / Prüfung
- [ ] **T9**: Manuelle Prüfung in der Testumgebung (PC mit DPR 1, 1,25 und 2, Handy hochkant/quer, zwei Geräte):
  - Boden statt Gitter: Gras mit Varianten, Wegekreuz durch den Start, Weg zum See, See, Bergrand
  - beim Laufen keine Säume oder Linien zwischen Kacheln, scharf
  - an den Kartenrand und darüber hinaus laufen: Berge gehen weiter, nie leere Fläche
  - Startmarkierung gut sichtbar und klar als Testhilfe erkennbar
  - zwei Geräte am selben Ort sehen denselben Boden, auch nach Server-Neustart
  - mit 017: Bild bleibt flüssig (sonst Block-Cache laut Risiken)

  Danach alle AKs abhaken, Spec-Status auf `umgesetzt`, Roadmap aktualisieren.
