# 004 – Admin-Interface & SMTP-Einstellungen

**Status:** in arbeit  <!-- entwurf | bereit | in arbeit | umgesetzt -->

Voraussetzung für: [003 – Accounts](../003-accounts/spec.md) (Bestätigungs- und Reset-Mails nutzen diese Einstellungen)

## Ziel

Das Spiel wird von Anfang an über ein eigenes Admin-Interface betrieben statt über Konfigurationsdateien. Es läuft
unabhängig vom Spiel, damit man auch dann noch eingreifen kann, wenn das Spiel steht. Erste Funktion: die
Einstellungen für den Mailversand.

## User Stories

- Als **Betreiber** möchte ich mich in einem eigenen Admin-Interface anmelden, das mit den Spieler-Accounts nichts zu tun hat.
- Als **Betreiber** möchte ich die Zugangsdaten für den Mailversand dort eintragen, ändern und mit einer Testmail prüfen.
- Als **Betreiber** möchte ich, dass Änderungen sofort im Spiel wirken, ohne es neu zu starten.

## Akzeptanzkriterien

### Erster Start & Anmeldung
- **AK-1:** Gegeben es gibt noch keinen Admin, wenn das Admin-Interface startet, dann gibt es einen einmaligen
  Einrichtungslink (nur für den Betreiber sichtbar, z. B. im Log), über den der erste Admin angelegt wird. Danach ist
  der Link ungültig.
- **AK-2:** Gegeben ich bin Admin, dann melde ich mich im Admin-Interface mit meinem Admin-Account an; ein
  Spieler-Account funktioniert dort nicht, und ein Admin-Account funktioniert nicht im Spiel.
- **AK-3:** Gegeben ich bin nicht angemeldet, dann sehe ich im Admin-Interface nur die Anmeldung.
- **AK-4:** Gegeben ich melde mich sehr oft mit falschen Daten an, dann werden weitere Versuche gebremst.
- **AK-4b:** Gegeben ich lege einen Admin-Account an, dann wähle ich als Anmeldung einen Passkey oder ein Passwort;
  bei einem Passwort muss ich zusätzlich einen zweiten Faktor (Authenticator-App) einrichten. Eine Anmeldung nur mit
  Passwort gibt es nicht.
- **AK-4c:** Gegeben ich richte den zweiten Faktor ein, dann bekomme ich einmalig Wiederherstellungscodes, mit denen
  ich mich anmelden kann, falls die Authenticator-App verloren geht; jeder Code funktioniert nur einmal.
- **AK-4d:** Gegeben ich bin angemeldet, dann bleibe ich höchstens 8 Stunden angemeldet.

### Admins verwalten
- **AK-4e:** Gegeben ich bin Admin, wenn ich einen weiteren Admin einlade, dann bekomme ich einen Einladungslink zum
  Weitergeben (und, falls der Mailversand eingerichtet ist, optional per Mail); er funktioniert nur einmal und nur
  48 Stunden.
- **AK-4f:** Gegeben es gibt mehrere Admins, dann kann ich andere Admins entfernen; der letzte Admin kann nicht
  entfernt werden.

### Unabhängigkeit
- **AK-5:** Gegeben das Spiel ist gestoppt oder abgestürzt, dann ist das Admin-Interface weiter erreichbar und
  benutzbar – und umgekehrt.
- **AK-6:** Gegeben ich rufe das Admin-Interface von außerhalb des Heimnetzes bzw. VPN auf, dann ist es nicht erreichbar.

### SMTP-Einstellungen
- **AK-7:** Gegeben ich bin angemeldet, dann kann ich Server, Port, Verschlüsselung, Benutzername, Passwort und
  Absender (Adresse und Anzeigename) für den Mailversand eintragen und speichern.
- **AK-8:** Gegeben ein Passwort ist gespeichert, dann wird es nie wieder angezeigt (nur „gesetzt“); lasse ich das
  Feld beim Speichern leer, bleibt es unverändert.
- **AK-9:** Gegeben die Einstellungen sind eingetragen, wenn ich „Testmail senden“ an eine Adresse wähle, dann kommt
  die Mail an bzw. ich sehe eine verständliche Fehlermeldung (z. B. „Anmeldung am Mailserver fehlgeschlagen“).
- **AK-10:** Gegeben ich ändere die Einstellungen, dann verschickt das Spiel die nächste Mail bereits mit den neuen
  Einstellungen, ohne Neustart.
- **AK-11:** Gegeben es sind noch keine SMTP-Einstellungen gespeichert, dann funktioniert die Registrierung im Spiel
  trotzdem; die Mails werden nicht verschickt, und Spieler können sie später erneut anfordern (003 AK-3).

### Protokoll
- **AK-12:** Gegeben ein Admin ändert Einstellungen, lädt einen Admin ein oder entfernt einen, dann wird festgehalten,
  wer wann was getan hat – ohne Passwörter oder andere Geheimnisse; das Protokoll ist im Admin-Interface einsehbar.

## Out of Scope

- Weitere Admin-Funktionen (Spieler verwalten, sperren, Server-Status, Spielwerte) – eigene Specs
- Mehrere Admin-Rollen mit unterschiedlichen Rechten

## Offene Fragen

_Keine._

## Entschiedene Fragen

- Eigene Admin-Accounts, getrennt von Spieler-Accounts; nur intern erreichbar; eigener Container.
- Anmeldung per Passkey oder Passwort + zweitem Faktor; Wiederherstellungscodes.
- Admins laden Admins ein (einmalig, 48 h) und können sie entfernen; Änderungsprotokoll.
- Ohne SMTP-Einstellungen läuft die Registrierung weiter, Mails gehen verloren.
- Admin-Sitzung: 8 Stunden.

<!-- Keine Technik in dieser Datei. Das gehört in plan.md. -->
