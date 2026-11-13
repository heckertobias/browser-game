# 012 – Tasks

Gehört zu: [plan.md](plan.md)

Reihenfolge: Verträge → Server → Client → Tests/Prüfung. Jeder Task bringt seine Tests mit.

## Verträge
- [x] **T1** (AK-1, AK-5, AK-6, AK-8): `game.proto`: `Welcome.max_hp`/`max_mana` (6, 7), `Snapshot.hp`/`mana` (4, 5),
  `PlayerState.spawn_seq` (7); `buf lint` und `buf breaking` grün.

## Server
- [x] **T2** (AK-1–4): Konstanten in `GameSettings`; `Player.Hp/Mana/SpawnSeq`; Regeneration in `World.Step()`;
  Unit-Tests.
- [x] **T3** (AK-7–10): `World.Damage`, `World.SetVitals`, `World.Respawn`; Unit-Tests ohne Gegner.
- [x] **T4** (AK-2, AK-5, AK-6): `GameLoop`: Max-Werte im `Welcome`, eigene Werte und `spawn_seq` im Snapshot;
  Integrationstests (WS) inkl. Übernahme einer wartenden Figur.

## Client
- [x] **T5** (AK-5, AK-6): `game.ts` `onVitals` (nur bei Änderung); `Vitals.tsx` mit `vitalsText`/`fillPercent` und
  Vitest; i18n de/en.
- [ ] **T6** (AK-8): `interpolation.ts` springt bei neuem `spawnSeq`; Vitest.

## Tests / Prüfung
- [ ] **T7**: Manuelle Prüfung in der Testumgebung (PC und Handy hochkant/quer): HUD zeigt volle Werte, nach
  Anmeldung voll. Schaden und Respawn sind ohne Gegner nur über die Unit- und Integrationstests prüfbar (AK-10); am
  Gerät sichtbar wird es mit 013 (Mana-Verbrauch) bzw. 015 (`fullHealth()`).

  Danach alle AKs abhaken, Spec-Status auf `umgesetzt`, Roadmap aktualisieren.
