# 019 – Tasks

Gehört zu: [plan.md](plan.md)

Reihenfolge: Assets → Client → Tests/Prüfung. Kein Vertrag, kein Server (Variante A, siehe Plan).
Erst beginnen, wenn die offenen Fragen beantwortet sind und 018 umgesetzt ist. Tasks mit ⚠ hängen an einer Antwort.

## Assets
- [ ] **T1** (AK-1): Natur-Tileset als `client/src/assets/nature.png` (unverändert aus dem Pack), Zeile in
  `LICENSES.md`; `licenses.test.ts` grün. Quellrechtecke für Baum, Busch, Stein bestimmen.

## Client
- [ ] **T2** (AK-1): `world/decorations.ts` (`decorationsInChunk`, `decorationsInView`, `DECORATION_KINDS`),
  Konstanten; Vitest. ⚠ F-3
- [ ] **T3** (AK-1, AK-2): `render.ts`: Objekte zeichnen, gemeinsame Sortierung mit Figuren und Zielen nach Fußlinie,
  Namen zuletzt; `Game.setDecorationTiles`, Laden im `App`; Vitest `render-deco.test.ts`. ⚠ F-2

## Tests / Prüfung
- [ ] **T4**: Manuelle Prüfung in der Testumgebung (PC und Handy, zwei Geräte am selben Ort):
  - beide sehen dieselben Objekte an denselben Stellen; Wege und Platz bleiben frei
  - Figur hinter einem Baum wird verdeckt, davor nicht; Namen bleiben lesbar
  - Objekte scharf, Bild flüssig

  Danach alle AKs abhaken, Spec-Status auf `umgesetzt`, Roadmap aktualisieren.
