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
- `SentAt` wird auf beiden Providern per Konverter als Unix-Millisekunden gespeichert (`INTEGER`/`bigint`): EF Core
  übersetzt unter SQLite keine Vergleiche und keine Sortierung auf `DateTimeOffset`.
- `DbSet<ChatMessageEntry> ChatMessages` im `GameDbContext`, Migration `AddChatMessages` für beide Provider
  (`scripts/add-migration.sh AddChatMessages`).
- Der Absendername wird nicht gespeichert, sondern beim Laden über `Account.UserName` gelesen (Namen sind nicht
  änderbar, 003).

### Regeln (`Features/Game/Chat/Domain/`, reine Logik ohne Netzwerk und DB)
- `ChatRules.Normalize(string, maxLength) → string?`:
  - Steuerzeichen (`char.IsControl`, also auch Zeilenumbrüche) sowie U+2028 und U+2029 werden zu Leerzeichen, dann
    trimmen. Eine Nachricht kann so keine zusätzliche Chatzeile vortäuschen.
  - Leer → `null` (AK-8); länger als `maxLength` UTF-16-Einheiten → `null` (AK-9). Die Länge zählt nach dem Trimmen.
  - Nachrichten nur aus unsichtbaren Zeichen (z. B. U+200B) bleiben erlaubt: AK-8 nennt nur Leerzeichen, Moderation
    ist Out of Scope.
- `ChatRateLimiter(limit, window)`: pro **Account** (nicht pro Verbindung, sonst hilft Neuladen) die Zeitpunkte der
  letzten angenommenen Nachrichten. `TryAccept(accountId, now)` nimmt an, solange im Fenster `(now − window, now]`
  weniger als `limit` angenommen wurden (AK-13); bei genau 10 s Abstand ist also wieder frei. Abgelehnte zählen nicht
  mit; nach Ablauf des Fensters geht es von selbst wieder. Zeitpunkt kommt als Parameter (testbar ohne Uhr).
  Abgelaufene Einträge des Aufrufers fliegen beim Aufruf raus; inaktive Accounts werden höchstens einmal je Fenster
  entfernt, damit der Speicher nicht dauerhaft wächst und die Kosten bei vielen Schreibern klein bleiben.
- Die Grenzwerte kommen als Parameter: Domain greift nicht auf `GameSettings` zu, T5 übergibt die Konstanten.
- `ChatHistory(capacity, maxAge)`: Ringpuffer der letzten 50 Nachrichten als `ChatHistoryEntry(AccountId, SenderName,
  Text, SentAt)`. `Recent(now)` liefert die jüngsten bis zu 50, die jünger als 24 h sind (AK-15, AK-16); genau 24 h
  alt fällt heraus, wie bei der Abfrage im `ChatStore`. `RemoveAccount(accountId)` entfernt alle Nachrichten eines
  Accounts (AK-17).
- Konstanten in `GameSettings`: `ChatMaxLength = 200`, `ChatHistoryCount = 50`, `ChatHistoryHours = 24`,
  `ChatRateLimitCount = 5`, `ChatRateLimitSeconds = 10`.

### Spiel (`Features/Game/`)
- `GameEndpoint.ReceiveLoopAsync`: `SendChat` → `connection.Chat.Writer.TryWrite(text)` (eigener kleiner Channel
  pro Verbindung, `DropOldest`, Kapazität 8). Die bestehende Grenze `MaxClientMessageBytes = 1024` reicht für 200
  Zeichen (≤ 600 Bytes UTF-8 plus Rahmen).
- `GameLoop` (Tick-Thread, damit es eine einzige Reihenfolge gibt – AK-11):
  - Pro Tick nach den Eingaben die Chat-Channels aller Verbindungen leeren. Je Nachricht: `Normalize`, dann
    `TryAccept` mit `TimeProvider.GetUtcNow()`.
    - Abgelehnt wegen Tempo → `ChatRejected(TOO_FAST)` nur an den Absender.
    - Angenommen → `ChatMessage` mit Spielername aus der `World`, Serverzeit, an **alle** Verbindungen (auch den
      Absender, AK-10); in `ChatHistory` aufnehmen; an `ChatStore` zum Speichern übergeben.
    - Jede Verbindung hat auf demselben WebSocket zwei Warteschlangen (`PlayerOutbox`), genau eine Sendeschleife liest:
      - „Muss ankommen“: `Welcome`, `ChatHistory`, `ChatMessage`, `ChatRejected`, `Kicked`, künftig `AttackRejected` (013)
        und `CommandRejected` (015). FIFO, geht immer zuerst raus, wird im Normalbetrieb nie verworfen.
      - „Darf ausfallen“: Snapshots. Nur der neueste wartet (ein Platz): Jeder enthält den ganzen Stand, und der Client stempelt
        beim Eintreffen, aufgestaute hätten keinen Nutzen.
      - Beide Teile hängen an einem Lock, damit kein Snapshot `Welcome` überholt. `Kicked` schließt die Warteschlange in einem
        Schritt, danach geht kein Snapshot mehr raus.
    - Auslegung: bis etwa 100 gleichzeitige Spieler. Eine Nachricht wird einmal serialisiert, nicht je Empfänger. Grenze für
      „Muss ankommen“: 1024 Einträge, das fasst den Stoß aller Spieler (500) und etwa 20 s Hänger bei 50 Nachrichten/s.
    - Erreicht ein Client die Grenze, liest er nicht mehr. Der Server trennt die Verbindung ohne `Kicked` (`Kicked` käme nicht
      an). Der Client zeigt „Verbindung weg“ mit Neu-Verbinden (007 AK-16), die Figur wartet (003 AK-20, AK-21), der Verlauf
      kommt beim Neuverbinden (AK-15).
  - Beim Beitritt nach `Welcome` sofort `ChatHistory` aus `Recent(now)` schicken, wenn sie nicht leer ist (AK-15). Der
    Client leert seine Liste bei `Welcome`.
  - Beim Start lädt der `ChatStore` die Historie einmal und übergibt sie dem `GameLoop`, bevor dieser tickt und der Server
    Verbindungen annimmt (AK-15 nach Neustart). Schlägt das fehl, startet der Server ohne alten Verlauf.
  - `RemoveChatOf(accountId)`: über den Membership-Channel in den Tick-Thread, dort `ChatHistory.RemoveAccount`; danach
    nimmt der `GameLoop` von diesem Account nichts mehr an (AK-17). Die `Delete`-Seite aus 003 T9 ruft nach erfolgreichem
    Löschen in der Datenbank erst `ForceLeave`, dann `RemoveChatOf` auf. Sie wartet nicht auf den Tick: Ein späterer
    `Join` steht im selben Channel dahinter.
  - Text, der mit `/` beginnt, ist in 011 ganz normaler Text. Slash-Commands (eigene Spec) setzen später genau hier
    an, bevor eine Nachricht verteilt wird.
- `ChatStore` in `Features/Game/Chat/Infrastructure/` (`BackgroundService`, Muster wie `PositionStore` aus 003).
  Typen, die `GameLoop` mit ihm austauscht (Channel-Einträge, geladene Historie), liegen in Application oder Domain,
  nicht in Infrastructure (`docs/project-structure.md`):
  - Schreiben: Der Tick-Thread legt angenommene Nachrichten in einen begrenzten Channel (15 000, die ältesten fallen weg;
    blockiert nie); der Dienst schreibt sie gebündelt (spätestens jede Sekunde), beim Herunterfahren alles Ausstehende.
    Nach einem Fehler bleibt der Stapel und wird wiederholt, nach 5 Minuten aufgegeben.
  - `LoadRecentAsync(now)`: jüngste 50 mit `SentAt > now − 24 h`, mit Spielername.
  - Aufräumen: beim Start und dann stündlich `DELETE … WHERE SentAt < now − 24 h` in Stapeln von 10 000 Zeilen je Sekunde,
    damit andere Schreiber nicht warten (AK-16). Dazwischen sorgen
    `Recent(now)` und die Abfrage dafür, dass nichts Älteres ausgeliefert wird.
- `TimeProvider` ist schon registriert (`DatabaseSetup`). `GameLoop` bekommt ihn als optionalen Konstruktor-Parameter; Tests
  setzen eine eigene kleine Fake-Uhr ein (keine neue Abhängigkeit).

## Client

- **`net.ts`:** `chatHistory`, `chatMessage`, `chatRejected` an neue Handler weiterreichen. `sendChat(text)` liefert `true`,
  wenn die Nachricht an den offenen Socket ging, sonst `false` (nichts gesendet, keine Ausnahme).
- **`game.ts`:** reicht die Chat-Ereignisse über `GameEvents` an React weiter (`onChatHistory`, `onChatMessage`,
  `onChatRejected`) und bietet `sendChat(text)` mit derselben Rückgabe. `net.ts` und `game.ts` halten keine Chatliste.
- **`features/game/chat/Chat.tsx`** (HUD-Ebene aus 007, unten links, Safe Areas):
  - Die Liste liegt hier. `GameEvents.onWelcome` leert sie (ohne Verlauf kommt keine `ChatHistory`), `onChatHistory` setzt den
    Verlauf, `onChatMessage` hängt an. Bei `Kicked` und Verbindungsende bleibt sie stehen.
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
    `text.trim()` nicht leer ist (AK-8), und leeren das Feld, wenn `sendChat` `true` liefert. Bei `false` (Verbindung
    nicht offen) bleibt der Text im Feld. Nach dem Senden verlässt das Feld mit feinem Zeiger (Maus und Tastatur) den Fokus,
    damit WASD sofort wieder steuern; mit Touch bleibt der Fokus im Feld und die Tastatur offen. Enter auf ein leeres Feld sendet nichts; mit feinem Zeiger
    verlässt das Feld den Fokus, mit Touch bleibt er. Esc leert das Feld, verlässt es und ruft
    `stopPropagation()` auf, damit ein späteres Menü (002) nicht aufgeht (AK-6).
  - Globales Enter (außerhalb eines Eingabefelds) klappt den Chat bei Bedarf auf und setzt den Fokus ins Feld (AK-5).
  - `ChatRejected(TOO_FAST)` → kurzer Hinweis über dem Eingabefeld („Du schreibst zu schnell“), verschwindet nach ein
    paar Sekunden. Der Chat merkt sich den zuletzt gesendeten Text. Kommt `ChatRejected` und ist das Feld noch leer, setzt er ihn wieder ein,
    damit er nicht verloren geht (AK-13). Hat der Spieler schon weitergetippt, bleibt die neue Eingabe.
  - Höchstens 200 Nachrichten im Client-Speicher; ältere fallen weg. Eingehende Nachrichten werden höchstens alle 100 ms
    gebündelt übernommen.
  - Uhrzeit mit `Intl.DateTimeFormat(language, { hour: "2-digit", minute: "2-digit", hourCycle: "h23" })` aus `sent_at_unix_ms`
    (Ortszeit, AK-2).
- **`input.ts`:**
  - Tastendrücke zählen nicht, solange ein Eingabefeld den Fokus hat (`isEditableTarget(e.target)`).
  - Beim Fokussieren eines Eingabefelds (`focusin`) werden gedrückte Tasten gelöscht, damit die Figur stehen bleibt
    (AK-6).
  - Der gehaltene Zeiger auf dem Canvas ist davon unberührt: Er ist eine bewusste Geste und endet beim Loslassen; AK-6
    meint die Tasten.
  - Ein Druck, der auf dem Canvas das Halten startet, nimmt einem Eingabefeld den Fokus (`blur()`), der Text bleibt.
    `preventDefault` auf `pointerdown` verhindert den Fokuswechsel sonst.
- **Handy (AK-7):**
  - Viewport-Meta um `interactive-widget=resizes-visual` ergänzen (Chrome-Standard seit 108). Das Layout und damit das
    Canvas bleiben bei offener Tastatur gleich. `resizes-content` würde das Canvas quer auf etwa 5:1 schrumpfen: `k` fiele
    von 3 auf 2 oder 1, dazu schwarze Balken (007 AK-4).
  - Android und iPhone nutzen denselben Weg: Solange das Feld den Fokus hat, sitzt der Chat mindestens
    `innerHeight − visualViewport.height − visualViewport.offsetTop` über dem unteren Rand (Listener auf `visualViewport`
    `resize`/`scroll`). Diese Höhe ersetzt Rand und Safe Area, sie addiert sich nicht.
  - Seit 007 haben `html, body` `user-select: none` und `touch-action: none`. Das Eingabefeld bekommt
    `user-select: text`, die Liste `touch-action: pan-y` und `overscroll-behavior: contain`.
  - Abgeschickt wird nur über das Formular. Mit Touch verwirft das Feld nur gehaltenes Enter: „Senden“ der
    Bildschirmtastatur kann mit `keyCode` 229 oder während der Komposition kommen.
  - Bei Verbindungsende und `Kicked` verlässt das Feld den Fokus, damit die Tastatur den Overlay-Knopf nicht verdeckt. Der
    Text bleibt.
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
| 7 | `enterKeyHint`, `interactive-widget`, `visualViewport` | Vitest: `keyboardInset`, `liftWhileFocused`, `blocksEnterInField`, Markup, Viewport-Meta; **manuell:** Android/Chrome und iPhone/Safari, hochkant und quer |
| 8 | Client schickt nichts; `ChatRules.Normalize` → `null` | Unit: leer, nur Leerzeichen, nur Zeilenumbrüche; Integration: leerer `SendChat` → niemand bekommt etwas |
| 9 | `maxLength`; Server prüft Länge | Unit: 200 ok, 201 → `null` |
| 10 | Verteilen an alle Verbindungen im Tick; eigene Warteschlange „Muss ankommen“ mit Vorrang vor Snapshots | Integration (WS): drei Spieler, einer schreibt → alle drei (inkl. Absender) bekommen sie innerhalb 1 s (Median aus 5 Runden, ein Stau auf dem Runner trifft nicht die Mehrheit); `GameLoop`-Test ohne Sendeschleife: 100 Spieler am Tempolimit, als Stoß und verteilt über hängende Clients → keine Chat-Nachricht geht verloren. Über Sockets lässt sich ein hängender Client nicht nachstellen. |
| 11 | Ein Tick-Thread entscheidet die Reihenfolge | Integration (WS): zwei Spieler senden abwechselnd je 5 → beide sehen identische Reihenfolge |
| 12 | Text bleibt Text | Unit: `<b>` und Links bleiben unverändert; Vitest/React: `renderToStaticMarkup`: Inhalt escaped, kein Element |
| 13 | `ChatRateLimiter`, `ChatRejected` | Unit: 5 ok, 6. abgelehnt, nach 10 s wieder ok, Abgelehntes zählt nicht; Integration: 6. Nachricht → nur Absender bekommt `ChatRejected`, kein Broadcast; Vitest: Hinweis nur bei `tooFast` |
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
- **Bildschirmtastatur:** Das Verhalten von `visualViewport` ist je nach Browser und iOS-Version eigenwillig. Das
  geht nur manuell zu prüfen; im schlimmsten Fall liegt die Eingabe kurz verdeckt, bis man scrollt.
- **Trennen hängender Clients:** Antwortet ein Client nicht mehr, trennt ihn schon der WebSocket-Keep-Alive
  (`Program.cs`: 10 s Intervall, 10 s Timeout), unabhängig vom Chat. Die Grenze von 1024 Einträgen ist das Sicherheitsnetz
  für Clients, die noch antworten, aber zu langsam lesen. Beides trennt die ganze Verbindung; danach reicht ein Tipp
  (007 AK-16). `Abort` am echten Kestrel ist nicht automatisch getestet, weil sich ein hängender Client im TestHost nicht
  nachstellen lässt.
- **Snapshots bei langsamem Client:** Snapshots fallen aus, ein Client sieht dann weniger Zwischenstände. Dahinter puffern
  Kestrel und TCP weiter (64 KB entsprechen bei 100 Spielern etwa 9 Snapshots). Das bleibt außerhalb der Warteschlangen.
- **Nachricht kurz vor dem Gehen:** Eine Nachricht, die höchstens einen Tick (50 ms) vor `Leave` oder Übernahme eintrifft, geht
  verloren, wie Eingaben auch.
- **Account löschen und Verlauf:** Nach `RemoveAccount` wird nicht aus der Datenbank nachgeladen. Der Verlauf zeigt
  dann vorübergehend weniger als 50 Nachrichten, bis neue nachrücken oder der Server neu startet.
- **Account löschen und `ChatStore` (T6, T7):** Eine Nachricht, die beim Löschen noch auf das Schreiben wartet, läuft in
  die FK-Verletzung. Der `ChatStore` MUSS das so behandeln, dass nur die Einträge des gelöschten Accounts verloren
  gehen. `RemoveChatOf` MUSS im Tick-Thread nach `ForceLeave` laufen, damit danach nichts mehr vom Account in den
  Verlauf kommt. Scheitert das Löschen in der Datenbank, bleiben Verbindung und Verlauf unberührt. Eine Verbindung, die
  die Anmeldeprüfung vor dem Löschen bestanden hat und erst danach beitritt, kann nichts mehr schreiben, liest aber bis zum
  Trennen mit, und ihre Figur bleibt bis dahin. Das Zeitfenster ist klein, das berührt AK-14 und wird bewusst nicht weiter
  abgefangen.
- **Herunterfahren mit offenen WebSockets:** Kestrel wartet sonst bis zum Shutdown-Timeout auf offene Sockets, danach
  schreibt kein Store mehr. `GameEndpoint` endet deshalb bei `ApplicationStopping` (auch für 003 AK-19). Hängt die
  Datenbank beim Stoppen, geht das Ausstehende verloren.
- **Uhr und Reihenfolge:** Springt die Serveruhr zurück, kann die Reihenfolge des Verlaufs nach einem Neustart von der
  gesehenen abweichen. Bewusst nicht abgefangen.
- **Doppelte Nachricht:** Reißt die Verbindung genau beim Commit ab, kann ein Stapel doppelt in die Datenbank kommen (nur
  PostgreSQL über Netz). Bewusst nicht abgefangen.
