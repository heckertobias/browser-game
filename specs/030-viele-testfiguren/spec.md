# 030 – Viele Testfiguren

**Status:** entwurf  <!-- entwurf | bereit | in arbeit | umgesetzt -->

Setzt voraus: [015 – Slash-Commands](../015-slash-commands/spec.md) (Test-Befehle) ·
[017 – Spielfigur als Pixel-Art](../017-spielfigur/spec.md) (AK-15)

## Ziel

017 AK-15 verlangt, dass etwa 50 laufende Figuren auf dem Testhandy flüssig laufen. Mit einem Gerät gibt es aber keine
50 Figuren, deshalb ist das AK bisher nicht geprüft. Ein Test-Befehl soll viele laufende Figuren erzeugen, damit sich
die Leistung prüfen lässt.

## User Stories

- Als **Entwickler** möchte ich mit einem Befehl viele laufende Figuren erzeugen, damit ich prüfen kann, ob das Spiel
  dabei flüssig bleibt.

## Akzeptanzkriterien

- **AK-1:** Gegeben ich gebe den Test-Befehl mit einer Anzahl ein, dann erscheinen so viele Figuren, die herumlaufen.
- **AK-2:** Gegeben Testfiguren sind da, dann sehen alle Spieler sie wie echte Figuren.
- **AK-3:** Gegeben ich gebe den Befehl zum Entfernen ein oder der Server startet neu, dann sind die Testfiguren weg;
  gespeichert wird nichts.

## Out of Scope

- Gegner oder Figuren mit eigenem Verhalten (014).
- Messen der Bildrate im Spiel.

## Offene Fragen

- Wer darf den Befehl nutzen: jeder Angemeldete wie bei 015 oder nur Admins?
- Wie laufen die Figuren: zufällig, im Kreis oder auf festen Wegen?
- Obergrenze für die Anzahl?

<!-- Keine Technik in dieser Datei. Das gehört in plan.md. -->
