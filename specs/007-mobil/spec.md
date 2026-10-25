# 007 – Handy hochkant, Sichtfeld ohne Ränder und installierbare Web-App

**Status:** bereit  <!-- entwurf | bereit | in arbeit | umgesetzt -->

Setzt voraus: [001 – Walking Skeleton](../001-walking-skeleton/spec.md) ·
[006 – Deutsch und Englisch](../006-sprachen/spec.md) (Texte der Offline-Seite) ·
Vor dem Rest von [003](../003-accounts/spec.md), damit HUD, Overlay und Account-Seiten gleich handytauglich entstehen.

## Ziel

Das Spiel soll auf jedem Bildschirm gut aussehen und sich gut anfühlen: am großen Monitor, am Laptop und am Handy –
quer wie hochkant. Das Spielbild füllt immer das ganze Fenster, ohne Ränder. Auf dem Handy lässt sich das Spiel wie
eine App auf den Homescreen legen und im Vollbild starten.

## User Stories

- Als **Spieler am Monitor oder Laptop** möchte ich ein Spielbild ohne schwarze Ränder, egal welches Format mein
  Fenster hat.
- Als **Spieler am Handy** möchte ich hochkant spielen können, ohne dass das Spielbild winzig wird.
- Als **Spieler am Handy** möchte ich das Spiel auf meinen Homescreen legen und wie eine App im Vollbild starten.
- Als **Spieler** möchte ich, dass mein Bildschirm beim Spielen nicht ausgeht.
- Als **Spieler ohne Netz** möchte ich eine verständliche Meldung statt einer Fehlerseite des Browsers.

## Akzeptanzkriterien

### Sichtfeld
- **AK-1:** Gegeben ich spiele in einem beliebigen Fenster oder auf einem beliebigen Bildschirm, dann füllt das
  Spielbild das ganze Fenster; es gibt keine Ränder. (Ausnahme: AK-4.)
- **AK-2:** Gegeben zwei Spieler mit unterschiedlichen Bildschirmen, dann zeigt die **kürzere** Seite ihres
  Spielbilds ungefähr gleich viel Welt: höchstens etwa ein Zehntel mehr als üblich; weniger nur bei Bildschirmen oder
  Fenstern mit niedriger Auflösung (dafür mit größeren Pixeln). Die längere Seite zeigt so viel mehr Welt, wie das
  Format hergibt: im Querformat mehr nach links und rechts, hochkant mehr nach oben und unten.
- **AK-3:** Gegeben ein beliebiger Bildschirm, dann sind alle Pixel der Pixel-Art gleich groß und scharf; nichts wirkt
  verwaschen oder ungleichmäßig.
- **AK-4:** Gegeben ein extrem breites oder extrem hohes Fenster (länger als etwa 21:9), dann zeigt das Spielbild
  höchstens so viel Welt wie bei 21:9; der Rest bleibt dunkel.
- **AK-5:** Gegeben ich drehe das Handy oder ändere die Fenstergröße, dann passt sich das Spielbild sofort an, ohne
  Neuladen und ohne dass meine Figur das Spiel verlässt.

### Handy
- **AK-6:** Gegeben ich spiele am Handy (hochkant oder quer), dann sind HUD-Anzeigen und Buttons gut lesbar, groß
  genug zum Tippen und liegen nicht unter Kamera-Aussparung, abgerundeten Ecken oder Systemleisten.
- **AK-7:** Gegeben ich halte den Finger auf das Spielbild, dann läuft meine Figur in Richtung meines Fingers, solange
  ich ihn halte, und folgt ihm, wenn ich ihn bewege; lasse ich los, bleibt sie stehen.
- **AK-8:** Gegeben ich bin im Spiel, dann lösen Gesten auf dem Spielbild weder Scrollen, Zoomen, Neuladen der Seite,
  Textauswahl noch ein Kontextmenü aus.
- **AK-9:** Gegeben ich nutze am Handy die Spieler-Seiten (Startseite, Anmeldung, Registrierung, Account), dann passen
  sie hochkant ohne seitliches Scrollen auf den Bildschirm und alle Felder und Buttons sind bequem bedienbar.

### Installierbare Web-App
- **AK-10:** Gegeben ich öffne das Spiel am Handy im Browser (Android: Chrome, iPhone: Safari), dann kann ich es mit
  eigenem Namen und Icon auf den Homescreen legen.
- **AK-11:** Gegeben ich starte das Spiel vom Homescreen, dann öffnet es ohne Browserleiste im Vollbild direkt das
  Spiel; bin ich nicht angemeldet, komme ich zuerst zur Anmeldung (003 AK-1).
- **AK-12:** Gegeben ich habe kein Netz, wenn ich das Spiel öffne oder neu lade, dann sehe ich in meiner Sprache die
  Seite „Keine Verbindung“ mit „Erneut versuchen“, statt einer Fehlerseite des Browsers. Spielen geht offline nicht.
- **AK-13:** Gegeben eine neue Version des Spiels ist ausgerollt, wenn ich das Spiel das nächste Mal starte, dann
  läuft die neue Version; ein alter Stand bleibt nicht hängen.

### Bildschirm
- **AK-14:** Gegeben ich bin im Spiel und es ist im Vordergrund, dann geht der Bildschirm nicht von selbst aus.
  Verlasse ich das Spiel oder wechsle die App, gilt wieder die normale Einstellung des Geräts.

## Out of Scope

- Push-Benachrichtigungen (→ [008](../008-push/spec.md))
- Native Apps und App-Stores
- Offline spielen
- Virtueller Joystick, Gamepad, eigene Touch-Gesten (Belegung von Gesten → 002 Out of Scope)
- Ein eigenes Layout für Tablets (sie folgen denselben Regeln wie Handy und Monitor)
- Das Admin-Interface als installierbare App
- Ausrichtung sperren: Hochkant und quer sind beide erlaubt

## Offene Fragen

_Keine._

## Entschiedene Fragen

- Sichtfeld: Die kürzere Seite zeigt überall etwa gleich viel Welt, die längere füllt das Format. Keine Ränder.
  Pixel bleiben scharf; dafür darf der Ausschnitt schwanken: nach oben höchstens ein Zehntel, nach unten bei niedriger
  Auflösung auch mehr (AK-2).
- Im Spiel ist Zoomen gesperrt (versehentliche Gesten), auf den Spieler-Seiten bleibt es erlaubt.
- Die Seite „Keine Verbindung“ erscheint auch, wenn der Server während eines Deploys kurz nicht erreichbar ist.
- Extreme Formate werden bei etwa 21:9 begrenzt, damit niemand extrem viel mehr sieht (AK-4).
- Touch-Steuerung: Tippen und Halten aufs Spielbild, kein virtueller Joystick.
- Web-App: installierbar, Offline-Hinweis, Bildschirm bleibt an. Push als eigene, spätere Spec.
- Reihenfolge: nach 006, vor dem Rest von 003.

<!-- Keine Technik in dieser Datei. Das gehört in plan.md. -->
