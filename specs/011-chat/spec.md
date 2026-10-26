# 011 – Chat

**Status:** entwurf  <!-- entwurf | bereit | in arbeit | umgesetzt -->

Setzt voraus: [003 – Accounts & gespeicherte Position](../003-accounts/spec.md) (Spielername) ·
[002 – Tastenbelegung & erstes Menü](../002-keybindings/spec.md) (Tasten steuern mal die Figur, mal etwas anderes) ·
[007 – Handy hochkant, Sichtfeld ohne Ränder, Web-App](../007-mobil/spec.md) (HUD am Handy)

## Ziel

Spieler können sich im Spiel unterhalten: Es gibt ein Chatfenster, in das jeder schreiben kann, und alle, die gerade
im Spiel sind, lesen mit und können antworten. Das ist der erste Schritt, damit sich die gemeinsame Welt auch
gemeinsam anfühlt. Bewusst ganz einfach: ein Kanal für alle.

## User Stories

- Als **Spieler** möchte ich im Spiel eine Nachricht schreiben, die alle anderen Spieler sehen, damit ich mit ihnen
  reden kann.
- Als **Spieler** möchte ich sehen, was andere schreiben und wer es geschrieben hat, damit ich antworten kann.
- Als **Spieler** möchte ich das Chatfenster einklappen können, wenn es mich beim Spielen stört.

## Akzeptanzkriterien

### Chatfenster
- **AK-1:** Gegeben ich bin im Spiel, dann sehe ich unten links ein Chatfenster über dem Spielbild. Es verdeckt nur
  einen kleinen Teil der Welt und lässt die Welt dahinter durchscheinen.
- **AK-2:** Gegeben im Chat steht eine Nachricht, dann sehe ich den Spielernamen des Absenders und den Text;
  die neueste Nachricht steht unten.
- **AK-3:** Gegeben es kommen mehr Nachrichten, als ins Fenster passen, dann rücken ältere nach oben heraus; ich kann
  im Fenster nach oben scrollen, um sie zu lesen. Scrolle ich nach oben, springt das Fenster bei neuen Nachrichten
  nicht von selbst nach unten.
- **AK-4:** Gegeben das Chatfenster ist offen, wenn ich es einklappe, dann bleibt nur ein kleiner Chat-Button übrig;
  kommt dann eine neue Nachricht, zeigt der Button das an. Mit dem Button klappe ich das Fenster wieder auf.

### Schreiben
- **AK-5:** Gegeben ich bin im Spiel, wenn ich Enter drücke, dann kann ich eine Nachricht tippen; Enter schickt sie ab,
  Esc bricht ab, ohne etwas zu schicken.
- **AK-6:** Gegeben ich tippe gerade eine Nachricht, dann steuern meine Tasten nicht meine Figur und öffnen nicht das
  Menü (Esc bricht nur das Tippen ab); meine Figur bleibt stehen.
- **AK-7:** Gegeben ich spiele per Touch, wenn ich auf das Eingabefeld des Chats tippe, dann kann ich mit der
  Bildschirmtastatur schreiben und mit „Senden“ abschicken; das Spielbild verschiebt sich dabei nicht so, dass das
  Eingabefeld verdeckt ist.
- **AK-8:** Gegeben ich schicke eine leere Nachricht oder eine, die nur aus Leerzeichen besteht, dann wird nichts
  gesendet.
- **AK-9:** Gegeben meine Nachricht ist länger als 200 Zeichen, dann kann ich nicht mehr weitertippen
  bzw. sie wird nicht angenommen.

### Alle sehen es
- **AK-10:** Gegeben mehrere Spieler sind im Spiel, wenn einer eine Nachricht schickt, dann sehen alle Spieler, die
  gerade im Spiel sind, sie innerhalb von etwa einer Sekunde – auch der Absender selbst.
- **AK-11:** Gegeben mehrere Spieler schreiben gleichzeitig, dann sehen alle die Nachrichten in derselben Reihenfolge.
- **AK-12:** Gegeben eine Nachricht enthält Zeichen wie `<`, `>` oder etwas, das wie ein Link aussieht, dann wird sie
  genau so als Text angezeigt; nichts davon wird ausgeführt, formatiert oder anklickbar.
- **AK-13:** Gegeben ein Spieler schreibt sehr viele Nachrichten kurz hintereinander (mehr als 5 in 10 Sekunden), dann
  werden weitere eine Weile nicht angenommen, und er sieht einen Hinweis, dass er zu schnell schreibt.
- **AK-14:** Gegeben ich bin nicht angemeldet, dann kann ich den Chat weder lesen noch hineinschreiben.

## Out of Scope

- Private Nachrichten (Flüstern), Gruppen-, Gilden- oder Handelskanäle
- Chat nur für Spieler in der Nähe, Sprechblasen über der Figur
- Melden, Blockieren, Stummschalten, Moderation durch Admins
- Filter für Beleidigungen in Nachrichten
- Nachrichten bearbeiten oder löschen
- Emoji-Auswahl, Formatierung, klickbare Links, Bilder
- Systemmeldungen im Chat (z. B. „X ist jetzt online“)
- Benachrichtigungen außerhalb des Spiels (→ 008)
- Taste für den Chat frei belegen (Enter ist vorerst fest)

## Offene Fragen

- [ ] **Verlauf:** Sehe ich beim Betreten des Spiels ältere Nachrichten (z. B. die letzten 50 oder die der letzten
  Stunde), oder nur, was ab jetzt geschrieben wird? Und wenn ja: Wie lange werden Nachrichten aufbewahrt?
- [ ] **Account löschen:** Falls Nachrichten aufbewahrt werden – werden meine Nachrichten beim Löschen des Accounts
  mitgelöscht (003 AK-16: „alle zugehörigen Daten“)?
- [ ] **Uhrzeit:** Soll bei jeder Nachricht die Uhrzeit stehen?
- [ ] **Moderation:** Reicht es zum Start ohne Melden/Stummschalten/Filter (kleiner Kreis von Spielern), oder braucht
  es mindestens, dass ein Admin Nachrichten löschen oder Spieler stummschalten kann?
- [ ] **Handy hochkant:** Ist der Chat dort standardmäßig eingeklappt, damit er nicht zu viel Spielbild verdeckt?
- [ ] **Merken:** Bleibt „eingeklappt/aufgeklappt“ gespeichert (pro Gerät oder pro Account wie die Tastenbelegung)?
- [ ] **Grenzen:** Passen 200 Zeichen pro Nachricht und „mehr als 5 in 10 Sekunden“ als Bremse?

<!-- Keine Technik in dieser Datei. Das gehört in plan.md. -->
