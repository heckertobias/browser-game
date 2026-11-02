# 015 – Tasks

Gehört zu: [plan.md](plan.md)

Reihenfolge: Verträge → Server → Client → Tests/Prüfung. Jeder Task bringt seine Tests mit.
Erst beginnen, wenn 011 und 012 umgesetzt sind.

## Verträge
- [ ] **T1** (AK-4): `game.proto`: `CommandRejected` (`ServerMessage` Feld 8); `buf lint` und `buf breaking` grün.

## Server
- [ ] **T2** (AK-3, AK-4): `CommandParser` mit Unit-Tests (gültige Formen, Parameter, Fehlerfälle).
- [ ] **T3** (AK-6, AK-7, AK-9–11): `World.ResetToStart` (gemeinsam mit dem Respawn aus 012); `CommandContext`,
  `CommandRegistry` (`fullHealth`, `resetPosition`), `CommandDispatcher`; Unit-Tests.
- [ ] **T4** (AK-1, AK-2, AK-4, AK-5, AK-8): Abfangen im `GameLoop` nach `Normalize` und Tempo-Limit, `CommandRejected`
  an den Absender; Integrationstests (WS).

## Client
- [ ] **T5** (AK-4): `commandRejected` in `net.ts`/`game.ts`; Hinweis in `Chat.tsx`, letzten Command wieder einsetzen;
  i18n de/en.

## Tests / Prüfung
- [ ] **T6**: Manuelle Prüfung in der Testumgebung mit zwei Geräten:
  - `/run fullHealth()` nach Mana-Verbrauch (013) bzw. Schaden; `/run resetPosition()` – das andere Gerät sieht den Sprung
  - `/xyz` und `/run nope()` → Hinweis; normaler Chat unverändert, kein Echo der Commands

  Danach alle AKs abhaken, Spec-Status auf `umgesetzt`, Roadmap aktualisieren.
