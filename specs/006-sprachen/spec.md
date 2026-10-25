# 006 – Deutsch und Englisch

**Status:** umgesetzt  <!-- entwurf | bereit | in arbeit | umgesetzt -->

Setzt voraus: [001 – Walking Skeleton](../001-walking-skeleton/spec.md) (HUD) ·
[004 – Admin-Interface & SMTP-Einstellungen](../004-admin/spec.md) (Mailversand) ·
Wird eingeschoben in: [003 – Accounts & gespeicherte Position](../003-accounts/spec.md) (nach T7; die restlichen
Account-Seiten entstehen danach gleich zweisprachig)

## Ziel

Spieler können das Spiel auf Deutsch oder Englisch nutzen. Ohne eigenes Zutun sehen sie die Sprache ihres Browsers;
wer eine andere möchte, schaltet um und behält seine Wahl auf allen Geräten. Damit ist das Spiel nicht mehr auf
deutschsprachige Spieler beschränkt.

## User Stories

- Als **neuer Spieler** möchte ich das Spiel gleich in meiner Sprache sehen, ohne etwas einstellen zu müssen.
- Als **Spieler** möchte ich die Sprache jederzeit umschalten können.
- Als **Spieler** möchte ich, dass meine gewählte Sprache auf jedem Gerät gilt, an dem ich angemeldet bin.
- Als **Spieler** möchte ich Mails vom Spiel in meiner Sprache bekommen.

## Akzeptanzkriterien

### Sprache beim ersten Besuch
- **AK-1:** Gegeben ich habe noch keine Sprache gewählt, wenn mein Browser Deutsch als bevorzugte Sprache meldet,
  dann sehe ich alles auf Deutsch.
- **AK-2:** Gegeben ich habe noch keine Sprache gewählt, wenn mein Browser eine andere Sprache als Deutsch meldet
  (Englisch oder eine dritte), dann sehe ich alles auf Englisch.

### Umschalten und merken
- **AK-3:** Gegeben ich bin auf einer Spieler-Seite (Startseite, Anmeldung, Registrierung, Account), dann kann ich
  dort zwischen „Deutsch“ und „English“ umschalten; die Seite erscheint sofort neu in der gewählten Sprache.
  Was ich in ein Formular schon eingegeben habe, geht dabei verloren; deshalb werde ich in dem Fall vorher gefragt
  und kann abbrechen.
- **AK-4:** Gegeben ich habe auf einem Gerät umgeschaltet, ohne angemeldet zu sein, dann bleibt diese Sprache auf
  diesem Gerät, auch nach einem Neustart des Browsers.
- **AK-5:** Gegeben ich bin angemeldet und schalte um, dann wird die Sprache in meinem Account gespeichert.
- **AK-6:** Gegeben in meinem Account ist eine Sprache gespeichert, wenn ich mich auf einem anderen Gerät anmelde,
  dann sehe ich ab der Anmeldung meine gespeicherte Sprache, unabhängig von Browser und Gerät.
- **AK-7:** Gegeben ich lege einen Account an, dann wird die Sprache, in der ich mich registriere, als meine Sprache
  gespeichert.

### Im Spiel
- **AK-8:** Gegeben ich bin im Spiel, dann sind alle Texte im Spiel (HUD, Hinweise wie „Auf einem anderen Gerät
  angemeldet“) in meiner Sprache.
- **AK-8b:** Gegeben ich bin im Spiel, dann gibt es dort keinen Sprachumschalter. Um die Sprache zu wechseln, verlasse
  ich das Spiel, schalte auf einer Spieler-Seite um und komme danach in der neuen Sprache zurück.

### Mails
- **AK-9:** Gegeben das Spiel schickt mir eine Mail (Bestätigung, erneute Bestätigung, Passwort zurücksetzen,
  Hinweis „Du hast schon einen Account“), dann ist sie in der Sprache meines Accounts.

### Vollständigkeit
- **AK-10:** Gegeben ich nutze eine der beiden Sprachen, dann ist jeder Text, den ich als Spieler sehe, in dieser
  Sprache, auch Fehlermeldungen und Hinweise. Es gibt keine Seiten mit gemischten Sprachen.
- **AK-11:** Gegeben ich habe einen Spielernamen, dann bleibt er in jeder Sprache gleich. Die Regeln für Namen
  (003 AK-5 bis AK-7b) gelten unverändert.

## Out of Scope

- Weitere Sprachen. Neue Sprachen sollen aber später ohne Umbau ergänzt werden können.
- Admin-Interface: wird mit [005](../005-admin-layout/spec.md) englisch. Eine deutsche Fassung soll später ohne
  Umbau ergänzt werden können.
- Übersetzung von Inhalten, die Spieler selbst schreiben (Namen, später Chat)
- Sprachabhängige Formate für Zahlen, Datum oder Uhrzeit, solange das Spiel keine anzeigt
- Sprachumschalter im Spiel selbst. Er gehört in ein Menü, nicht direkt ins HUD; kommt frühestens mit dem Menü
  aus [002](../002-keybindings/spec.md).

## Offene Fragen

_Keine._

## Entschiedene Fragen

- Sprachen: Deutsch und Englisch.
- Ohne Wahl: Sprache des Browsers; alles außer Deutsch ergibt Englisch.
- Umschalter; die Wahl gilt auf dem Gerät und wird bei angemeldeten Spielern im Account gespeichert.
- Umfang: Spieler-Seiten, Mails an Spieler, Spiel-Client. Admin nur Englisch, später lokalisierbar.
- Admin auf Englisch umstellen gehört zu 005, das ohnehin jede Admin-Seite anfasst.
- Umschalten nur auf Startseite, Anmeldung, Registrierung und Account; im Spiel ist immer genau eine Sprache aktiv.
  Ein Umschalter im Spiel gehört in ein Menü, nie direkt ins HUD.
- Reihenfolge: direkt nach 003 T7; 003 pausiert so lange.
- Umschalten lädt die Seite neu; Formulareingaben werden nicht gemerkt (KISS), aber es gibt vorher eine Nachfrage.

<!-- Keine Technik in dieser Datei. Das gehört in plan.md. -->
