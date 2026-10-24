# 002 – Tastenbelegung & erstes Menü

**Status:** bereit  <!-- entwurf | bereit | in arbeit | umgesetzt -->

Setzt voraus: [003 – Accounts & gespeicherte Position](../003-accounts/spec.md) (die Belegung gehört zum Account).

## Ziel

Spieler können jede Aktion auf die Tasten legen, die ihnen passen – etwa für andere Tastaturlayouts,
Linkshänder oder Gewohnheiten aus anderen Spielen. Dafür gibt es ein erstes Spielmenü.

## User Stories

- Als **Spieler** möchte ich im Spiel ein Menü öffnen können, um Einstellungen zu ändern oder mich abzumelden.
- Als **Spieler** möchte ich jede Aktion (z. B. Laufen nach oben/unten/links/rechts) auf bis zu zwei Tasten meiner
  Wahl legen, damit ich so spielen kann, wie es mir angenehm ist.
- Als **Spieler** möchte ich, dass meine Belegung auf jedem Gerät gilt, an dem ich angemeldet bin.
- Als **Spieler** möchte ich meine Belegung jederzeit auf den Standard zurücksetzen können.

## Akzeptanzkriterien

### Menü
- **AK-1:** Gegeben ich bin im Spiel, wenn ich Esc drücke, dann öffnet sich das Menü; erneutes Esc schließt es.
- **AK-2:** Gegeben ich spiele per Touch, dann gibt es im HUD einen Menü-Button, der dasselbe Menü öffnet.
- **AK-3:** Gegeben das Menü ist offen, dann enthält es „Weiterspielen“, „Einstellungen → Tastenbelegung“ und „Abmelden“.
- **AK-4:** Gegeben das Menü ist offen, dann steuern Tasten das Menü und nicht meine Figur; meine Figur bleibt stehen.
- **AK-4b:** Gegeben das Menü ist offen, dann läuft das Spiel im Hintergrund weiter: Andere Spieler bewegen sich
  weiter und bleiben sichtbar.

### Tastenbelegung
- **AK-5:** Gegeben die Tastenbelegung, wenn ich bei einer Aktion einen der zwei Plätze wähle und eine Taste
  drücke, dann löst diese Taste ab sofort die Aktion aus.
- **AK-6:** Gegeben eine Aktion hat zwei Tasten, dann lösen beide die Aktion aus.
- **AK-7:** Gegeben ich belege eine Taste, die schon einer anderen Aktion zugeordnet ist, dann werde ich darauf
  hingewiesen, bevor etwas überschrieben wird.
- **AK-8:** Gegeben Esc ist für das Menü reserviert, dann kann Esc keiner anderen Aktion zugeordnet werden.
- **AK-9:** Gegeben ich habe Tasten umbelegt, wenn ich mich auf einem anderen Gerät anmelde, dann gilt dort meine Belegung.
- **AK-10:** Gegeben ich habe Tasten umbelegt, wenn ich „Standard wiederherstellen“ wähle, dann gilt wieder WASD
  (zweiter Platz leer).
- **AK-11:** Gegeben ein Platz ist belegt, wenn ich ihn leere, dann löst nur noch die andere Taste die Aktion aus;
  mindestens eine Taste pro Bewegungsaktion bleibt Pflicht.

## Out of Scope

- Gamepad-Unterstützung
- Belegung von Maus- und Touch-Gesten
- Weitere Einstellungen (Grafik, Ton) – das Menü ist dafür vorbereitet, Inhalte folgen in eigenen Specs

## Offene Fragen

_Keine._

## Entschiedene Fragen

- Belegung gilt pro Account (nicht pro Gerät).
- Bis zu zwei Tasten pro Aktion.
- Einstellungen sind über ein Spielmenü erreichbar (Esc bzw. Menü-Button).
- Standard: nur WASD, zweiter Platz leer.
- Das Spiel läuft bei offenem Menü weiter, nur die eigene Figur steht.

<!-- Keine Technik in dieser Datei. Das gehört in plan.md. -->
