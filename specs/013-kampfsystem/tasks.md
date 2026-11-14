# 013 – Tasks

Gehört zu: [plan.md](plan.md)

Reihenfolge: Verträge → Server → Client → Tests/Prüfung. Jeder Task bringt seine Tests mit.
Erst beginnen, wenn 012 und 016 umgesetzt sind.

## Verträge
- [x] **T1** (AK-4–15, AK-18, AK-19): `game.proto`: `Attack` (`ClientMessage` 4), `AttackRejected` (`ServerMessage` 7),
  `TargetState`, `ProjectileState`, `Snapshot.targets`/`projectiles` (6, 7), `Action` 5–6; `buf lint` und `buf breaking`
  grün.

## Server
- [x] **T2** (AK-13, AK-15–17): Konstanten in `GameSettings`; `Target`, `Projectile`; `Combat` mit Dummy und
  `Step(tick)` (Respawn); Unit-Tests.
- [x] **T3** (AK-2, AK-4–12, AK-14, AK-19): `Combat.TryAttack` (Reihenfolge Ziel → Cooldown → Reichweite →
  Blickrichtung → Mana),
  Projektilflug und Einschlag; `Player.NextAttackTick`; Unit-Tests.
- [x] **T4** (AK-7, AK-8, AK-13, AK-19): `Attack` im `GameEndpoint` → Angriffs-Channel; im `GameLoop` ausführen,
  `AttackRejected` an den Angreifer, Ziele und Projektile im Snapshot; Integrationstests (WS).

## Client
- [x] **T5** (AK-1–3): `targeting.ts` (`pickTarget`, `TargetSelection`, `attackRequest`) mit Vitest; `input.ts`:
  `pointerdown` auf Ziel wählt aus statt zu laufen, Angriffstasten bei `keydown` ohne Repeat.
- [x] **T6** (AK-1, AK-7, AK-18): `net.ts` `sendAttack`, `game.attack(kind)`, Callbacks des `InputTracker` in `game.ts`;
  `game.ts`/`render.ts`: Ziele, Umrandung, HP-Balken (`hpBarWidth` mit Vitest),
  Projektile mit Interpolation.
- [x] **T7** (AK-8, AK-19): `AttackRejected` → `onAttackRejected`; `AttackButtons.tsx` mit Hinweisen „Zu wenig Mana“ und „Ziel nicht vor dir“; Angriffsaktionen in `DEFAULT_BINDINGS` (`Digit1`,
  `Digit2`) und in der Tastenbelegung (002); i18n de/en.

## Nachtrag nach dem ersten RC
- [ ] **T8** (AK-2, AK-5, AK-9, AK-20): `REASON_OUT_OF_RANGE` in `game.proto`; Server meldet `OutOfRange` an den
  Angreifer; Client meldet „kein Ziel“ selbst; Hinweise als `AttackHint.tsx` mittig oben, groß (statt über den
  Buttons); i18n de/en; Tests.

## Tests / Prüfung
- [ ] **T9**: Manuelle Prüfung in der Testumgebung (PC mit Maus und Tasten, Handy hochkant/quer):
  - Dummy anwählen, Nah- und Fernkampf, außer Reichweite (Hinweis), Mana leer schießen (Hinweis), ohne Ziel (Hinweis)
  - Hinweise gut sichtbar mittig oben
  - mit dem Rücken zum Dummy: kein Angriff, Hinweis; nach einem Schritt auf ihn zu: Angriff geht
  - Cooldown spürbar, Dummy stirbt und kommt nach 5 s wieder, Auswahl wird dabei aufgehoben
  - zweites Gerät sieht Projektile und HP-Balken

  Danach alle AKs abhaken, Spec-Status auf `umgesetzt`, Roadmap aktualisieren.
