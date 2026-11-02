# 013 – Tasks

Gehört zu: [plan.md](plan.md)

Reihenfolge: Verträge → Server → Client → Tests/Prüfung. Jeder Task bringt seine Tests mit.
Erst beginnen, wenn 012 umgesetzt ist.

## Verträge
- [ ] **T1** (AK-4–15, AK-18): `game.proto`: `Attack` (`ClientMessage` 4), `AttackRejected` (`ServerMessage` 7),
  `TargetState`, `ProjectileState`, `Snapshot.targets`/`projectiles` (6, 7), `Action` 5–6; `buf lint` und `buf breaking`
  grün.

## Server
- [ ] **T2** (AK-13, AK-15–17): Konstanten in `GameSettings`; `Target`, `Projectile`; `Combat` mit Dummy und
  `Step(tick)` (Respawn); Unit-Tests.
- [ ] **T3** (AK-2, AK-4–12, AK-14): `Combat.TryAttack` (Reihenfolge Ziel → Cooldown → Reichweite → Mana),
  Projektilflug und Einschlag; `Player.NextAttackTick`; Unit-Tests.
- [ ] **T4** (AK-7, AK-8, AK-13): `Attack` im `GameEndpoint` → Angriffs-Channel; im `GameLoop` ausführen,
  `AttackRejected` an den Angreifer, Ziele und Projektile im Snapshot; Integrationstests (WS).

## Client
- [ ] **T5** (AK-1–3): `targeting.ts` (`pickTarget`, `TargetSelection`, `attackRequest`) mit Vitest; `input.ts`:
  `pointerdown` auf Ziel wählt aus statt zu laufen, Angriffstasten bei `keydown` ohne Repeat.
- [ ] **T6** (AK-1, AK-7, AK-18): `game.ts`/`render.ts`: Ziele, Umrandung, HP-Balken (`hpBarWidth` mit Vitest),
  Projektile mit Interpolation.
- [ ] **T7** (AK-8): `AttackButtons.tsx` mit Hinweis „Zu wenig Mana“; Angriffsaktionen in `DEFAULT_BINDINGS` (`Digit1`,
  `Digit2`) und in der Tastenbelegung (002); i18n de/en.

## Tests / Prüfung
- [ ] **T8**: Manuelle Prüfung in der Testumgebung (PC mit Maus und Tasten, Handy hochkant/quer):
  - Dummy anwählen, Nah- und Fernkampf, außer Reichweite, Mana leer schießen (Hinweis)
  - Cooldown spürbar, Dummy stirbt und kommt nach 5 s wieder, Auswahl wird dabei aufgehoben
  - zweites Gerät sieht Projektile und HP-Balken

  Danach alle AKs abhaken, Spec-Status auf `umgesetzt`, Roadmap aktualisieren.
