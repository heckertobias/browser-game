# 008 – Push-Benachrichtigungen

**Status:** entwurf  <!-- entwurf | bereit | in arbeit | umgesetzt -->

Setzt voraus: [007 – Handy hochkant und installierbare Web-App](../007-mobil/spec.md) · eine Spec mit einem ersten
echten Anlass (voraussichtlich Haus des Spielers / Offline-Produktion)

## Ziel

Spieler erfahren auch dann, wenn sie nicht im Spiel sind, dass etwas für sie passiert ist – etwa dass ihr Haus etwas
produziert hat. So kommen sie zurück, wenn es sich lohnt, ohne ständig nachsehen zu müssen.

## User Stories

- Als **Spieler** möchte ich auf Wunsch eine Benachrichtigung bekommen, wenn im Offline-Teil etwas für mich fertig ist.
- Als **Spieler** möchte ich selbst entscheiden, ob und wofür ich Benachrichtigungen bekomme, und sie jederzeit
  abschalten können.

## Akzeptanzkriterien

_Vorläufig – die Anlässe (offene Frage) fehlen noch; „ein Anlass“ steht für den ersten echten Anlass._

- **AK-1:** Gegeben ich besuche das Spiel zum ersten Mal, dann werde ich nicht nach der Erlaubnis für
  Benachrichtigungen gefragt.
- **AK-2:** Gegeben auf meinem Gerät entsteht zum ersten Mal ein Anlass, für den es Benachrichtigungen gibt, dann
  werde ich gefragt, ob ich dafür benachrichtigt werden möchte; lehne ich ab, werde ich nicht erneut gefragt.
- **AK-3:** Gegeben ich bin in den Einstellungen, dann kann ich Benachrichtigungen für dieses Gerät jederzeit an- und
  abschalten und die Anlässe wählen.
- **AK-4:** Gegeben ich nutze mehrere Geräte, dann gilt die Einstellung nur für das Gerät, auf dem ich sie ändere.
- **AK-5:** Gegeben ich spiele auf dem iPhone im Safari-Tab, dann sehe ich statt des Schalters den Hinweis, dass
  Benachrichtigungen nur in der zum Homescreen hinzugefügten Web-App (007) möglich sind.
- **AK-6:** Gegeben ein Anlass tritt während meiner Ruhezeit ein, dann bekomme ich die Benachrichtigung erst nach
  ihrem Ende. Die Ruhezeit ist pro Gerät einstellbar, Standard 22–7 Uhr.
- **AK-7:** Gegeben mehrere Anlässe treten kurz hintereinander (oder während der Ruhezeit) ein, dann bekomme ich eine
  gebündelte Benachrichtigung statt mehrerer einzelner.
- **AK-8:** Gegeben eine Benachrichtigung, dann ist sie in der Sprache meines Accounts (006).

## Out of Scope

- Werbung oder Benachrichtigungen ohne Bezug zum eigenen Spielstand
- Höchstzahl an Benachrichtigungen pro Tag (gibt es nicht)
- E-Mail-Benachrichtigungen: vorerst nicht, für später denkbar
- SMS-Benachrichtigungen

## Offene Fragen

- [ ] Welche Anlässe gibt es zum Start (Haus, Expedition, Handwerk, …)? Hängt an der Spec zum Offline-Teil.

## Entschiedene Fragen

- **Wann wird gefragt:** nie ungefragt beim ersten Besuch; beim ersten passenden Anlass, und zusätzlich jederzeit
  über einen Schalter in den Einstellungen.
- **Pro Gerät:** An/Aus, Anlässe und Ruhezeit gelten je Gerät.
- **iPhone:** Benachrichtigungen nur in der Homescreen-Web-App; das reicht. Im Safari-Tab gibt es einen Hinweis.
- **Ruhezeiten und Bündeln:** ja; Ruhezeit pro Gerät einstellbar (Standard 22–7 Uhr), keine Höchstzahl pro Tag.
- **Sprache:** die des Accounts, wie bei Mails (006).

<!-- Keine Technik in dieser Datei. Das gehört in plan.md. -->
