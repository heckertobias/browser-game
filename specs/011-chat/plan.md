# 011 – Plan

Gehört zu: [spec.md](spec.md)

## Überblick

```
Client  ── Chat.tsx (React, HUD-Ebene unten links) ── Game ── net.ts ── /ws (bestehender WebSocket, Protobuf)
Server  ── GameEndpoint: SendChat → Inbox des Tick-Threads
        ── GameLoop: prüft, nummeriert (Reihenfolge), verteilt an alle Verbindungen, hält die letzten 50 im Speicher
        ── ChatStore (BackgroundService): schreibt Nachrichten gebündelt in die DB, löscht stündlich alles > 24 h
        ── DB: Tabelle ChatMessages (FK auf Account, Cascade-Delete)
```

Der Chat läuft über den vorhandenen Spiel-WebSocket – keine zweite Verbindung, kein HTTP-Endpunkt. Wer dort
verbunden ist, ist angemeldet (003: `/ws` nur angemeldet + E-Mail bestätigt) und „im Spiel“.

**Voraussetzung aus 003:** T10 (`/ws` nur angemeldet, Spieler = Account, Name aus dem Account). Für AK-17 zusätzlich
T9 (`Delete`); die Löschung der Nachrichten selbst passiert per Cascade, siehe unten.

## Verträge

**Protobuf** (`contracts/proto/browsergame/v1/game.proto`), abwärtskompatibel:

```proto
message ClientMessage {
  oneof payload {
    Input input = 1;
    SendChat send_chat = 2;
  }
}

// Chatnachricht an alle (011). Höchstens 200 Zeichen (UTF-16-Einheiten, wie `maxLength` im Browser);
// der Server trimmt und prüft selbst.
message SendChat {
  string text = 1;
}

message ServerMessage {
  oneof payload {
    Welcome welcome = 1;
    Snapshot snapshot = 2;
    Kicked kicked = 3;
    // Direkt nach Welcome: letzte bis zu 50 Nachrichten der letzten 24 h, älteste zuerst.
    ChatHistory chat_history = 4;
    // Neue Nachricht, an alle Verbindungen in derselben Reihenfolge.
    ChatMessage chat_message = 5;
    // Nur an den Absender: Nachricht nicht angenommen.
    ChatRejected chat_rejected = 6;
  }
}

message ChatHistory {
  repeated ChatMessage messages = 1;
}

message ChatMessage {
  string sender_name = 1;
  string text = 2;
  // Serverzeit, Unix-Millisekunden; der Client zeigt sie in Ortszeit.
  int64 sent_at_unix_ms = 3;
}

message ChatRejected {
  enum Reason {
    REASON_UNSPECIFIED = 0;
    // Mehr als 5 Nachrichten in 10 Sekunden (AK-13).
    REASON_TOO_FAST = 1;
  }
  Reason reason = 1;
}
```

Leere und zu lange Nachrichten werden still verworfen: Der Client schickt sie gar nicht erst (AK-8, AK-9), der
Server prüft nur zur Sicherheit. Ein eigener Ablehnungsgrund lohnt sich dafür nicht.

**OpenAPI:** keine Änderung.

## Server

### Daten (`BrowserGame.Data`)
- `ChatMessageEntry` (`Id` long, Autoincrement · `AccountId` FK → `Account`, **Cascade-Delete** · `Text` max. 200 ·
  `SentAt` `DateTimeOffset`), Index auf `SentAt`.
- `DbSet<ChatMessageEntry> ChatMessages` im `GameDbContext`, Migration `AddChatMessages` für beide Provider
  (`scripts/add-migration.sh AddChatMessages`).
- Der Absendername wird nicht gespeichert, sondern beim Laden über `Account.UserName` gelesen (Namen sind nicht
  änderbar, 003).

### Regeln (`Game/Chat/`, reine Logik ohne Netzwerk und DB)
- `ChatRules.Normalize(string) → string?`:
  - Steuerzeichen (`char.IsControl`, also auch Zeilenumbrüche) werden zu Leerzeichen, dann trimmen.
  - Leer → `null` (AK-8); länger als 200 UTF-16-Einheiten → `null` (AK-9).
- `ChatRateLimiter`: pro **Account** (nicht pro Verbindung, sonst hilft Neuladen) die Zeitpunkte der letzten
  angenommenen Nachrichten. `TryAccept(accountId, now)` nimmt an, solange in den letzten 10 s weniger als 5 angenommen
  wurden (AK-13). Abgelehnte zählen nicht mit; nach Ablauf des Fensters geht es von selbst wieder.
  Zeitpunkt kommt als Parameter (testbar ohne Uhr). Einträge älter als 10 s fliegen beim nächsten Aufruf raus.
- `ChatHistory`: Ringpuffer der letzten 50 Nachrichten (mit `AccountId` und `SentAt`).
  `Recent(now)` liefert die jüngsten bis zu 50, die höchstens 24 h alt sind (AK-15, AK-16);
  `RemoveAccount(accountId)` entfernt alle Nachrichten eines Accounts (AK-17).
- Konstanten in `GameSettings`: `ChatMaxLength = 200`, `ChatHistoryCount = 50`, `ChatHistoryHours = 24`,
  `ChatRateLimitCount = 5`, `ChatRateLimitSeconds = 10`.

### Spiel (`Game/`)
- `GameEndpoint.ReceiveLoopAsync`: `SendChat` → `connection.Chat.Writer.TryWrite(text)` (eigener kleiner Channel
  pro Verbindung, `DropOldest`, Kapazität 8). Die bestehende Grenze `MaxClientMessageBytes = 1024` reicht für 200
  Zeichen (≤ 600 Bytes UTF-8 plus Rahmen).
- `GameLoop` (Tick-Thread, damit es eine einzige Reihenfolge gibt – AK-11):
  - Pro Tick nach den Eingaben die Chat-Channels aller Verbindungen leeren. Je Nachricht: `Normalize`, dann
    `TryAccept` mit `TimeProvider.GetUtcNow()`.
    - Abgelehnt wegen Tempo → `ChatRejected(TOO_FAST)` nur an den Absender.
    - Angenommen → `ChatMessage` mit Spielername aus der `World`, Serverzeit, an **alle** Verbindungen (auch den
      Absender, AK-10); in `ChatHistory` aufnehmen; an `ChatStore` zum Speichern übergeben.
    - Chat-Nachrichten gehen in dieselbe Outbox wie Snapshots. Die Outbox verwirft bei Überlauf die ältesten
      Einträge; damit Chat nicht hinter Snapshots verloren geht, wird ihre Kapazität von 8 auf 32 erhöht. Ein Client,
      der so weit hinterherhängt, ist ohnehin praktisch getrennt.
  - Beim Beitritt nach `Welcome` sofort `ChatHistory` aus `Recent(now)` schicken (AK-15).
  - Beim Start lädt der `GameLoop` die Historie einmal aus dem `ChatStore` (AK-15 nach Neustart).
  - `RemoveChatOf(accountId)`: über den Membership-Channel in den Tick-Thread, dort `ChatHistory.RemoveAccount`
    (AK-17). Wird von der `Delete`-Seite aus 003 T9 aufgerufen, zusammen mit `ForceLeave`.
  - Text, der mit `/` beginnt, ist in 011 ganz normaler Text. Slash-Commands (eigene Spec) setzen später genau hier
    an, bevor eine Nachricht verteilt wird.
- `ChatStore` (`BackgroundService`, Muster wie `PositionStore` aus 003):
  - Schreiben: Der Tick-Thread legt angenommene Nachrichten in einen Channel; der Dienst schreibt sie gebündelt
    (spätestens jede Sekunde), beim Herunterfahren alles Ausstehende.
  - `LoadRecentAsync(now)`: jüngste 50 mit `SentAt > now − 24 h`, mit Spielername.
  - Aufräumen: beim Start und dann stündlich `DELETE … WHERE SentAt < now − 24 h` (AK-16). Dazwischen sorgen
    `Recent(now)` und die Abfrage dafür, dass nichts Älteres ausgeliefert wird.
- `TimeProvider.System` wird registriert; Tests ersetzen ihn durch eine eigene kleine Fake-Uhr (keine neue
  Abhängigkeit).

## Client

- **`net.ts`:** `chatHistory`, `chatMessage`, `chatRejected` an neue Handler weiterreichen; `sendChat(text)`.
- **`game.ts`:** reicht die Chat-Ereignisse über `GameEvents` an React weiter (`onChatHistory`, `onChatMessage`,
  `onChatRejected`) und bietet `sendChat(text)`.
- **`chat/Chat.tsx`** (HUD-Ebene aus 007, unten links, Safe Areas):
  - Halbtransparenter Kasten (`bg-black/50`), Breite `min(24rem, 100% − Ränder)`, Höhe etwa ein Drittel des
    Fensters (AK-1). Liste mit Zeilen `HH:MM Name: Text`, neueste unten (AK-2).
  - Text nur als React-Textknoten, nie `dangerouslySetInnerHTML`, kein Link-Erkennen (AK-12).
  - Scrollen: Die Liste bekommt `pointer-events-auto`, `touch-action: pan-y`, `overflow-y: auto`. Sie bleibt nur dann
    unten, wenn sie vorher schon unten war (`isAtBottom(scrollTop, scrollHeight, clientHeight)`, Toleranz ein paar px).
    Der Spieler kann ältere Nachrichten lesen, ohne dass es springt (AK-3).
  - Einklappen über einen Button im Kopf. Eingeklappt bleibt ein Chat-Button mit Punkt für ungelesene Nachrichten
    (AK-4). Der Zustand liegt in `localStorage` (`bg.chat.collapsed`, pro Gerät, AK-4b). Ist nichts gespeichert,
    gilt `defaultCollapsed(isPortrait, isCoarsePointer)` → eingeklappt nur hochkant mit Touch (AK-4c).
  - Eingabe: `<input maxLength={200} enterKeyHint="send">` (AK-9). Enter oder der Senden-Button schicken ab, wenn
    `text.trim()` nicht leer ist (AK-8), und leeren das Feld. Esc leert das Feld, verlässt es und ruft
    `stopPropagation()` auf, damit ein späteres Menü (002) nicht aufgeht (AK-6).
  - Globales Enter (außerhalb eines Eingabefelds) klappt den Chat bei Bedarf auf und setzt den Fokus ins Feld (AK-5).
  - `ChatRejected(TOO_FAST)` → kurzer Hinweis über dem Eingabefeld („Du schreibst zu schnell“), verschwindet nach ein
    paar Sekunden. Der Text bleibt im Feld, damit er nicht verloren geht (AK-13).
  - Höchstens 200 Nachrichten im Client-Speicher; ältere fallen weg.
  - Uhrzeit mit `Intl.DateTimeFormat(language, { hour: "2-digit", minute: "2-digit" })` aus `sent_at_unix_ms`
    (Ortszeit, AK-2).
- **`input.ts`:**
  - Tastendrücke zählen nicht, solange ein Eingabefeld den Fokus hat (`isEditableTarget(e.target)`).
  - Beim Fokussieren eines Eingabefelds (`focusin`) werden gedrückte Tasten gelöscht, damit die Figur stehen bleibt
    (AK-6).
  - Der gehaltene Zeiger auf dem Canvas ist davon unberührt.
- **Handy (AK-7):**
  - Viewport-Meta um `interactive-widget=resizes-content` ergänzen (Android/Chrome: das Layout schrumpft mit der
    Tastatur).
  - iPhone/Safari ignoriert das. Dort hebt der Chat sich, solange das Feld den Fokus hat, um
    `innerHeight − visualViewport.height − visualViewport.offsetTop` an (Listener auf `visualViewport` `resize`/`scroll`).
  - Seit 007 haben `html, body` `user-select: none` und `touch-action: none`. Das Eingabefeld bekommt
    `user-select: text`, die Liste `touch-action: pan-y`.
- **`i18n.ts`:** neue Schlüssel `chatPlaceholder`, `chatSend`, `chatOpen`, `chatCollapse`, `chatTooFast`, `chatUnread`
  (de/en).

## Abdeckung der Akzeptanzkriterien

| AK | Wie erfüllt | Wie getestet |
|---|---|---|
| 1 | `Chat.tsx` unten links, halbtransparent | **manuell:** Monitor, Handy quer |
| 2 | Zeile Uhrzeit + Name + Text, neueste unten | Vitest: `formatTime` (Ortszeit, 2-stellig); Integration (WS): `ChatMessage` mit Name, Text, Zeit |
| 3 | `isAtBottom`, Scrollen in der Liste | Vitest: `isAtBottom`; **manuell:** hochscrollen, neue Nachricht → kein Sprung |
| 4 | Einklappen, Ungelesen-Punkt | **manuell** |
| 4b | `localStorage` `bg.chat.collapsed` | Vitest: Laden/Speichern inkl. fehlendem oder kaputtem Storage |
| 4c | `defaultCollapsed` | Vitest: Tabelle hochkant/quer × Touch/Maus |
| 5 | globales Enter, Enter/Esc im Feld | **manuell** (Tastatur) |
| 6 | `isEditableTarget`, Reset bei `focusin`, `stopPropagation` bei Esc | Vitest: `InputTracker` ignoriert Tasten bei fokussiertem Feld, Reset bei Fokus |
| 7 | `enterKeyHint`, `interactive-widget`, `visualViewport` | **manuell:** Android/Chrome und iPhone/Safari, hochkant und quer |
| 8 | Client schickt nichts; `ChatRules.Normalize` → `null` | Unit: leer, nur Leerzeichen, nur Zeilenumbrüche; Integration: leerer `SendChat` → niemand bekommt etwas |
| 9 | `maxLength`; Server prüft Länge | Unit: 200 ok, 201 → `null` |
| 10 | Verteilen an alle Verbindungen im Tick | Integration (WS): drei Spieler, einer schreibt → alle drei (inkl. Absender) bekommen sie innerhalb 1 s |
| 11 | Ein Tick-Thread entscheidet die Reihenfolge | Integration (WS): zwei Spieler senden abwechselnd je 5 → beide sehen identische Reihenfolge |
| 12 | Text bleibt Text | Unit: `<b>` und Links bleiben unverändert; Vitest/React: Inhalt als Textknoten (`textContent`), kein Element |
| 13 | `ChatRateLimiter`, `ChatRejected` | Unit: 5 ok, 6. abgelehnt, nach 10 s wieder ok, Abgelehntes zählt nicht; Integration: 6. Nachricht → nur Absender bekommt `ChatRejected`, kein Broadcast |
| 14 | `/ws` nur angemeldet (003 T10) | Integration aus 003 (anonym → 401); Chat hat keinen eigenen Zugang |
| 15 | `ChatHistory` nach `Welcome`, Laden beim Start | Unit: `Recent` (max. 50, älteste zuerst); Integration: 60 Nachrichten, neuer Spieler bekommt die letzten 50; neuer Server auf derselben DB → Historie da |
| 16 | `Recent(now)` filtert, `ChatStore` löscht stündlich und beim Start | Unit mit Zeitparameter; Integration mit Fake-Uhr: Nachricht 25 h alt → nicht in Historie, nach Aufräumen nicht mehr in der DB |
| 17 | Cascade-Delete + `RemoveChatOf` | Integration: Account mit Nachrichten löschen → neue Verbindung bekommt sie nicht, DB-Zeilen weg |

Integrationstests laufen wie in 003 gegen SQLite und PostgreSQL.

## Constitution-Check

- [x] **Server ist Autorität:** Der Server bestimmt Absendername (aus dem Account), Zeit und Reihenfolge, er prüft
  Länge und Tempo. Der Client liefert nur den Text.
- [x] **Typen nur aus `contracts/`:** neue Protobuf-Nachrichten, generiert für C# und TypeScript.
- [x] **Offline-Fortschritt:** nicht betroffen. Das Löschen nach 24 h ist Aufräumen; ausgeliefert wird ohnehin nur,
  was laut Zeitstempel jünger ist.
- [x] **Keine neuen Abhängigkeiten:** Fake-Uhr als kleine Testklasse, `TimeProvider` gehört zu .NET.
- [x] **KISS / kleiner Durchstich:** ein Kanal, bestehender WebSocket, eine Tabelle.

## Risiken / Abweichungen

- **Reihenfolge der Specs:** Der Chat braucht 003 T10 und für AK-17 auch 003 T9. 003 ist bis nach 007 pausiert.
  Der Chat kommt also nach 007 und nach dem Rest von 003.
- **Esc und Menü (002):** 002 ist noch nicht gebaut. Das Menü muss Esc ignorieren, wenn ein Eingabefeld den Fokus hat.
  Das gehört als Hinweis in den Plan von 002; der Chat stoppt Esc zusätzlich per `stopPropagation`.
- **Bildschirmtastatur auf dem iPhone:** Das Verhalten von `visualViewport` ist je nach iOS-Version eigenwillig. Das
  geht nur manuell zu prüfen; im schlimmsten Fall liegt die Eingabe kurz verdeckt, bis man scrollt.
- **Outbox-Kapazität:** Die Erhöhung von 8 auf 32 lässt einen langsamen Client etwas mehr alte Snapshots puffern.
  Unkritisch, weil der Client nur den neuesten auswertet.
