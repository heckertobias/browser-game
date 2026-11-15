# 018 – Tasks

Gehört zu: [plan.md](plan.md)

Reihenfolge: Assets → Client → Tests/Prüfung. Kein Vertrag, kein Server (Variante A, siehe Plan).
Erst beginnen, wenn die offenen Fragen beantwortet sind und 017 umgesetzt ist. Tasks mit ⚠ hängen an einer Antwort.

## Assets
- [ ] **T1** (AK-1): `sprites/atlas.ts` (`parseAtlas`, `group`); Modus `atlas` in `import-sprites.mjs` mit
  `atlas-ground.json`; daraus `client/src/assets/tiles/ground.png` und `ground.json` (Gras, Varianten, 13 Wegformen;
  Pack-Tileset stellt der User bereit); Zeile in `LICENSES.md`; `assets.test.ts` prüft den Atlas; Vitest
  `atlas.test.ts`.

## Client
- [ ] **T2** (AK-1): `world/hash.ts`, `world/ground.ts` (`groundAt`, `pathShape`, `groundTile`, `visibleTiles`,
  `GROUND_ENTRIES`), Konstanten; Vitest inkl. „jeder Name steht im Atlas“. ⚠ F-1, F-3, F-5
- [ ] **T3** (AK-1, AK-2): `render.ts` zeichnet Kacheln statt Gitter (Gitter als Rückfall); `Game.setGround`,
  Laden im `App`; Vitest `render-ground.test.ts` (Abdeckung ohne Lücke für die Sichtfelder aus ADR 0010).

## Tests / Prüfung
- [ ] **T4**: Manuelle Prüfung in der Testumgebung (PC mit DPR 1, 1,25 und 2, Handy hochkant/quer):
  - Boden statt Gitter, Platz am Start, Wege auf den Achsen
  - beim Laufen keine Säume oder Linien zwischen Kacheln, scharf
  - mit 017: Bild bleibt flüssig (sonst Block-Cache laut Risiken)

  Danach alle AKs abhaken, Spec-Status auf `umgesetzt`, Roadmap aktualisieren.
