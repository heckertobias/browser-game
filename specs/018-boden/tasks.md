# 018 – Tasks

Gehört zu: [plan.md](plan.md)

Reihenfolge: Assets → Client → Tests/Prüfung. Kein Vertrag, kein Server (Variante A, siehe Plan).
Erst beginnen, wenn die offenen Fragen beantwortet sind und 017 umgesetzt ist. Tasks mit ⚠ hängen an einer Antwort.

## Assets
- [ ] **T1** (AK-1): Boden-Tileset als `client/src/assets/ground.png` (unverändert aus dem Pack), Zeile in
  `LICENSES.md`; `licenses.test.ts` grün. Zellen für Gras, Varianten und die 13 Wegformen bestimmen.

## Client
- [ ] **T2** (AK-1): `world/hash.ts`, `world/ground.ts` (`groundAt`, `pathShape`, `groundCell`, `visibleTiles`),
  Konstanten; Vitest. ⚠ F-1, F-3, F-5
- [ ] **T3** (AK-1, AK-2): `render.ts` zeichnet Kacheln statt Gitter (Gitter als Rückfall); `Game.setGroundTiles`,
  Laden im `App`; Vitest `render-ground.test.ts` (Abdeckung ohne Lücke für die Sichtfelder aus ADR 0010).

## Tests / Prüfung
- [ ] **T4**: Manuelle Prüfung in der Testumgebung (PC mit DPR 1, 1,25 und 2, Handy hochkant/quer):
  - Boden statt Gitter, Platz am Start, Wege auf den Achsen
  - beim Laufen keine Säume oder Linien zwischen Kacheln, scharf
  - mit 017: Bild bleibt flüssig (sonst Block-Cache laut Risiken)

  Danach alle AKs abhaken, Spec-Status auf `umgesetzt`, Roadmap aktualisieren.
