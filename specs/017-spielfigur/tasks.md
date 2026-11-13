# 017 – Tasks

Gehört zu: [plan.md](plan.md)

Reihenfolge: Doku/Assets → Verträge → Server → Client → Tests/Prüfung. Jeder Task bringt seine Tests mit.
Erst beginnen, wenn 015 und 016 umgesetzt sind.

## Doku & Assets
- [ ] **T1** (AK-16, AK-17): ADR 0016 (nur CC0 oder selbst erstellt); `client/src/assets/` mit `player.png`
  (Original-`SpriteSheet.png` der gewählten Figur), `LICENSES.md` und `licenses.test.ts`;
  `docs/project-structure.md` nachziehen. Figurwahl und Kleidungsfarben nach Plan-Entscheidung 2 festhalten.

## Verträge
- [ ] **T2** (AK-11, AK-12): `game.proto`: `ToggleFacingArrows` (`ServerMessage` Feld 9); `buf lint` und `buf breaking`
  grün.

## Server
- [ ] **T3** (AK-11–13): `CommandContext.Reply` (init, Standard no-op); Command `toggleFacingArrows` im
  `CommandRegistry`; `GameLoop` setzt `Reply` auf die Outbox des Absenders. Unit-Tests (Reply genau einmal, `World`
  unverändert) und Integrationstests (WS: nur der Absender bekommt die Nachricht, kein Chat-Echo, zweimal → zwei
  Nachrichten).

## Client
- [ ] **T4** (AK-4–8): Konstanten in `constants.ts`; `render/spriteFrames.ts` (`spriteDirection`, `spriteCell`);
  Vitest inkl. 45°-Fällen als float32.
- [ ] **T5** (AK-2, AK-9, AK-14, AK-15, AK-17): `render/images.ts`, `render/playerSprites.ts` (`recolor`,
  `greyscale`, `PlayerSprites` mit Cache, `loadPlayerSprites` mit `null` bei Fehler, Browser-Umsetzung); Vitest mit
  synthetischen Pixeln und gefälschtem `toSource`.
- [ ] **T6** (AK-7, AK-8): `Predictor.moving`; `SnapshotBuffer.moving` (gemeinsame Suche mit `sample`, dessen Ergebnis
  gleich bleibt); Vitest.
- [ ] **T7** (AK-1, AK-3, AK-9, AK-10, AK-14): `render.ts`: `RenderOptions`, Sprite statt Quadrat, Quadrat als
  Rückfall, Pfeil nur mit `facingArrows`; `fakeContext.ts` additiv (`drawImage`, `fillText`); Vitest
  `render-sprite.test.ts`.
- [ ] **T8** (AK-10–14): `net.ts` (`onToggleFacingArrows`); `game.ts` (Schalter, `setPlayerSprites`, `moving`,
  Optionen an `render`); `App.tsx` lädt das Blatt. Vitest `net`, `game-facing-arrows.test.ts`. Bestehende
  `game-facing.test.ts` nur nach Freigabe anpassen (Toggle vor dem Zeichnen, Prüfungen gleich).

## Tests / Prüfung
- [ ] **T9**: Manuelle Prüfung in der Testumgebung (PC mit DPR 1 und 2, Handy hochkant/quer, zwei Geräte mit
  verschiedenen Farben):
  - Figur statt Quadrat, scharf, Kleidung in Spielerfarbe; vier Richtungen, schräg = Seite; Laufen und Stehen;
    andere Figur läuft sichtbar; wartende Figur grau
  - `/run toggleFacingArrows()` zeigt Pfeile nur auf diesem Gerät, zweites Mal aus, nach Neuladen aus
  - Grafik im Browser blockieren → Quadrate, Spiel läuft
  - etwa 50 laufende Figuren auf dem Testhandy flüssig (Weg je nach Antwort auf die offene Frage zu AK-15);
    `WALK_FRAME_MS` bei Bedarf nachstellen

  Danach alle AKs abhaken, Spec-Status auf `umgesetzt`, Roadmap aktualisieren.
