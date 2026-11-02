# 014 – Tasks

Gehört zu: [plan.md](plan.md)

Reihenfolge: Verträge → Server → Client → Tests/Prüfung. Jeder Task bringt seine Tests mit.
Erst beginnen, wenn 012, 013, 015 und 016 umgesetzt sind.

## Verträge
- [ ] **T1** (AK-11): `game.proto`: `TargetState.Kind.KIND_ENEMY = 2`; `buf lint` und `buf breaking` grün.

## Server
- [ ] **T2** (AK-1, AK-2, AK-4): Konstanten in `GameSettings`; `Combat.SpawnEnemy` (Position aus Blickrichtung,
  fortlaufende IDs); Unit-Tests.
- [ ] **T3** (AK-5–8, AK-10): Gegner-Angriff in `Combat.Step` (nächster verbundener Spieler, Cooldown, `World.Damage`),
  Entfernen bei 0 HP, Reihenfolge im Tick; Unit-Tests.
- [ ] **T4** (AK-3, AK-9): Unit-Tests: Angriffe aus 013 auf Gegner (Nah, Fern, Projektil verfällt bei besiegtem Gegner).
- [ ] **T5** (AK-1, AK-12): `spawnEnemy` im `CommandRegistry`, `Combat` im `CommandContext`; Integrationstests (WS).

## Client
- [ ] **T6** (AK-11): Gegner in `render.ts` (rote Raute, HP-Balken); Vitest.

## Tests / Prüfung
- [ ] **T7**: Manuelle Prüfung in der Testumgebung (PC und Handy, zwei Geräte):
  - `/run spawnEnemy()` in mehrere Richtungen, mehrere Gegner
  - hinlaufen → Schaden im HUD, Tod → Respawn am Startpunkt
  - mit Nah- und Fernkampf besiegen → verschwindet
  - zweites Gerät sieht Gegner und HP-Balken

  Danach alle AKs abhaken, Spec-Status auf `umgesetzt`, Roadmap aktualisieren.
