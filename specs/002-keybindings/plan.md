# 002 – Plan

Gehört zu: [spec.md](spec.md)

## Überblick

```
Client  ── features/game/menu/Menu.tsx         Overlay über dem Spiel: Weiterspielen, Tastenbelegung, Mein Account, Abmelden
        ── features/game/menu/KeyBindings.tsx  Liste der Aktionen mit je zwei Plätzen, Taste aufnehmen, Mitteilung bei Überschreiben
        ── features/game/input/bindings.ts     reine Logik: Belegung ändern, Überschreiben, Standard, Tastennamen
        ── input.ts                            Belegung austauschbar, „pausiert“ solange das Menü offen ist
        ── net.ts / game.ts                    Belegung aus Welcome übernehmen, SaveKeyBindings schicken
Server  ── GameEndpoint: Belegung beim Verbinden laden → Welcome; SaveKeyBindings prüfen und speichern
        ── Features/Game/KeyBindings/Domain/KeyBindingRules.cs       Prüfung ohne Netz und DB
        ── Features/Game/KeyBindings/Infrastructure/KeyBindingStore.cs  liest/schreibt Account.KeyBindings
        ── Accounts: GET /Account/Logout zeigt eine Bestätigungsseite (bisher Redirect auf /)
        ── DB: Spalte Account.KeyBindings (Protobuf-Bytes, NULL = Standard)
```

Die Belegung läuft über den vorhandenen Spiel-WebSocket. Wer dort verbunden ist, ist angemeldet (003); eine eigene
HTTP-API (OpenAPI, Antiforgery, zweiter Auth-Weg) lohnt sich für einen einzigen Wert nicht.

## Entschiedene Fragen (Plan)

1. **Standard liegt nur im Client.** Der Server speichert entweder eine vollständige, geprüfte Belegung oder `NULL`
   („Standard“). Der Client kennt `DEFAULT_BINDINGS` (WASD, zweiter Platz leer – AK-10) ohnehin schon.
2. **Gespeichert wird die Protobuf-Nachricht `KeyBindings` als Bytes** in `Account.KeyBindings`. Damit bleibt der Vertrag
   die einzige Typquelle (Constitution §3); es gibt keine zweite Form (JSON, eigene Tabelle) derselben Daten.
3. **Tasten werden als `KeyboardEvent.code` gespeichert** (wie bisher, unabhängig vom Tastaturlayout). Angezeigt wird
   der Name auf dem aktuellen Layout über `navigator.keyboard.getLayoutMap()` (Chromium); sonst ein Name aus dem Code
   (`KeyW` → `W`, `ArrowUp` → `↑`, `Digit1` → `1`).
4. **Reserviert sind `Escape`, `Enter` und `NumpadEnter`** (AK-8). Enter öffnet seit 011 den Chat (`shouldOpenChat`
   prüft `e.key`, trifft also beide Enter-Tasten). Wird beim Aufnehmen Esc gedrückt, bricht das Aufnehmen ab, der Platz
   bleibt wie er war. Enter beim Aufnehmen ergibt `reserved`; der Aufnahme-Listener ruft `preventDefault()`, damit
   `shouldOpenChat` (prüft `defaultPrevented`) den Chat nicht öffnet. Die Liste `RESERVED_CODES` liegt an einer Stelle
   (Client und Server).
5. **Überschreiben (AK-7):** Die Taste ist schon bei einer anderen Aktion belegt → sie wird dort entfernt und hier
   belegt, ohne Rückfrage. Danach steht eine Mitteilung „W war ‚Nach rechts‘ zugeordnet und ist jetzt ‚Nach oben‘.“
   Die andere Aktion darf dadurch ohne Taste sein. Dieselbe Taste zweimal bei derselben Aktion wird still ignoriert.
   (Geändert nach dem Test von 0.8.0-rc.1; vorher Rückfrage und Pflichttaste je Bewegungsaktion.)
6. **Leeren (AK-11):** Jeder Platz hat ein „×“, immer aktiv, sobald der Platz belegt ist. Es gibt keine Pflichttasten.
   Rückt der zweite Platz nach, wenn der erste geleert wird? Ja: Eine Aktion hält eine Liste mit 0–2 Tasten, die
   Anzeige füllt von vorne. Eine Aktion ohne Taste wird mitgeschickt (leere Liste), damit `fromProto` ihr nicht wieder
   den Standard gibt.
7. **Menü statt „Mein Account“-Link im HUD.** 003 hat den Link nur bis zum Menü vorgesehen. Der HUD-Button „Menü“ ist
   immer sichtbar (auch am PC, damit man ihn mit der Maus findet) und erfüllt AK-2. Im Menü bleibt „Mein Account“ als
   vierter Eintrag, sonst gäbe es keinen Weg mehr dorthin.
8. **Abmelden:** Das Spiel ist eine statische Seite ohne Antiforgery-Token, ein `POST /Account/Logout` geht von dort nicht.
   „Abmelden“ öffnet `GET /Account/Logout`; die Seite zeigt jetzt „Wirklich abmelden?“ mit dem bestehenden POST-Formular
   und „Zurück zum Spiel“. Ein GET meldet weiterhin nicht ab (kein CSRF über Links).
9. **Speichern sofort.** Jede Änderung wird sofort lokal wirksam (AK-5) und sofort geschickt. Kein „Übernehmen“-Button.
10. **Esc, solange ein Eingabefeld den Fokus hat, öffnet das Menü nicht** (Vorgabe aus 011, T13 dort). Das Menü prüft
    `isEditableTarget(e.target)`; der Chat ruft zusätzlich `stopPropagation()` auf. Esc mit `e.repeat` wird ignoriert
    (weder öffnen noch schließen): Wird Esc im Chat gehalten, kommt die Wiederholung sonst außerhalb des Felds an.

## Verträge

**Protobuf** (`contracts/proto/browsergame/v1/game.proto`), abwärtskompatibel.

Die Feldnummern sind über die Pläne 002 und 011–016 abgestimmt, damit die Reihenfolge der Umsetzung egal ist:
`ClientMessage` 2 = Chat (011), 3 = Belegung (002), 4 = Angriff (013); `Welcome` 5 = Belegung (002), 6–7 = Max-Werte (012).

```proto
message ClientMessage {
  oneof payload {
    Input input = 1;
    // 2: SendChat (011)
    SaveKeyBindings save_key_bindings = 3;
  }
}

// Spieleraktionen, die auf Tasten liegen. Neue Aktionen (z. B. Angriffe in 013) kommen hier dazu.
enum Action {
  ACTION_UNSPECIFIED = 0;
  ACTION_MOVE_UP = 1;
  ACTION_MOVE_DOWN = 2;
  ACTION_MOVE_LEFT = 3;
  ACTION_MOVE_RIGHT = 4;
}

message KeyBinding {
  Action action = 1;
  // KeyboardEvent.code, höchstens 2, ohne Escape, Enter, NumpadEnter.
  repeated string codes = 2;
}

// Belegung eines Accounts. Leer = Standard des Clients.
message KeyBindings {
  repeated KeyBinding bindings = 1;
}

// Neue Belegung speichern; leer = Standard wiederherstellen (AK-10). Ungültige Belegungen verwirft der Server still.
message SaveKeyBindings {
  KeyBindings key_bindings = 1;
}

message Welcome {
  string player_id = 1;
  string name = 2;
  float speed = 3;
  uint32 tick_rate_hz = 4;
  // Gespeicherte Belegung des Accounts; leer = Standard (AK-9).
  KeyBindings key_bindings = 5;
}
```

**OpenAPI:** keine Änderung.

## Server

### Daten (`BrowserGame.Data`)
- `Account.KeyBindings` (`byte[]?`, max. 512 Bytes). `NULL` = Standard.
- Migration `AddKeyBindings` für beide Provider (`scripts/add-migration.sh AddKeyBindings`).
- Löschen des Accounts nimmt die Belegung automatisch mit (Spalte am Account).

### Regeln (`Features/Game/KeyBindings/Domain/KeyBindingRules.cs`, ohne Netz und DB)
- `IsValid(KeyBindings)`:
  - leer → gültig (Standard).
  - Sonst: jede Aktion bekannt (nicht `UNSPECIFIED`, kein unbekannter Enum-Wert) und höchstens einmal aufgeführt.
  - Je Aktion 0–2 Codes (AK-11); keine Pflichtaktionen, fehlende Aktionen sind erlaubt.
  - Kein Code doppelt, weder in einer Aktion noch über Aktionen hinweg (AK-7).
  - Kein Code aus `ReservedCodes` (`Escape`, `Enter`, `NumpadEnter`, AK-8).
  - Codes 1–32 Zeichen, nur `[A-Za-z0-9]`.
- `ToStored(KeyBindings) → byte[]?`: leer → `null`, sonst `ToByteArray()`.

### Spiel (`Features/Game/`)
- `GameEndpoint.HandleAsync`: lädt die Belegung zusammen mit dem Account (`account.KeyBindings`), parst sie
  (`KeyBindings.Parser`; kaputte Daten → leer) und gibt sie an `game.Join(…)` weiter; `PlayerConnection` hält sie,
  `GameLoop` setzt sie in `Welcome.key_bindings`.
- `ReceiveLoopAsync`: `SaveKeyBindings` → `KeyBindingRules.IsValid` → `KeyBindingStore.SaveAsync(accountId, bytes)`.
  Ungültig → ignorieren und loggen (Debug). Der Endpoint wartet das Speichern ab; Speicherfehler werden geloggt, die
  Verbindung bleibt bestehen.
- `KeyBindingStore` (`Features/Game/KeyBindings/Infrastructure/`): eigener Scope über `IServiceScopeFactory`,
  `ExecuteUpdateAsync` auf `Account.KeyBindings`. Kein Hintergrunddienst, Speichern ist selten.

### Abmelden (`Features/Accounts/Presentation/Account/Logout`)
- `OnGet` zeigt die Seite statt umzuleiten: Frage „Wirklich abmelden?“, POST-Formular (wie in `Index.cshtml`) und Link
  „Zurück zum Spiel“ (`/play`). Nicht angemeldet → wie bisher Redirect auf `/`.
- Texte in `Texts.resx` / `Texts.de.resx`.

## Client

### `features/game/input/bindings.ts` (reine Funktionen, Vitest)
- Typen aus dem generierten Code (`Action`, `KeyBindings`); intern `type Bindings = ReadonlyMap<Action, readonly string[]>`.
  Die handgeschriebenen `Action`, `KeyBindings` und `DEFAULT_BINDINGS` in `input/input.ts` (aus 001) entfallen.
- `DEFAULT_BINDINGS` (WASD, zweiter Platz leer), `RESERVED_CODES = ["Escape", "Enter", "NumpadEnter"]`.
- `fromProto(KeyBindings) → Bindings`: leer → Standard. Aktionen, die fehlen (z. B. neue aus späteren Specs), bekommen
  ihren Standard, sofern dessen Taste noch frei ist, sonst bleiben sie leer. Mitgeschickte leere Aktionen bleiben leer.
- `toProto(Bindings) → KeyBindings`: gleich dem Standard → leer; sonst alle Aktionen, auch leere.
- `assign(bindings, action, slot, code) → { kind: "ok", bindings, overwritten?: Action } | { kind: "reserved" }`
  (AK-5, AK-7, AK-8): `overwritten` ist die Aktion, der die Taste weggenommen wurde.
- `clear(bindings, action, slot)` (AK-11).
- `keyLabel(code, layoutMap?)`.

### `input.ts`
- `InputTracker` bekommt `setBindings(bindings)` (AK-5: sofort wirksam) und `setSuspended(boolean)`: pausiert → gedrückte
  Tasten und gehaltener Zeiger werden gelöscht, `direction()` liefert `ZERO` (AK-4). Der Client schickt weiter Eingaben
  (Richtung 0), damit der Server die Figur sofort anhält.
- `activeActions` bleibt, liest aber die `Bindings`-Map; beide Plätze lösen aus (AK-6).

### `net.ts` / `game.ts`
- `onWelcome` liefert die Belegung mit; `Game` setzt sie im `InputTracker` und meldet sie über `GameEvents.onKeyBindings`
  an React.
- `Game.saveKeyBindings(bindings)`: setzt sie lokal und schickt `SaveKeyBindings`.
- `Game.setMenuOpen(open)` → `input.setSuspended(open)`. Rendern und Snapshots laufen weiter (AK-4b).

### `features/game/menu/Menu.tsx`
- Overlay (`bg-black/60`, Safe Areas wie die bestehenden Overlays), das Spiel bleibt dahinter sichtbar (AK-4b).
- Einträge als echte `<button>`/`<a>` (Tab/Enter/Leertaste, Fokus beim Öffnen auf „Weiterspielen“): Weiterspielen,
  Tastenbelegung, Mein Account (`/Account/Manage`), Abmelden (`/Account/Logout`) (AK-3).
- Globaler `keydown`-Listener für Esc in `App.tsx`: öffnet/schließt (AK-1). Ignoriert, wenn `isEditableTarget(e.target)` oder `e.repeat`.
  In der Tastenbelegung während des Aufnehmens bricht Esc nur das Aufnehmen ab.
- HUD: Button „Menü“ oben rechts an der Stelle von „Mein Account“ (AK-2), `min-h-11` wie bisher.

### `features/game/menu/KeyBindings.tsx`
- Tabelle: Aktion | Platz 1 | Platz 2. Klick auf einen Platz → „Taste drücken …“; der nächste `keydown` (mit
  `preventDefault`) wird über `assign` geprüft. Ergebnis `ok` mit `overwritten` → Mitteilung (AK-7),
  `reserved` → Hinweis „Esc ist für das Menü reserviert“ bzw. „Enter ist für den Chat reserviert“ (AK-8).
- „×“ je belegtem Platz (AK-11), Button „Standard wiederherstellen“ (AK-10), „Zurück“.
- Am Handy ohne Tastatur ist die Seite erreichbar, aber ohne Tastatur nicht nutzbar; kein eigener Sonderfall.

### `i18n.ts`
Neue Schlüssel (de/en): `menu`, `menuContinue`, `menuKeyBindings`, `menuSignOut`, Aktionsnamen (`actionMoveUp` …),
`bindingPressKey`, `bindingOverwritten`, `bindingReserved`, `bindingReservedChat`,
`bindingClear`, `bindingReset`, `back`.

## Abdeckung der Akzeptanzkriterien

| AK | Wie erfüllt | Wie getestet |
|---|---|---|
| 1 | Esc-Listener in `App.tsx` | Vitest: `menuKeyAction` (Esc → toggle, Esc in Eingabefeld → nichts, Esc mit `repeat` → nichts, Esc beim Aufnehmen → abbrechen); **manuell** |
| 2 | HUD-Button „Menü“ | **manuell:** Handy hochkant/quer |
| 3 | `Menu.tsx` | Vitest/React: Einträge vorhanden; **manuell:** Abmelden über Bestätigungsseite; Integration: `GET /Account/Logout` angemeldet → 200 mit Formular, meldet nicht ab |
| 4 | `InputTracker.setSuspended` | Vitest: pausiert → `direction()` = 0 trotz gedrückter Taste/gehaltenem Zeiger, gedrückte Tasten gelöscht |
| 4b | Rendern läuft weiter | **manuell:** zwei Geräte, eines mit offenem Menü sieht das andere laufen |
| 5 | `assign`, `setBindings` | Vitest: neue Taste → `activeActions` liefert die Aktion |
| 6 | Map mit zwei Codes | Vitest: beide Codes lösen aus |
| 7 | `assign` → `ok` mit `overwritten`, Mitteilung | Vitest: Taste wechselt die Aktion, `overwritten` nennt die alte, andere Aktion darf leer werden; Unit (Server): doppelte Codes ungültig |
| 8 | `RESERVED_CODES` Client + Server | Vitest: `Escape`, `Enter`, `NumpadEnter` → `reserved`; Aufnehmen mit Enter öffnet den Chat nicht (`defaultPrevented`); Unit (Server): alle drei ungültig |
| 9 | `Account.KeyBindings`, Welcome | Integration (WS): speichern, neue Verbindung → `Welcome.key_bindings` gleich; ungültige Belegung → nicht gespeichert |
| 10 | „Standard wiederherstellen“ → leer | Vitest: `toProto(DEFAULT)` leer; Integration: leere Belegung → Spalte `NULL`, Welcome leer |
| 11 | `clear`, Server-Prüfung | Vitest: auch die letzte Taste ist leerbar, leere Aktion bleibt nach `toProto`/`fromProto` leer; Unit (Server): Aktion ohne Taste gültig |

Integrationstests laufen wie in 003 gegen SQLite und PostgreSQL.

## Constitution-Check

- [x] **Server ist Autorität:** Die Belegung beeinflusst den Spielstand nicht (sie übersetzt nur Tasten in Richtungen,
  die der Server ohnehin prüft). Trotzdem prüft der Server, was er speichert.
- [x] **Typen nur aus `contracts/`:** `Action`, `KeyBindings` aus Protobuf; gespeichert als Protobuf-Bytes.
- [x] **Offline-Fortschritt:** nicht betroffen.
- [x] **Keine neuen Abhängigkeiten.**
- [x] **KISS / kleiner Durchstich:** eine Spalte, zwei Nachrichtenfelder, kein neuer Endpunkt.

## Risiken / Abweichungen

- **`navigator.keyboard.getLayoutMap()`** gibt es nur in Chromium. Firefox/Safari zeigen den Namen aus dem Code (auf
  QWERTZ steht dann bei `KeyY` ein „Y“, obwohl die Taste „Z“ heißt). Gespeichert wird trotzdem die richtige physische
  Taste; nur die Beschriftung kann abweichen.
- **Keine Drosselung beim Speichern:** Jede gültige Belegung wird sofort geschrieben. Ein Client, der flutet, braucht
  einen Account und trifft nur seine eigene Zeile. Falls nötig, später ein allgemeines Rate-Limit für den WebSocket.
- **Abmelden-Seite:** ein Klick mehr als vorher über „Mein Account“. Dafür kein Token im statischen Spiel.
- **Neue Aktionen in späteren Specs (013):** `fromProto` gibt fehlenden Aktionen ihren Standard, damit alte
  gespeicherte Belegungen nicht ungültig werden.
