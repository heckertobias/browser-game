# 005 – Sachliches Layout fürs Admin-Interface

**Status:** bereit  <!-- entwurf | bereit | in arbeit | umgesetzt -->

Baut auf: [004 – Admin-Interface & SMTP-Einstellungen](../004-admin/spec.md)

## Ziel

Im Admin-Interface wird das Spiel betrieben, nicht gespielt. Es bekommt deshalb ein sachliches, übersichtliches
Layout, wie man es von Verwaltungsoberflächen kennt, statt der Pixel-Optik des Spiels. Das Layout soll auch dann noch
tragen, wenn viele weitere Bereiche dazukommen (Spieler, Server-Status, Spielwerte …).

## User Stories

- Als **Betreiber** möchte ich alle Bereiche des Admin-Interfaces jederzeit in einer Seitenleiste sehen, damit ich
  schnell dorthin komme und weiß, wo ich bin.
- Als **Betreiber** möchte ich Formulare, Meldungen und Tabellen, die gut lesbar und einheitlich sind, damit ich mich
  auf den Inhalt konzentrieren kann.
- Als **Betreiber** möchte ich hell oder dunkel arbeiten können, je nachdem, was mir gerade lieber ist.
- Als **Betreiber** möchte ich das Admin-Interface notfalls auch auf dem Handy bedienen können.

## Akzeptanzkriterien

### Navigation
- **AK-1:** Gegeben ich bin angemeldet, dann sehe ich links eine Seitenleiste mit allen Bereichen (Übersicht,
  SMTP-Einstellungen, Admins, Protokoll); der aktuelle Bereich ist hervorgehoben.
- **AK-2:** Gegeben ich bin angemeldet, dann erreiche ich von jeder Seite aus mein Konto und das Abmelden.
- **AK-3:** Gegeben ich nutze einen schmalen Bildschirm (Handy), dann ist die Seitenleiste eingeklappt und lässt sich
  über einen Menü-Knopf öffnen; die Seite scrollt nicht seitlich.
- **AK-4:** Gegeben ich bin nicht angemeldet (Anmeldung, zweiter Faktor, Einrichtung über einen Einladungslink),
  dann sehe ich keine Seitenleiste, sondern nur das jeweilige Formular mittig auf der Seite.

### Aussehen
- **AK-5:** Gegeben ich öffne eine beliebige Admin-Seite, dann ist sie sachlich gestaltet (normale Schrift, keine
  Pixel- oder Spieloptik); das Spiel und seine Startseite sehen unverändert aus.
- **AK-6:** Gegeben ein Formular, dann stehen alle Beschriftungen über ihren Feldern, und Hinweise zum Feld stehen
  direkt darunter.
- **AK-7:** Gegeben ich schicke ein Formular mit Fehlern ab, dann ist jedes betroffene Feld markiert und der Fehler
  steht direkt am Feld; Fehler, die zu keinem Feld gehören (z. B. „Testmail fehlgeschlagen“), stehen oben über dem
  Formular. Ohne Fehler erscheint kein Fehlerkasten.
- **AK-8:** Gegeben eine Aktion war erfolgreich (z. B. „Gespeichert.“), dann sehe ich eine deutlich als Erfolg
  erkennbare Meldung oben auf der Seite.
- **AK-9:** Gegeben eine Liste (Admins, Protokoll), dann wird sie als gut lesbare Tabelle angezeigt; auf schmalen
  Bildschirmen lässt sich die Tabelle seitlich scrollen, die Seite selbst nicht.

### Hell & dunkel
- **AK-10:** Gegeben ich habe nichts eingestellt, dann folgt das Admin-Interface der hellen oder dunklen Einstellung
  meines Systems.
- **AK-11:** Gegeben ich wähle in der Navigation „Hell“, „Dunkel“ oder „System“, dann wechselt die Darstellung sofort,
  und die Wahl bleibt in diesem Browser auch nach dem Neuladen und nach dem Abmelden erhalten.
- **AK-12:** Gegeben ich habe „Dunkel“ gewählt und lade eine Seite, dann blitzt sie beim Laden nicht kurz hell auf.

### Sprache
- **AK-14:** Gegeben ich öffne eine beliebige Admin-Seite, dann sind alle Texte auf Englisch, auch Fehlermeldungen
  und Hinweise, unabhängig von der Sprache des Browsers. Das gilt auch für Mails aus dem Admin (Einladung, Testmail).
  Eine deutsche Fassung soll später ohne Umbau ergänzt werden können (siehe [006](../006-sprachen/spec.md)).

### SMTP-Einstellungen
- **AK-15:** Gegeben ich habe eine Testmail erfolgreich verschickt, wenn ich danach speichere, dann wird genau das
  getestete Passwort gespeichert; es kann nicht passieren, dass still ein altes oder gar kein Passwort gespeichert
  bleibt.
- **AK-16:** Gegeben ich speichere ohne Passwort, dann sehe ich deutlich, ob ein Passwort gespeichert ist; ist keins
  gespeichert, werde ich darauf hingewiesen.
- **AK-17:** Gegeben die Einstellungen sind gespeichert, dann kann ich eine Testmail genau mit dem gespeicherten Stand
  schicken – so wie ihn das Spiel für Bestätigungs- und Reset-Mails benutzt.

### Nichts geht verloren
- **AK-13:** Gegeben die bisherigen Abläufe aus 004 (erster Admin, Einladen, Passkey, Passwort + Authenticator-App,
  Wiederherstellungscodes, SMTP speichern und testen, Admins entfernen, Protokoll), dann funktionieren sie unverändert
  (nur die Texte sind jetzt englisch, AK-14).

## Out of Scope

- Neue Admin-Funktionen oder eine Übersichtsseite mit Kennzahlen (Spieler online, Server-Status …) – eigene Specs
- Hell/Dunkel-Wahl pro Admin-Account (sie gilt pro Browser)
- Änderungen am Aussehen des Spiels oder seiner Startseite

## Offene Fragen

_Keine._

## Entschiedene Fragen

- Funktional vor Spieloptik: ein gängiges, sachliches Admin-Framework (Bootstrap), kein eigenes Design.
- Navigation als Seitenleiste links, auf dem Handy einklappbar.
- Farbschema folgt dem System, mit Umschalter Hell/Dunkel/System; die Wahl wird im Browser gemerkt.
- Umsetzung nach 003.
- Admin-Interface auf Englisch (aus 006 hierher verschoben, weil 005 ohnehin jede Admin-Seite anfasst).
- SMTP-Seite: Nach einer Testmail wurde das Passwortfeld geleert; wer danach speicherte, behielt still das alte
  Passwort (beim RC 0.3.0-rc.1 in der Testumgebung aufgefallen). AK-15 bis AK-17 beheben das.

<!-- Keine Technik in dieser Datei. Das gehört in plan.md. -->
