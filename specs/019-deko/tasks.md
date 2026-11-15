# 019 – Tasks

Gehört zu: [plan.md](plan.md)

Reihenfolge: Assets → Client → Tests/Prüfung. Kein Vertrag, kein Server (Variante A, siehe Plan).
Erst beginnen, wenn die offenen Fragen beantwortet sind und 018 umgesetzt ist. Tasks mit ⚠ hängen an einer Antwort.

## Assets
- [ ] **T1** (AK-1): Atlas `client/src/assets/tiles/nature.png` und `nature.json` (Baum, Busch, Stein, je mit
  Fußpunkt) mit `import-sprites.mjs atlas` und `atlas-nature.json` (Pack-Tileset stellt der User bereit); Zeile in
  `LICENSES.md`; `licenses.test.ts` und `assets.test.ts` grün.

## Client
- [ ] **T2** (AK-1): `world/decorations.ts` (`decorationsInChunk`, `decorationsInView`, `DECORATION_WEIGHTS`),
  Konstanten; Vitest inkl. „jede Art steht im Atlas“. ⚠ F-3
- [ ] **T3** (AK-1, AK-2): `render.ts`: Objekte zeichnen, gemeinsame Sortierung mit Figuren und Zielen nach Fußlinie,
  Namen zuletzt; `Game.setDecorations`, Laden im `App`; Vitest `render-deco.test.ts`. ⚠ F-2

## Tests / Prüfung
- [ ] **T4**: Manuelle Prüfung in der Testumgebung (PC und Handy, zwei Geräte am selben Ort):
  - beide sehen dieselben Objekte an denselben Stellen; Wege und Platz bleiben frei
  - Figur hinter einem Baum wird verdeckt, davor nicht; Namen bleiben lesbar
  - Objekte scharf, Bild flüssig

  Danach alle AKs abhaken, Spec-Status auf `umgesetzt`, Roadmap aktualisieren.
