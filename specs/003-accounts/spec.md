# 003 – Accounts & gespeicherte Position

**Status:** bereit  <!-- entwurf | bereit | in arbeit | umgesetzt -->

Voraussetzung für: [002 – Tastenbelegung & erstes Menü](../002-keybindings/spec.md)

## Ziel

Spieler haben eine eigene, dauerhafte Identität: Sie melden sich an, spielen unter ihrem gewählten Namen und
kommen dort wieder heraus, wo sie aufgehört haben. Das ist die Grundlage für alles, was zum Spieler gehört
(Einstellungen, später Haus und Offline-Fortschritt).

## User Stories

- Als **neuer Spieler** möchte ich mir mit E-Mail und Passwort oder einem Passkey einen Account anlegen und
  einen eigenen Namen wählen, damit andere mich wiedererkennen.
- Als **Spieler** möchte ich mich auf jedem Gerät anmelden können und dort weiterspielen, wo ich aufgehört habe.
- Als **Spieler** möchte ich mein Passwort zurücksetzen können, wenn ich es vergessen habe.
- Als **Spieler** möchte ich mich abmelden und meinen Account samt aller Daten löschen können.

## Akzeptanzkriterien

### Registrierung & Name
- **AK-1:** Gegeben ich bin nicht angemeldet, wenn ich auf „Spielen“ klicke, dann werde ich zur Anmeldung geführt
  und komme erst danach ins Spiel.
- **AK-2:** Gegeben ich habe noch keinen Account, wenn ich E-Mail, Passwort und Spielernamen angebe, dann wird
  mein Account angelegt und ich bekomme eine Mail mit einem Bestätigungslink.
- **AK-3:** Gegeben meine E-Mail-Adresse ist noch nicht bestätigt, dann kann ich nicht spielen und kann mir den
  Bestätigungslink erneut schicken lassen. Nach dem Klick auf den Link kann ich spielen.
- **AK-4:** Gegeben ich lege einen Account an, wenn ich statt eines Passworts einen Passkey verwende, dann kann ich
  mich künftig allein mit dem Passkey anmelden.
- **AK-5:** Gegeben ich wähle einen Spielernamen, dann muss er 3 bis 16 Zeichen lang sein und darf nur aus den
  Buchstaben A–Z bestehen (keine Umlaute, Ziffern, Leer- oder Sonderzeichen); sonst wird er mit verständlicher
  Begründung abgelehnt.
- **AK-6:** Gegeben ich gebe einen Namen in beliebiger Schreibweise ein (z. B. „tObIaS“), dann wird er als
  „Tobias“ gespeichert und angezeigt: erster Buchstabe groß, der Rest klein.
- **AK-7:** Gegeben ein Spielername ist schon vergeben, wenn ich ihn wähle, dann wird er abgelehnt und ich kann
  einen anderen wählen.
- **AK-7b:** Gegeben ein Name steht auf der Sperrliste (reservierte Namen wie „Admin“, „System“, „Moderator“,
  „Support“, „Server“ sowie eine kurze Liste klarer Beleidigungen), dann wird er abgelehnt. Geprüft wird der ganze
  Name, unabhängig von der Schreibweise.
- **AK-8:** Gegeben ich bin angemeldet und im Spiel, dann sehen andere Spieler meinen gewählten Namen über meiner Figur.

### Anmeldung & Sitzung
- **AK-9:** Gegeben ich habe einen Account, wenn ich mich mit falschen Daten anmelde, dann werde ich abgewiesen,
  ohne zu erfahren, ob es die E-Mail-Adresse gibt.
- **AK-10:** Gegeben ich versuche sehr oft hintereinander, mich anzumelden, dann werden weitere Versuche für eine
  Weile gebremst.
- **AK-11:** Gegeben ich habe mich angemeldet, dann bleibe ich auf diesem Gerät 30 Tage angemeldet, solange ich mich
  nicht abmelde.
- **AK-12:** Gegeben ich bin auf Gerät A im Spiel, wenn ich mich auf Gerät B anmelde und spiele, dann wird Gerät A
  aus dem Spiel genommen und sieht den Hinweis „Auf einem anderen Gerät angemeldet“. Meine Figur ist nur einmal in der Welt.
- **AK-13:** Gegeben ich bin angemeldet, wenn ich mich abmelde, dann ist meine Sitzung beendet und ich muss mich
  zum Weiterspielen erneut anmelden.

### Anmeldearten verwalten
- **AK-13b:** Gegeben ich bin angemeldet, dann kann ich in meinem Account weitere Passkeys hinzufügen (z. B. Handy
  und Laptop) und einzelne wieder entfernen; ein Account, der nur Passkeys hat, kann ein Passwort setzen.
- **AK-13c:** Gegeben ich entferne eine Anmeldeart, dann bleibt mindestens eine (Passwort oder Passkey) übrig;
  die letzte kann ich nicht entfernen.

### Passwort vergessen
- **AK-14:** Gegeben ich habe mein Passwort vergessen, wenn ich meine E-Mail-Adresse angebe, dann bekomme ich eine
  Mail mit einem Link zum Zurücksetzen. Die Seite verrät nicht, ob es die Adresse gibt.
- **AK-15:** Gegeben ich habe einen Link zum Zurücksetzen, dann funktioniert er nur einmal und nur für begrenzte
  Zeit; danach kann ich mich mit dem neuen Passwort anmelden, und alle anderen Sitzungen sind beendet.

### Account löschen
- **AK-16:** Gegeben ich bin angemeldet, wenn ich „Account löschen“ wähle und mit Passwort oder Passkey bestätige,
  dann werden mein Account und alle zugehörigen Daten gelöscht, ich bin abgemeldet und mein Name ist wieder frei.

### Position
- **AK-17:** Gegeben ich spiele zum ersten Mal, dann starte ich am Startpunkt der Welt.
- **AK-18:** Gegeben ich verlasse das Spiel an einer Stelle, wenn ich mich später erneut anmelde (auch auf einem
  anderen Gerät), dann starte ich an derselben Stelle.
- **AK-19:** Gegeben der Server wird neu gestartet, dann geht höchstens die Bewegung der letzten paar Sekunden verloren.

### Spiel verlassen
- **AK-20:** Gegeben ich schließe den Tab oder verliere die Verbindung, dann bleibt meine Figur für andere noch
  20 Sekunden an ihrer Stelle in der Welt, steht still und ist sichtbar anders eingefärbt (z. B. ausgegraut);
  danach verschwindet sie. (Ersetzt 001 AK-4.)
- **AK-21:** Gegeben meine Figur wartet nach einem Verbindungsverlust noch, wenn ich innerhalb der 20 Sekunden
  wieder ins Spiel komme (z. B. Tab neu geladen), dann übernehme ich nahtlos dieselbe Figur an derselben Stelle,
  und sie sieht für andere wieder normal aus.
- **AK-22:** Gegeben ich melde mich bewusst ab, dann verschwindet meine Figur sofort.
- **AK-23:** Gegeben ich komme nach mehr als 20 Sekunden zurück, dann starte ich an der Stelle, an der meine Figur
  zuletzt stand.

## Out of Scope

- Login über Google, Discord, GitHub o. Ä.
- Spielernamen ändern (später, ggf. mit Abklingzeit)
- Erkennen verbotener Wortteile in Namen (z. B. Beleidigung als Teil eines längeren Namens) – langfristig nötig,
  eigene Spec; vorerst nur die Sperrliste mit ganzen Namen
- E-Mail-Adresse ändern
- Mehrere Figuren pro Account
- Rollen/Rechte (Admin, Moderation)
- Zonen, in denen man sich sofort abmelden kann, und Gefahr während der 20 Sekunden (z. B. sterben) – später
- Eigene Darstellung für wartende Figuren (z. B. hinsetzen) – kommt mit Sprites
- Tastenbelegung (→ 002), Haus und Offline-Fortschritt (eigene Specs)

## Offene Fragen

_Keine._

## Entschiedene Fragen

- Kein Gastmodus; Anmeldung per E-Mail + Passwort oder Passkey.
- Name: 3–16 Buchstaben A–Z, Schreibweise wird vereinheitlicht (erster groß, Rest klein), eindeutig, nicht änderbar.
- E-Mail-Bestätigung, Passwort vergessen und Account löschen gehören zu dieser Spec.
- Eine neue Anmeldung im Spiel wirft die alte hinaus.
- Nach Tab schließen/Verbindungsabbruch bleibt die Figur 20 s (anders eingefärbt); bewusstes Abmelden: sofort weg.
- Angemeldet bleiben: 30 Tage.
- Sperrliste: kleine Liste ganzer Namen (reserviert + klare Beleidigungen); Wortteil-Erkennung später.
- Passwort und beliebig viele Passkeys pro Account, mindestens eine Anmeldeart bleibt.

<!-- Keine Technik in dieser Datei. Das gehört in plan.md. -->
