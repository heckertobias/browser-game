# 017 – Tasks

Gehört zu: [plan.md](plan.md)

Reihenfolge: Doku → Verträge → Server → Client → Assets → Tests/Prüfung. Jeder Task bringt seine Tests mit.
Erst beginnen, wenn 015 und 016 umgesetzt sind.

## Doku
- [x] **T1** (AK-16): ADR 0017 (nur CC0 oder selbst erstellt) und ADR 0018 (Sprite-Format, Plan-Abschnitt
  „Sprite-Format“); `client/src/assets/` mit `LICENSES.md` (noch ohne Zeilen) und `licenses.test.ts`;
  `docs/project-structure.md` nachziehen.

## Verträge
- [ ] **T2** (AK-11, AK-12): `game.proto`: `ToggleFacingArrows` (`ServerMessage` Feld 9); `buf lint` und `buf breaking`
  grün.

## Server
- [ ] **T3** (AK-11–13): `CommandContext.Reply` (init, Standard no-op); Command `toggleFacingArrows` im
  `CommandRegistry`; `GameLoop` setzt `Reply` auf die Outbox des Absenders. Unit-Tests (Reply genau einmal, `World`
  unverändert) und Integrationstests (WS: nur der Absender bekommt die Nachricht, kein Chat-Echo, zweimal → zwei
  Nachrichten).

## Client
- [ ] **T4** (AK-4–8, AK-19): `FACING_TIE_EPSILON`; `sprites/rig.ts` (`parseRig`, `sheetSize`, `frameRect`) und
  `sprites/direction.ts` (`spriteDirection`); Vitest `rig.test.ts`, `direction.test.ts` inkl. 45°-Fällen als float32
  und aller Regelverletzungen in `parseRig`. Importgrenze für `sprites/` in `oxlint.config.ts` und in
  `docs/project-structure.md`; Gegenprobe: ein Import aus `core/` in `sprites/` lässt `npm run lint` scheitern
  (nur ausprobieren, nicht einchecken).
- [ ] **T5** (AK-2, AK-9, AK-14, AK-15, AK-18–20): `sprites/compose.ts` (`tint`, `compose`, `greyscale`),
  `sprites/spriteSet.ts` (`Appearance`, `playerAppearance`, `appearanceKey`, `SpriteSet` mit Cache, `loadSpriteSet` mit `null` bei
  Fehler), `sprites/browser.ts`; Vitest mit synthetischen Pixeln, eigenem Test-Rig (zweites Kleidungsteil, dritter
  Slot, Pixel in den Zell-Ecken) und gefälschtem `toSource`.
- [ ] **T6** (AK-7, AK-8): `Predictor.moving`; `SnapshotBuffer.moving` (gemeinsame Suche mit `sample`, dessen Ergebnis
  gleich bleibt); Vitest.

## Assets
- [ ] **T7** (AK-1, AK-16–18, AK-20): `pngjs` und `@types/pngjs` (devDependencies, Versionen mindestens 7 Tage alt);
  `client/scripts/import-sprites.mjs` mit `npm run sprites:import`; `assets/sprites/humanoid/rig.json`; daraus
  `body-default.png` und `clothing-ninja.png` aus zwei Farbvarianten einer Pack-Figur (Blätter stellt der User
  bereit); Zeilen in `LICENSES.md`; `sprites/assets.test.ts`; `HUMANOID_RIG`, `HUMANOID_IMAGES`. Figurwahl und
  Ausgabe des Skripts nach Plan-Entscheidung 2 festhalten.

## Client (Zeichnen)
- [ ] **T8** (AK-1, AK-3, AK-9, AK-10, AK-14, AK-20): `render.ts`: `RenderOptions`, Sprite statt Quadrat, Quadrat als
  Rückfall, Pfeil nur mit `facingArrows`; `fakeContext.ts` additiv (`drawImage`, `fillText`); Vitest
  `render-sprite.test.ts`.
- [ ] **T9** (AK-10–14): `net.ts` (`onToggleFacingArrows`); `game.ts` (Schalter, `setSprites`, `moving`, Optionen an
  `render`); `App.tsx` lädt die Sprites. Vitest `net`, `game-facing-arrows.test.ts`. Bestehende
  `game-facing.test.ts` nur nach Freigabe anpassen (Toggle vor dem Zeichnen, Prüfungen gleich).

## Tests / Prüfung
- [ ] **T10**: Manuelle Prüfung in der Testumgebung (PC mit DPR 1 und 2, Handy hochkant/quer, zwei Geräte mit
  verschiedenen Farben):
  - Figur statt Quadrat, scharf, Kleidung in Spielerfarbe; vier Richtungen, schräg = Seite; Laufen und Stehen;
    andere Figur läuft sichtbar; wartende Figur grau
  - `/run toggleFacingArrows()` zeigt Pfeile nur auf diesem Gerät, zweites Mal aus, nach Neuladen aus
  - Grafik im Browser blockieren → Quadrate, Spiel läuft
  - etwa 50 laufende Figuren auf dem Testhandy flüssig (Weg je nach Antwort auf die offene Frage zu AK-15);
    `walk.frameMs` in `rig.json` bei Bedarf nachstellen

  Danach alle AKs abhaken, Spec-Status auf `umgesetzt`, Roadmap aktualisieren.
