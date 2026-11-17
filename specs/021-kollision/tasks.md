# 021 – Tasks

Gehört zu: [plan.md](plan.md)

Reihenfolge: Verträge → Server → Client → Tests/Prüfung. Jeder Task bringt seine Tests mit.
Erst beginnen, wenn 013, 014, 018 und 019 umgesetzt sind.

## Verträge
- [ ] **T1** (AK-1, AK-5, AK-7): `game.proto`: `AreaMap.blocking_ground` (8) und `blocking_objects` (9),
  `Welcome.hitbox_width`/`hitbox_height`/`hitbox_offset_y` (11–13), `AttackRejected.REASON_NO_LINE_OF_SIGHT` (4);
  `buf lint` und `buf breaking` grün.

## Server
- [ ] **T2** (AK-1–3, AK-5, AK-7): `Map/Domain/Obstacles.cs`, `Hitbox.cs`, `Collision.cs` (`Move`, `Overlaps`,
  `HasLineOfSight`); Fixture-Datei mit Bewegungsfällen (Plan-Entscheidung 3); xUnit `ObstaclesTests`, `CollisionTests`.
- [ ] **T3** (AK-1–3, AK-5): `GameSettings` (Hitbox, blockierende Arten); `World` bewegt mit `Collision.Move`, prüft
  „Schritt < Kachel“, `IsFree`; `GameSetup` baut `Obstacles`; `Welcome` mit Hitbox, `AreaMap` mit Regeln. xUnit
  `WorldTests` (See, Kartenrand, Baumstamm, hohes Gras); bestehende Tests ohne Karte unverändert grün.
- [ ] **T4** (AK-4): `GameLoop.AddOrTakeOver` prüft die gespeicherte Position (Plan-Entscheidung 4); Test der echten
  Karte: kein Hindernis im `SpawnRadius` plus Hitbox um (0, 0). Integrationstests: Position im See, im Berg, außerhalb
  der Karte → Start; gültige bleibt; wartende Figur ohne Prüfung.
- [ ] **T5** (AK-6–8): `Combat` mit `Obstacles` und `Hitbox`: Sichtprüfung im Fernkampf vor dem Mana
  (`AttackResult.NoLineOfSight` → `AttackRejected`), `SpawnEnemy` nach Plan-Entscheidung 6. xUnit `CombatTests`;
  Integration (WS): Grund nur an den Angreifer.

## Client
- [ ] **T6** (AK-1–3, AK-5): `world/collision.ts` (`obstaclesFromMap`, `moveWithCollision`); `Predictor` mit Kollision
  aus dem `Welcome`; Vitest `collision.test.ts` (dieselbe Fixture wie T2), `prediction-collision.test.ts`.
- [ ] **T7** (AK-7): `AttackHint` und `i18n.ts`: „Keine freie Sicht“ / „No line of sight“; Vitest.

## Tests / Prüfung
- [ ] **T8**: Hinweis in [001](../001-walking-skeleton/spec.md) AK-7 auf 021 (Plan-Entscheidung 10). Manuelle Prüfung
  in der Testumgebung (PC und Handy):
  - gegen Berg, See, Baum, Busch und Stein laufen → stehen bleiben; durch hohes Gras laufen
  - schräg an Ufer und Bergrand entlang gleiten, ohne Ruckeln
  - das Gebiet lässt sich nicht verlassen
  - Account mit alter Position weit außerhalb (oder per Datenbank in den See gesetzt) → startet am Startpunkt
  - Fernkampf über den See geht, durch einen Baum oder Berg nicht (Hinweis, Mana bleibt)
  - `/run spawnEnemy()` mit Blick aufs Wasser → Gegner am Ufer, nicht im Wasser

  Danach alle AKs abhaken, Spec-Status auf `umgesetzt`, Roadmap aktualisieren.
