# 025 – Spieltakt nach Features aufteilen

**Status:** bereit  <!-- entwurf | bereit | in arbeit | umgesetzt -->

Setzt voraus: [011 – Chat](../011-chat/spec.md) (Chat im Takt) · [013 – Kampfsystem](../013-kampfsystem/spec.md)
(Angriffe und Projektile im Takt)
Berührt: [014 – Gegner](../014-gegner/spec.md) und [015 – Slash-Commands](../015-slash-commands/spec.md), die den Takt
erweitern

## Ziel

Der Spieltakt des Servers, also alles, was in jedem Tick passiert (Ein- und Austritte, Eingaben, Chat, Bewegung, Kampf,
Snapshots), steht heute vollständig an einer Stelle. Mit 014 und 015 kommen weitere Teile dazu. Damit diese Stelle
nicht mit jedem Feature wächst, bekommt jedes spielnahe Feature seinen Teil des Takts in seinem eigenen Bereich, wie es
[docs/project-structure.md](../../docs/project-structure.md#regeln) für spielnahe Features vorsieht. Der gemeinsame
Takt legt nur noch die Reihenfolge fest.

## User Stories

- Als **Entwickler** möchte ich den Teil des Takts, der zu einem Feature gehört (z. B. Chat oder Kampf), beim Feature
  finden, damit ich es ändern kann, ohne den ganzen Takt zu lesen.
- Als **Entwickler** möchte ich die Reihenfolge eines Ticks an genau einer Stelle sehen, weil der Client sich darauf
  verlässt (z. B. Treffer vor dem Snapshot desselben Ticks, 013 AK-21).
- Als **Spieler** merke ich davon nichts.

## Akzeptanzkriterien

- **AK-1:** Gegeben die Umstellung, dann verhält sich das Spiel für Spieler unverändert; alle bestehenden automatischen
  Tests bleiben grün, ohne dass sich ihre Erwartungen ändern.
- **AK-2:** Gegeben ein Tick, dann erreichen die Nachrichten jede Verbindung in derselben Reihenfolge wie heute: die
  Begrüßung vor dem Chat-Verlauf (011 AK-15), alle Nachrichten eines Ticks vor seinem Snapshot (011 AK-11,
  013 AK-21, AK-22).
- **AK-3:** Gegeben Chat und Kampf, dann liegt ihr Teil des Takts jeweils bei ihrem Feature, und der gemeinsame Takt
  enthält keine Regeln von Chat oder Kampf mehr.
- **AK-4:** Gegeben die Reihenfolge eines Ticks, dann ist sie an genau einer Stelle beschrieben, samt Begründung, wo
  sie wichtig ist (z. B. Bewegung vor Angriffen, damit Reichweite und Blickrichtung aktuell sind).
- **AK-5:** Gegeben ein neues spielnahes Feature (z. B. 014 oder 015), dann ergänzt es seinen Teil des Takts beim
  eigenen Feature und trägt im gemeinsamen Takt nur seinen Platz in der Reihenfolge ein.
- **AK-6:** Gegeben der Teil eines Features, dann lässt er sich ohne Netz und ohne Datenbank testen (Constitution §7).

## Out of Scope

- Geändertes Verhalten oder neue Features.
- Den Takt auf mehrere Threads verteilen oder seine Frequenz ändern.
- Die Welt (Bewegung, HP, Mana) oder den Client aufteilen.

## Offene Fragen

_Keine._

## Entschiedene Fragen

- **Gemeinsam bleibt:** Verbindungen, Ein- und Austritte, Bewegung und Snapshots. Jedes Feature braucht sie, sie
  gehören keinem einzelnen.
- **Vor 014 und 015:** Beide erweitern den Takt; mit dieser Spec vorher wachsen sie gleich in die neue Aufteilung. 014
  und 015 setzen 025 trotzdem nicht voraus; die Roadmap reiht 025 nur davor ein.

<!-- Keine Technik in dieser Datei. Das gehört in plan.md. -->
