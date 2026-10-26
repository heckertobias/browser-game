# 011 – Tasks

Gehört zu: [plan.md](plan.md)

Reihenfolge: Verträge → Server → Client → Tests/Prüfung. Jeder Task bringt seine Tests mit.
Erst beginnen, wenn 003 T9 und T10 umgesetzt sind (siehe Plan, „Risiken“).

## Verträge
- [ ] **T1** (AK-2, AK-10, AK-13, AK-15): `game.proto` um `SendChat`, `ChatHistory`, `ChatMessage` und
  `ChatRejected` erweitern; `buf lint` und `buf breaking` gegen `main` müssen grün sein.

## Server – Daten
- [ ] **T2** (AK-15–17): `ChatMessageEntry` mit FK auf `Account` (Cascade), Index auf `SentAt`;
  Migration `AddChatMessages` für beide Provider (`scripts/add-migration.sh AddChatMessages`).

## Server – Regeln
- [ ] **T3** (AK-8, AK-9, AK-12, AK-13): `ChatRules.Normalize` und `ChatRateLimiter` mit Unit-Tests;
  Konstanten in `GameSettings`.
- [ ] **T4** (AK-15–17): `ChatHistory` (Ringpuffer, `Recent(now)`, `RemoveAccount`) mit Unit-Tests.

## Server – Spiel
- [ ] **T5** (AK-10, AK-11, AK-13): `SendChat` im `GameEndpoint` → Chat-Channel der Verbindung; im `GameLoop`
  prüfen, `ChatMessage` an alle bzw. `ChatRejected` an den Absender; Outbox-Kapazität 32; `TimeProvider` registrieren.
- [ ] **T6** (AK-15, AK-16): `ChatStore` (gebündeltes Schreiben, Laden der Historie beim Start, Aufräumen beim Start
  und stündlich); `ChatHistory` nach `Welcome` senden.
- [ ] **T7** (AK-17): `GameLoop.RemoveChatOf(accountId)`, Aufruf aus der `Delete`-Seite (003 T9).

## Client
- [ ] **T8** (AK-10, AK-13, AK-15): `net.ts` und `game.ts` – Chat-Nachrichten empfangen und weiterreichen,
  `sendChat`.
- [ ] **T9** (AK-6): `input.ts` – Tasten bei fokussiertem Eingabefeld ignorieren, Reset bei `focusin`; Vitest.
- [ ] **T10** (AK-1–5, AK-8, AK-9, AK-12, AK-13): `chat/Chat.tsx` mit Liste, Eingabe, Einklappen, Ungelesen-Punkt,
  Hinweis „zu schnell“; reine Funktionen `formatTime`, `isAtBottom`, `defaultCollapsed`, Speichern in
  `localStorage` mit Vitest; i18n-Schlüssel de/en.
- [ ] **T11** (AK-7): Handy – `enterKeyHint`, `interactive-widget=resizes-content`, Anheben per `visualViewport`,
  `user-select`/`touch-action` für Feld und Liste.

## Tests / Prüfung
- [ ] **T12**: Integrationstests (WS) laut Abdeckungstabelle mit Fake-Uhr, gegen SQLite und PostgreSQL.
- [ ] **T13**: Hinweis im Plan von 002 (sobald es ihn gibt): Esc öffnet das Menü nicht, solange ein Eingabefeld den
  Fokus hat.
- [ ] **T14**: Manuelle Prüfung auf dem Pi:
  - zwei Geräte (Laptop + Handy) chatten
  - AK-1, AK-3, AK-4, AK-5 am Monitor
  - AK-4c und AK-7 am Handy hochkant und quer (Android/Chrome und iPhone/Safari)

  Danach alle AKs abhaken, Spec-Status auf `umgesetzt`, Roadmap aktualisieren.
