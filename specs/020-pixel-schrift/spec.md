# 020 – Pixel-Schrift

**Status:** entwurf  <!-- entwurf | bereit | in arbeit | umgesetzt -->

Setzt voraus: [001 – Walking Skeleton](../001-walking-skeleton/spec.md) (Namen über Figuren)

## Ziel

Texte im Spiel nutzen eine **pixelige Schrift**, wie die Vision es für den UI-Stil vorsieht: gut lesbar,
mit Pixel-Akzenten. Die Lizenzregel aus [017](../017-spielfigur/spec.md) gilt auch für Schriften.

## User Stories

- Als **Spieler** möchte ich, dass Namen und Anzeigen zum Pixel-Stil der Welt passen.
- Als **Spieler** möchte ich alle Texte trotzdem gut lesen können, auch auf dem Handy.

## Akzeptanzkriterien

- **AK-1:** Gegeben eine Figur ist sichtbar, dann steht ihr Name in der Pixel-Schrift über ihr und ist
  scharf und gut lesbar.
- **AK-2:** Gegeben ein Name mit Umlauten oder ß, dann werden alle Zeichen dargestellt.
- **AK-3:** Gegeben die Schrift steht nicht unter CC0, dann sind ihre Lizenz und Herkunft wie bei den Grafiken
  (017) nachgewiesen und werden mit ausgeliefert.

## Out of Scope

- HUD (012), Chat (011), Menüs (002) und alle übrige Spiel-UI: vorerst nicht; erst wird die Schrift an den Namen
  erprobt.
- Öffentliche Seiten (Landing, Anmeldung).
- Admin-Interface (bleibt sachlich, 005).
- Rahmen und Icons im Pixel-Stil – eigene spätere Spec.

## Offene Fragen

_Keine._

## Entschiedene Fragen

- **Nur Namen über Figuren:** Die Pixel-Schrift wird zunächst nur dort erprobt. Bewährt sie sich, folgt eine
  Erweiterung (HUD, Chat, Menüs) in einer eigenen Spec.
- **Keine öffentlichen Seiten**, und die Lesbarkeit für lange Texte (Chat) spielt damit vorerst keine Rolle.
- **Lizenz:** Für Schriften ist neben CC0 auch die SIL Open Font License (OFL) zulässig (Lizenztext mitliefern,
  Schrift nicht einzeln verkaufen, geänderte Fassungen nicht unter dem Originalnamen). Die Lizenzregel aus 017
  (dort als ADR geplant) bekommt dafür diese Ausnahme. Gewählt wird die Schrift, die am besten lesbar ist und alle
  nötigen Zeichen hat.

<!-- Keine Technik in dieser Datei. Das gehört in plan.md. -->
