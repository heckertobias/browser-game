# 020 – Tasks

Gehört zu: [plan.md](plan.md)

Reihenfolge: Assets → Client → Tests/Prüfung. Kein Vertrag, kein Server-Code.
Erst beginnen, wenn die offenen Fragen beantwortet sind. Tasks mit ⚠ hängen an einer Antwort.

## Assets
- [ ] **T1** (AK-2): Schrift wählen und ihren Zeichenvorrat prüfen (⚠ F-3); `client/src/assets/<schrift>.ttf`, Zeile
  in `LICENSES.md`; `licenses.test.ts` grün. Fehlen ADR 0016 und Register aus 017 noch, hier anlegen.

## Client
- [ ] **T2** (AK-1, AK-2): `render/pixelFont.ts`; Namen im Canvas nach Plan-Entscheidung 2; `fakeContext.ts` additiv
  (`measureText`); Laden im `App`; Vitest `render-names.test.ts`, `pixelFont.test.ts`.
- [ ] **T3** (AK-2): `global.css` `@font-face` und `--font-mono`; Textgrößen in HUD, Chat, Menü, Tastenbelegung.
  ⚠ F-1, F-4
- [ ] **T4** (AK-2): Razor-Seiten: `landing.css`, Kopie der Schrift in `build:landing`, `.gitignore`. ⚠ F-2 (entfällt
  bei „nein“)

## Tests / Prüfung
- [ ] **T5**: Manuelle Prüfung in der Testumgebung (PC mit DPR 1 und 2, Handy hochkant/quer, Deutsch und Englisch):
  - Namen über Figuren scharf und lesbar
  - HUD, Chat, Menü in der Pixel-Schrift; Umlaute und ß in Texten und im Chat; nichts bricht unschön um
  - Chat-Verlauf über längere Zeit lesbar (sonst Chat-Text in Systemschrift, F-4)
  - Startseite und Anmeldung (bei F-2 = ja), Admin unverändert

  Danach alle AKs abhaken, Spec-Status auf `umgesetzt`, Roadmap aktualisieren.
