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
- **AK-2:** Gegeben ein Text in Deutsch oder Englisch (006), dann werden alle Zeichen dargestellt,
  auch Umlaute und ß.

## Out of Scope

- Admin-Interface (bleibt sachlich, 005).
- Rahmen und Icons im Pixel-Stil – eigene spätere Spec.

## Offene Fragen

- [ ] Wo gilt die Schrift: nur Namen über Figuren, oder auch HUD (012), Chat (011) und Menüs (002)?
- [ ] Auch auf den öffentlichen Seiten (Landing, Anmeldung)?
- [ ] Welche Schrift? Schriften stehen oft unter der SIL Open Font License statt CC0. Ist die zulässig, oder
  gilt strikt CC0 (dann ADR aus 017 anpassen)?
- [ ] Ist die Schrift für lange Texte (Chat) lesbar genug, oder nur für Überschriften und Namen?

<!-- Keine Technik in dieser Datei. Das gehört in plan.md. -->
