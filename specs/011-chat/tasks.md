# 011 – Tasks

Gehört zu: [plan.md](plan.md)

Reihenfolge: Verträge → Server → Client → Tests/Prüfung. Jeder Task bringt seine Tests mit.
Erst beginnen, wenn 003 T9 und T10 umgesetzt sind (siehe Plan, „Risiken“).

## Verträge
- [x] **T1** (AK-2, AK-10, AK-13, AK-15): `game.proto` um `SendChat`, `ChatHistory`, `ChatMessage` und
  `ChatRejected` erweitern; `buf lint` und `buf breaking` gegen `main` müssen grün sein.

## Server – Daten
- [x] **T2** (AK-15–17): `ChatMessageEntry` mit FK auf `Account` (Cascade), Index auf `SentAt`;
  Migration `AddChatMessages` für beide Provider (`scripts/add-migration.sh AddChatMessages`).

## Server – Regeln
- [x] **T3** (AK-8, AK-9, AK-12, AK-13): `ChatRules.Normalize` und `ChatRateLimiter` mit Unit-Tests;
  Konstanten in `GameSettings`.
- [x] **T4** (AK-15–17): `ChatHistory` (Ringpuffer, `Recent(now)`, `RemoveAccount`) mit Unit-Tests.

## Server – Spiel
- [x] **T5** (AK-10, AK-11, AK-13): `SendChat` im `GameEndpoint` → Chat-Channel der Verbindung; im `GameLoop`
  prüfen, `ChatMessage` an alle bzw. `ChatRejected` an den Absender; `PlayerOutbox` mit zwei Warteschlangen („Muss ankommen“
  zuerst, von Snapshots nur der neueste, Trennen ab 1024 Einträgen); `TimeProvider` im `GameLoop` nutzen.
- [x] **T6** (AK-15, AK-16): `ChatHistory.Add` im Angenommen-Zweig; `ChatStore` (gebündeltes Schreiben, Laden der Historie beim Start, Aufräumen beim Start
  und stündlich); `ChatHistory` nach `Welcome` senden; `GameEndpoint` endet bei `ApplicationStopping`.
- [x] **T7** (AK-17): `GameLoop.RemoveChatOf(accountId)`, Aufruf aus der `Delete`-Seite (003 T9).

## Client
- [x] **T8** (AK-10, AK-13, AK-15): `net.ts` und `game.ts` – Chat-Nachrichten empfangen und weiterreichen,
  `sendChat` mit Rückgabe, ob gesendet; leere Pflicht-Handler in `App.tsx` bis T10.
- [x] **T9** (AK-6): `input.ts` – Tasten bei fokussiertem Eingabefeld ignorieren, Reset bei `focusin`, Canvas-Druck nimmt dem Feld den
  Fokus; Vitest.
- [x] **T10** (AK-1–5, AK-8, AK-9, AK-12, AK-13): `features/game/chat/Chat.tsx` mit Liste, Eingabe, Einklappen, Ungelesen-Punkt,
  Hinweis „zu schnell“; reine Funktionen `formatTime`, `isAtBottom`, `defaultCollapsed`, Speichern in
  `localStorage` mit Vitest; i18n-Schlüssel de/en.
- [x] **T11** (AK-7): Handy – `enterKeyHint`, `interactive-widget=resizes-visual`, Anheben per `visualViewport`,
  `user-select`/`touch-action`/`overscroll-behavior` für Feld und Liste, „Senden“ über das Formular, Fokus weg bei
  Verbindungsende.

## Tests / Prüfung
- [ ] **T12**: Integrationstests (WS) laut Abdeckungstabelle mit Fake-Uhr, gegen SQLite und PostgreSQL.
- [x] **T13**: Hinweis im Plan von 002 (sobald es ihn gibt): Esc öffnet das Menü nicht, solange ein Eingabefeld den
  Fokus hat.
- [ ] **T14**: Manuelle Prüfung in der Testumgebung:
  - zwei Geräte (Laptop + Handy) chatten
  - AK-1, AK-3, AK-4, AK-5 am Monitor
  - AK-4c und AK-7 am Handy hochkant und quer (Android/Chrome und iPhone/Safari)
  - Nach dem Senden und nach dem Einklappen: am PC steuern WASD sofort wieder; nach dem Senden am Handy bleibt die Tastatur offen
  - AK-6: W halten, Enter: Figur stoppt; WASD im Feld bewegen nicht; Esc öffnet kein Menü
  - Feld fokussiert, Canvas drücken: Fokus weg, Tastatur zu, Text bleibt, WASD wirken wieder (auch iPhone/Safari)
  - Flugmodus am Handy: Der Server trennt nach etwa 20 s (Keep-Alive), danach „Neu verbinden“ und der Verlauf kommt wieder
  - Am Handy (Android/Chrome und iPhone/Safari, je Browser und Homescreen-App, hochkant und quer):
    - Feld antippen: Feld liegt ganz über der Tastatur, kein Zoom, Tippen geht, Cursor setzen und Einfügen per langem Druck
      gehen.
    - Die Taste der Tastatur zeigt „Senden“. Wort ohne Leerzeichen tippen (noch unterstrichen), „Senden“: geht beim ersten
      Tipp raus, Feld leer, Tastatur offen. Der nächste Buchstabe holt den alten Text nicht zurück. Dasselbe mit dem
      Senden-Knopf.
    - Spielbild bei offener Tastatur: Pixelgröße gleich, keine schwarzen Balken, auch quer. Es darf nach oben verschoben sein.
    - Tastatur schließen (Android-Zurück, iPhone „Fertig“, Canvas drücken): Chat und Spielbild liegen wieder genau wie
      vorher, ohne Versatz. Android-Zurück ohne Fokusverlust: Chat sinkt mit.
    - Canvas halten bei offener Tastatur: Figur läuft in Richtung Finger, ohne Sprung, wenn die Tastatur zugeht.
    - Drehen mit offener Tastatur: Feld bleibt sichtbar.
    - Quer mit Hinweis „zu schnell“: Feld sichtbar, Liste scrollt. Einklappen ist spätestens nach dem Schließen der Tastatur
      erreichbar.
    - Einklappen bei offener Tastatur: Tastatur zu, Chat-Button am Ursprungsort.
    - Liste wischen: scrollt. Am Anfang und Ende kein Neuladen, kein Mitziehen der Seite, Figur läuft nicht. Pinch und
      Doppeltipp zoomen nicht. Liste und Spielbild lassen sich nicht markieren.
    - Verbindung trennen bei offener Tastatur: Tastatur geht zu, „Neu verbinden“ ist sichtbar und tippbar, auch quer.
    - Bildschirm bleibt an, solange die Tastatur offen ist (Wake Lock).

  Danach alle AKs abhaken, Spec-Status auf `umgesetzt`, Roadmap aktualisieren.
