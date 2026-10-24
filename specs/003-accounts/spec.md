# 003 – Accounts & gespeicherte Position

**Status:** entwurf  <!-- entwurf | bereit | in arbeit | umgesetzt -->

Voraussetzung für: [002 – Tastenbelegung & erstes Menü](../002-keybindings/spec.md)

## Ziel

Spieler haben eine eigene, dauerhafte Identität: Sie melden sich an, spielen unter ihrem gewählten Namen und
kommen dort wieder heraus, wo sie aufgehört haben. Das ist die Grundlage für alles, was zum Spieler gehört
(Einstellungen, später Haus und Offline-Fortschritt).

## User Stories

- Als **neuer Spieler** möchte ich mir mit E-Mail und Passwort oder einem Passkey einen Account anlegen und
  einen eigenen Namen wählen, damit andere mich wiedererkennen.
- Als **Spieler** möchte ich mich auf jedem Gerät anmelden können und dort weiterspielen, wo ich aufgehört habe.
- Als **Spieler** möchte ich mich abmelden können, z. B. an einem fremden Gerät.

## Akzeptanzkriterien

- **AK-1:** Gegeben ich bin nicht angemeldet, wenn ich auf „Spielen“ klicke, dann werde ich zur Anmeldung geführt
  und komme erst danach ins Spiel.
- **AK-2:** Gegeben ich habe noch keinen Account, wenn ich E-Mail, Passwort und Spielernamen angebe, dann wird
  mein Account angelegt und ich bin angemeldet.
- **AK-3:** Gegeben ich lege einen Account an, wenn ich statt eines Passworts einen Passkey verwende, dann kann ich
  mich künftig allein mit dem Passkey anmelden.
- **AK-4:** Gegeben ein Spielername ist schon vergeben (unabhängig von Groß-/Kleinschreibung), wenn ich ihn wähle,
  dann wird er abgelehnt und ich kann einen anderen wählen.
- **AK-5:** Gegeben ein Spielername verstößt gegen die Namensregeln, dann wird er mit einer verständlichen Begründung abgelehnt.
- **AK-6:** Gegeben ich habe einen Account, wenn ich mich mit falschen Daten anmelde, dann werde ich abgewiesen,
  ohne zu erfahren, ob es die E-Mail-Adresse gibt.
- **AK-7:** Gegeben ich versuche sehr oft hintereinander, mich anzumelden, dann werden weitere Versuche für eine
  Weile gebremst.
- **AK-8:** Gegeben ich bin angemeldet und im Spiel, dann sehen andere Spieler meinen gewählten Namen über meiner Figur.
- **AK-9:** Gegeben ich verlasse das Spiel an einer Stelle, wenn ich mich später erneut anmelde (auch auf einem
  anderen Gerät), dann starte ich an derselben Stelle.
- **AK-10:** Gegeben der Server wird neu gestartet, dann geht höchstens die Bewegung der letzten paar Sekunden verloren.
- **AK-11:** Gegeben ich spiele zum ersten Mal, dann starte ich am Startpunkt der Welt.
- **AK-12:** Gegeben ich bin angemeldet, wenn ich mich abmelde, dann ist meine Sitzung beendet und ich muss mich
  zum Weiterspielen erneut anmelden.

## Out of Scope

- Login über Google, Discord, GitHub o. Ä.
- Mehrere Figuren pro Account
- Rollen/Rechte (Admin, Moderation)
- Tastenbelegung (→ 002), Haus und Offline-Fortschritt (eigene Specs)

## Offene Fragen

- [ ] Namensregeln: Länge (z. B. 3–16 Zeichen) und erlaubte Zeichen (nur Buchstaben/Ziffern? Umlaute? Leerzeichen,
      `-`, `_`)? Gibt es gesperrte Namen (z. B. „Admin“, Beleidigungen)?
- [ ] Muss die E-Mail-Adresse bestätigt werden, bevor man spielen kann? (Braucht Mailversand.)
- [ ] Passwort vergessen: Zurücksetzen per Mail schon in dieser Spec? (Braucht ebenfalls Mailversand.)
- [ ] Kann man den Spielernamen später ändern?
- [ ] Gleichzeitig auf zwei Geräten mit demselben Account im Spiel: erlaubt, oder wirft die neue Anmeldung die alte hinaus?
- [ ] Wie lange bleibt man angemeldet (z. B. 30 Tage „angemeldet bleiben“)?
- [ ] Account löschen (inkl. aller Daten) – schon in dieser Spec? Bei einem öffentlich erreichbaren Spiel ist das
      rechtlich (DSGVO) früher oder später Pflicht.
- [ ] Kann man einem Passwort-Account später einen Passkey hinzufügen (und umgekehrt)?

<!-- Keine Technik in dieser Datei. Das gehört in plan.md. -->
