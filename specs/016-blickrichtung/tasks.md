# 016 – Tasks

Gehört zu: [plan.md](plan.md)

Reihenfolge: Verträge → Server → Client → Tests/Prüfung. Jeder Task bringt seine Tests mit.

## Verträge
- [x] **T1** (AK-8, AK-12): `game.proto`: `PlayerState.facing` (Feld 8), `Welcome.min_facing_input` (Feld 8); `buf lint` und `buf breaking` grün.

## Server
- [x] **T2** (AK-1–5, AK-9, AK-11): `Player.Facing`, `DefaultFacing`, `MinFacingInput`; Ableitung in `World.Step()`;
  Unit-Tests.
- [x] **T3** (AK-4, AK-8, AK-11, AK-12): `facing` im Snapshot, `min_facing_input` im `Welcome`; Integrationstests (WS).

## Client
- [x] **T4** (AK-1–5, AK-7): `facingOf`, `lerpAngle` in `vector.ts`; `Predictor.facing`; Interpolation der
  Blickrichtung; Vitest.
- [ ] **T5** (AK-6–8): Pfeil in `render.ts` (`arrowPoints`, Konstanten in `constants.ts`); Vitest.

## Tests / Prüfung
- [ ] **T6**: Manuelle Prüfung in der Testumgebung (PC mit Tasten und Maus, Handy hochkant/quer, zwei Geräte):
  Pfeil dreht stufenlos mit, bleibt im Stehen, Standard nach Anmeldung, andere Figur zeigt ihren Pfeil.

  Danach alle AKs abhaken, Spec-Status auf `umgesetzt`, Roadmap aktualisieren. AK-10 wird mit 014 geprüft.
