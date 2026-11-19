# 023 – Chat-Höchstlänge vom Server

**Status:** bereit  <!-- entwurf | bereit | in arbeit | umgesetzt -->

Setzt voraus: [011 – Chat](../011-chat/spec.md) (Eingabefeld, Höchstlänge AK-9, Verlauf AK-15)
Berührt: [015 – Slash-Commands](../015-slash-commands/spec.md) (Befehle laufen über das Chat-Eingabefeld)

## Ziel

Die Höchstlänge einer Chatnachricht steht nur noch an einer Stelle, und das Spiel im Browser bekommt sie vom Server
([Constitution §8](../constitution.md)). Heute kennen Spielserver, Spiel im Browser und Speicherung je eine eigene
Zahl. Ändert man eine davon, laufen sie auseinander: Das Eingabefeld ließe mehr zu, als der Server annimmt, oder die
Speicherung könnte eine angenommene Nachricht nicht ablegen.

## User Stories

- Als **Betreiber** möchte ich die Höchstlänge einer Chatnachricht an einer Stelle ändern, damit Eingabefeld, Annahme
  und Verlauf immer zusammenpassen.
- Als **Spieler** möchte ich genau so viel tippen können, wie der Server annimmt, damit keine Nachricht unerwartet
  abgelehnt wird.

## Akzeptanzkriterien

- **AK-1:** Gegeben die Standardwerte, dann verhält sich der Chat für Spieler unverändert (011 AK-9).
- **AK-2:** Gegeben die Höchstlänge wurde an der zentralen Stelle geändert und der Server neu gestartet, wenn ich das
  Spiel danach öffne, dann kann ich im Eingabefeld genau bis zur neuen Höchstlänge tippen, ohne dass das Spiel im
  Browser neu gebaut wurde.
- **AK-3:** Gegeben dieselbe Änderung, dann nimmt der Server Nachrichten bis genau zur neuen Höchstlänge an und
  längere nicht, gezählt wie in 011 AK-9.
- **AK-4:** Gegeben die Höchstlänge wurde erhöht, wenn ich eine Nachricht mit der neuen Höchstlänge schicke, dann steht
  sie vollständig im Verlauf, auch nach einem Neustart des Servers (011 AK-15).
- **AK-5:** Gegeben die Höchstlänge wurde gesenkt, dann bleiben schon angenommene, längere Nachrichten im Verlauf
  unverändert sichtbar.

## Out of Scope

- Wo und wie Spielwerte konfiguriert werden (Datei, Admin-Interface): eigene Spec (Constitution §8).
- Andere Chat-Werte wie Tempolimit und Größe des Verlaufs: Die kennt schon heute nur der Server.
- Die Regeln der Tastenbelegung (002), die Spiel und Server je selbst prüfen: feste Regeln der Spec, keine Spielwerte.
- Darstellungswerte des Chatfensters (z. B. wie viele Zeilen die Liste hält): Bedienung, keine Spielwerte.

## Offene Fragen

_Keine._

## Entschiedene Fragen

- **Woher der Wert kommt:** Das Spiel im Browser bekommt ihn beim Betreten des Spiels vom Server, wie Tempo, Max-HP und
  Projektiltempo (Constitution §8).
- **Gesenkte Höchstlänge:** Sie gilt nur für neue Nachrichten; nichts wird nachträglich gekürzt oder entfernt (AK-5).
- **011 AK-9:** Die dort genannte Zahl beschreibt den Standard. Maßgeblich ist danach der zentrale Wert; 011 selbst
  bleibt unverändert.

<!-- Keine Technik in dieser Datei. Das gehört in plan.md. -->
