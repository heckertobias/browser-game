# 002 – Tasks

Gehört zu: [plan.md](plan.md)

Reihenfolge: Verträge → Server → Client → Tests/Prüfung. Jeder Task bringt seine Tests mit.

## Verträge
- [x] **T1** (AK-5, AK-9, AK-10): `game.proto` um `Action`, `KeyBinding`, `KeyBindings`, `SaveKeyBindings`
  (`ClientMessage` Feld 3) und `Welcome.key_bindings` (Feld 5) erweitern; `buf lint` und `buf breaking` grün.

## Server
- [ ] **T2** (AK-9, AK-10): `Account.KeyBindings` (`byte[]?`, max. 512), Migration `AddKeyBindings` für beide Provider.
- [ ] **T3** (AK-7, AK-8, AK-11): `KeyBindingRules` (`IsValid`, `ToStored`, `ReservedCodes`) mit Unit-Tests.
- [ ] **T4** (AK-9, AK-10): Belegung beim Verbinden laden und in `Welcome` schicken; `SaveKeyBindings` prüfen und über
  `KeyBindingStore` speichern; Integrationstests (WS) gegen SQLite und PostgreSQL.
- [ ] **T5** (AK-3): `GET /Account/Logout` zeigt Bestätigungsseite mit POST-Formular und „Zurück zum Spiel“; Texte de/en;
  Integrationstest (GET meldet nicht ab).

## Client
- [ ] **T6** (AK-5–8, AK-10, AK-11): `bindings.ts` (`DEFAULT_BINDINGS`, `fromProto`, `toProto`, `assign`,
  `assignOverwriting`, `clear`, `canClear`, `keyLabel`) mit Vitest.
- [ ] **T7** (AK-4, AK-5, AK-6): `InputTracker.setBindings` und `setSuspended`; `activeActions` auf `Bindings`; Vitest.
- [ ] **T8** (AK-9): `net.ts`/`game.ts`: Belegung aus `Welcome` übernehmen, `saveKeyBindings`, `setMenuOpen`,
  `GameEvents.onKeyBindings`.
- [ ] **T9** (AK-1–4b): `Menu.tsx`, HUD-Button „Menü“ statt „Mein Account“, Esc-Listener (nicht in Eingabefeldern);
  `menuKeyAction` mit Vitest; i18n de/en.
- [ ] **T10** (AK-5–8, AK-10, AK-11): `KeyBindings.tsx` (Aufnehmen, Konflikt-Rückfrage, Hinweise, „×“, Standard);
  i18n de/en.

## Tests / Prüfung
- [ ] **T11**: Manuelle Prüfung in der Testumgebung:
  - PC: Esc auf/zu, Taste umlegen, zweite Taste, Konflikt, Esc und Enter nicht belegbar (Chat geht dabei nicht auf), letzte Taste nicht leerbar, Standard
  - zweites Gerät: Belegung ist da (AK-9); offenes Menü, anderes Gerät läuft weiter (AK-4b)
  - Handy hochkant/quer: Menü-Button, Abmelden über die Bestätigungsseite

  Danach alle AKs abhaken, Spec-Status auf `umgesetzt`, Roadmap aktualisieren.
