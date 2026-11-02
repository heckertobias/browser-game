# 015 – Plan

Gehört zu: [spec.md](spec.md)

## Überblick

```
Client  ── Chat.tsx (011): Eingabe wie bisher über SendChat; zeigt CommandRejected als kurzen Hinweis über dem Feld
Server  ── GameLoop (Chat-Verarbeitung aus 011): Text beginnt mit „/“ → nicht verteilen, sondern an den Dispatcher
        ── Features/Game/Commands/Domain/CommandParser.cs   „/run name(arg, …)“ → ParsedCommand oder Fehler
        ── Features/Game/Commands/Application/CommandRegistry.cs  Name → Command; fullHealth(), resetPosition()
```

Commands laufen auf dem Tick-Thread, genau wie Chat-Nachrichten in 011. Damit gibt es eine einzige Reihenfolge, und
ein Command darf die `World` direkt ändern.

**Voraussetzung:** 011 (Chat-Versandweg, `ChatRules.Normalize`, `ChatRateLimiter`, Hinweis über dem Eingabefeld),
012 (`World.SetVitals`, `spawn_seq`), 003 (Startpunkt).

## Entschiedene Fragen (Plan)

1. **Abfangpunkt:** im `GameLoop` nach `ChatRules.Normalize` (getrimmt, Steuerzeichen ersetzt) und nach dem
   `ChatRateLimiter`. Beginnt der Text mit `/`, geht er an den Dispatcher und **nie** in Verteilung, Historie oder
   Speicher (AK-1, AK-5). Sonst weiter wie in 011 (AK-2).
2. **Commands zählen zum Chat-Tempo** (5 in 10 s, 011 AK-13). `spawnEnemy()` (014) hat keine Obergrenze; das Tempo-Limit
   ist der einzige Schutz gegen Fluten. Zu schnell → `ChatRejected(TOO_FAST)` wie beim Chat.
3. **Grammatik** (Leerzeichen dazwischen erlaubt):
   ```
   /run <name>               z. B. /run fullHealth
   /run <name>(<args>)       z. B. /run fullHealth()   /run debug(A, B, C)
   name := Buchstabe (Buchstabe | Ziffer)*
   args := leer | arg (',' arg)*      arg := getrimmter Text ohne ',' und ')', nicht leer
   ```
   Alles andere nach `/` ist ein Fehler. Namen werden ohne Rücksicht auf Groß-/Kleinschreibung gefunden
   (`/run fullhealth()` geht auch).
4. **Zwei Fehlerarten** (`CommandRejected.Reason`):
   - `UNKNOWN_COMMAND`: anderes Präfix als `/run` (z. B. `/xyz`) oder unbekannter Name (AK-4).
   - `INVALID`: `/run` ohne Namen, kaputte Klammern oder falsche Anzahl Parameter (Spec: „fehlerhafter Befehl“).
   Beide: kurzer Hinweis, keine Wirkung.
5. **Erfolg ohne Antwort:** Ein ausgeführter Command schickt keine Nachricht; man sieht nur den Effekt (AK-5).
6. **Ein Command bekommt nur den eigenen Spieler** (`CommandContext`: `World`, `Player self`, `Tick`). Es gibt keinen Weg,
   eine andere Figur zu adressieren (AK-11). Ziele, Gegner und Dummies erreicht er nicht; 014 bekommt für
   `spawnEnemy()` gezielt Zugriff auf `Combat` (dort ist das Erzeugen gewollt, es trifft keine bestehende Figur).
7. **Keine Rechte, kein Schalter** (AK-9, AK-10): Die Registry ist fest im Code, ohne Konfiguration und ohne
   Umgebungsabfrage. Die bewusste Einschränkung steht als Kommentar an der Registry.
8. **`resetPosition()`** setzt die Figur wie beim ersten Start an einen zufälligen Punkt im `SpawnRadius` um (0, 0)
   (003 AK-17), erhöht `spawn_seq` (012, kein Gleiten bei den anderen), verwirft Richtung und offene Eingaben wie der
   Respawn aus 012. HP und Mana bleiben. Die neue Position wird mit dem nächsten regulären Speichern gesichert.
9. **`fullHealth()`** = `World.SetVitals(self, MaxHp, MaxMana)` (012).

## Verträge

**Protobuf** (`contracts/proto/browsergame/v1/game.proto`), abwärtskompatibel. Feldnummern sind über die Pläne 002 und
011–016 abgestimmt: `ServerMessage` 8. Eingegeben wird über `SendChat` (011); es gibt keine neue Client-Nachricht.

```proto
message ServerMessage {
  oneof payload {
    // … 1–3, 4–6: Chat (011), 7: AttackRejected (013)
    // Nur an den Absender: Befehl nicht ausgeführt (015).
    CommandRejected command_rejected = 8;
  }
}

message CommandRejected {
  enum Reason {
    REASON_UNSPECIFIED = 0;
    // Präfix oder Name unbekannt (AK-4).
    REASON_UNKNOWN_COMMAND = 1;
    // Syntax kaputt oder falsche Parameter.
    REASON_INVALID = 2;
  }
  Reason reason = 1;
}
```

**OpenAPI:** keine Änderung.

## Server

### Domain (`Features/Game/Commands/Domain/`, ohne Netz und DB)
- `CommandParser.Parse(string text) → ParseResult`: `Ok(name, IReadOnlyList<string> args)`, `UnknownPrefix`,
  `Invalid`. Einfaches Zerlegen von Hand nach der Grammatik oben (Text ≤ 200 Zeichen aus 011), kein Regex.

### Application (`Features/Game/Commands/Application/`)
- `CommandContext(World World, Player Self, uint Tick)`.
- `CommandRegistry`: `Dictionary<string, Command>` mit `StringComparer.OrdinalIgnoreCase`;
  `record Command(int ParameterCount, Action<CommandContext, IReadOnlyList<string>> Run)`.
  - `fullHealth` (0 Parameter) → `World.SetVitals` (AK-6).
  - `resetPosition` (0 Parameter) → `World.ResetToStart(self)` (AK-7, AK-8).
  - `Register(name, command)` für weitere (014: `spawnEnemy`).
- `CommandDispatcher.Execute(text, context) → CommandRejected.Reason?`: Parsen, nachschlagen, Parameterzahl prüfen,
  ausführen.

### Domain-Erweiterung `World`
- `ResetToStart(Player)`: gemeinsamer Teil mit dem Respawn aus 012 (neue Startposition, `SpawnSeq++`, Richtung und
  offene Eingaben verwerfen), aber ohne HP/Mana zu ändern.

### `GameLoop`
- In der Chat-Verarbeitung aus 011: nach `Normalize` und `TryAccept`
  `if (text.StartsWith('/')) { var reason = dispatcher.Execute(text, ctx); if (reason is { } r) send CommandRejected(r); continue; }`.

## Client

- **`net.ts` / `game.ts`:** `commandRejected` → `GameEvents.onCommandRejected(reason)`.
- **`Chat.tsx` (011):** derselbe kurze Hinweis wie bei „zu schnell“: „Unbekannter Befehl“ bzw. „Befehl fehlerhaft“.
  Das Feld wird beim Absenden geleert wie bei einer Nachricht (der Client weiß nicht, ob ein Command gelingt,
  Entscheidung 5). Bei `CommandRejected` setzt er den zuletzt gesendeten Command wieder ein, damit man ihn korrigieren
  kann.
- Der Client kennt die Commands nicht und prüft nichts außer dem, was 011 schon prüft (leer, Länge).
- **`i18n.ts`:** `commandUnknown`, `commandInvalid` (de/en).

## Abdeckung der Akzeptanzkriterien

| AK | Wie erfüllt | Wie getestet |
|---|---|---|
| 1 | Abfangen im `GameLoop` vor der Verteilung | Integration (WS): zwei Spieler, A schickt `/run fullHealth()` → B bekommt keine `ChatMessage`; nicht in der Historie, nicht in der DB |
| 2 | Nicht-`/`-Text unverändert | Integration: normale Nachricht kommt bei beiden an (bestehende 011-Tests bleiben grün) |
| 3 | `CommandParser`, `CommandRegistry` | Unit: `/run fullHealth`, `/run fullHealth()`, `/run  fullHealth ( )`, `/run debug(A, B, C)` → Name + 3 Args |
| 4 | `UNKNOWN_COMMAND` / `INVALID` | Unit: `/xyz`, `/run nope()`, `/run`, `/run x(`, `/run fullHealth(1)`; Integration: Absender bekommt `CommandRejected`, Spielstand unverändert |
| 5 | kein Echo, keine Antwort bei Erfolg | Integration: nach erfolgreichem Command keine `ChatMessage` an irgendwen |
| 6 | `fullHealth` → `SetVitals` | Unit: HP 10/Mana 0 → 100/100; Integration: nächster Snapshot voll |
| 7 | `resetPosition` → `ResetToStart` | Unit: Position (500, 500) → im `SpawnRadius`, HP unverändert, `SpawnSeq + 1` |
| 8 | Snapshot an alle | Integration: B sieht die neue Position von A im nächsten Snapshot (< 1 s), mit neuem `spawn_seq` |
| 9 | keine Rechteprüfung | Unit: Registry hat keinen Rechte-Parameter; Integration: frisch registrierter Account darf beide Commands |
| 10 | keine Konfiguration | Code-Review (Registry fest im Code); Integration: Commands auch mit `Environment=Production` |
| 11 | `CommandContext` nur mit `Self` | Unit: zwei Figuren, `fullHealth` auf A → B unverändert; `resetPosition` auf A → B unverändert |

## Constitution-Check

- [x] **Server ist Autorität:** Nur der Server kennt und führt Commands aus; der Client schickt Text.
- [x] **Typen nur aus `contracts/`:** `CommandRejected` in Protobuf.
- [x] **Offline-Fortschritt:** nicht betroffen.
- [x] **Keine neuen Abhängigkeiten.**
- [x] **KISS:** ein Präfix, ein Wörterbuch, zwei Commands.

## Risiken / Abweichungen

- **Keine Rechte im Produktivbetrieb (gewollt, AK-9/10):** Jeder kann sich heilen und an den Start setzen. Für ein Spiel
  ohne echten Kampfdruck ist das harmlos; vor einem öffentlichen Start braucht es die Rechte-Spec. Das steht so auch in
  der Spec.
- **Reihenfolge der Specs:** 015 setzt 011 voraus, das selbst noch nicht umgesetzt ist (siehe Roadmap).
- **Feld wieder füllen:** Kommt `CommandRejected` erst nach einer weiteren Eingabe an (sehr langsame Verbindung), wird
  der zuletzt gesendete Command nur eingesetzt, wenn das Feld noch leer ist.
