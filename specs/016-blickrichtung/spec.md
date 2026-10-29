# 016 – Blickrichtung

**Status:** bereit  <!-- entwurf | bereit | in arbeit | umgesetzt -->

Setzt voraus: [001 – Walking Skeleton](../001-walking-skeleton/spec.md) (Bewegung per Tasten und Pointer,
freie Richtungsvektoren)
Voraussetzung für: [014 – Gegner](../014-gegner/spec.md) (Gegner erscheint vor dem Spieler)

## Ziel

Jede Figur hat eine **Blickrichtung**: Sie schaut dorthin, wohin sie zuletzt gelaufen ist. Ein kleiner
**Pfeil** an der Figur macht diese Richtung sichtbar – für den eigenen Spieler und für alle anderen.
Die Richtung ist ein **freier Winkel** (kein 8er-Raster), weil die Bewegung aus 001 ohnehin freie
Richtungsvektoren liefert; der Pfeil dreht also stufenlos mit.

In dieser Spec ist die Blickrichtung **reine Anzeige**. Sie beeinflusst weder Tempo noch Reichweite
oder Treffer. Einzige Ausnahme: [014 – Gegner](../014-gegner/spec.md) nutzt sie, um den Gegner „vor
dem Spieler" zu platzieren.

## User Stories

- Als **Spieler** möchte ich sehen, in welche Richtung meine Figur schaut, damit ich ihre Ausrichtung
  einschätzen kann.
- Als **Spieler** möchte ich, dass die Blickrichtung einfach aus dem Laufen folgt, damit ich keine
  zusätzliche Taste oder Geste lernen muss.
- Als **Spieler** möchte ich die Blickrichtung anderer Figuren sehen, damit ich ihre Ausrichtung
  erkennen kann.
- Als **Entwickler** möchte ich eine verlässliche Blickrichtung, damit 014 den Gegner vor dem
  Spieler erzeugen kann.

## Akzeptanzkriterien

### Entstehung

- **AK-1:** Gegeben meine Figur bewegt sich (Tasten oder Pointer, 001), dann zeigt ihre Blickrichtung
  in die **Richtung der Bewegung** – als freier Winkel, ohne Rasterung auf feste Himmelsrichtungen.
- **AK-2:** Gegeben ich bewege mich in eine andere Richtung, dann dreht die Blickrichtung entsprechend
  mit, auch bei schrägen Winkeln (z. B. 37°).
- **AK-3:** Gegeben ich höre auf zu laufen, dann **behält** die Figur die zuletzt gelaufene
  Blickrichtung bei; sie dreht nicht zurück.
- **AK-4:** Gegeben ich bin neu im Spiel (erster Start bzw. nach Anmeldung), dann hat meine Figur eine
  feste Standard-Blickrichtung (Konstante im Plan), bis ich mich das erste Mal bewege.
- **AK-5:** Gegeben eine Bewegung ist so klein, dass sie praktisch keine Richtung ergibt (Totzone aus
  001), dann ändert sie die Blickrichtung nicht.

### Anzeige

- **AK-6:** Gegeben meine Figur ist sichtbar, dann ist an ihr ein **kleiner Pfeil** zu sehen, der in
  die aktuelle Blickrichtung zeigt; er ist von der Figur und vom Hintergrund gut unterscheidbar.
- **AK-7:** Gegeben ich bewege mich, dann dreht der Pfeil **stufenlos** mit der Blickrichtung mit.
- **AK-8:** Gegeben eine andere Figur ist sichtbar, dann sehe ich auch **deren** Pfeil und damit ihre
  Blickrichtung; die Pfeile anderer Figuren verhalten sich wie der eigene.

### Wirkung & Abgrenzung

- **AK-9:** Gegeben meine Blickrichtung ist beliebig, dann ändern sich **Tempo, Angriffsreichweiten
  und Treffer** dadurch nicht (013 bleibt unverändert); die Blickrichtung ist in dieser Spec nur
  Anzeige.
- **AK-10:** Gegeben ein Gegner wird per `/run spawnEnemy()` erzeugt (014), dann erscheint er **vor**
  dem Spieler, also in dessen Blickrichtung; die Blickrichtung ist damit die einzige mechanische
  Verwendung.
- **AK-11:** Gegeben ich melde mich ab und wieder an, dann startet meine Figur mit der
  Standard-Blickrichtung (AK-4); die Blickrichtung wird **nicht** gespeichert.

### Autorität

- **AK-12:** Gegeben ich bewege mich, dann bestimmt der **Server** die Blickrichtung aus der
  Bewegungsrichtung und verteilt sie an alle; der Client schickt keine eigene Blickrichtungs-Eingabe.

## Out of Scope

- **Feste 8 (oder 4) Richtungen / Rasterung** – die Blickrichtung ist ein freier Winkel.
- Eine **eigene Steuerung** der Blickrichtung (Drehtasten, Mauszeiger im Stehen, Gesten) – sie folgt
  ausschließlich der Bewegung.
- Blickrichtung als **Kampfmechanik** (Treffer nur nach vorne, Sichtkegel, Ausweichen von hinten,
  Richtungsboni) – eigene spätere Spec.
- **Sicht/Aggro** von Gegnern anhand ihrer Blickrichtung (014 greift reine Reichweite an).
- **Sprites/Animationen** je Richtung; sichtbar ist nur der kleine Pfeil.
- **Persistenz** der Blickrichtung im Account (003).
- **Feinere Darstellung** (Kompass, Gradanzeige, Fadenkreuz).

## Offene Fragen

_Keine._

## Entschiedene Fragen

- **Freier Winkel statt 8 Richtungen:** 001 liefert bereits freie Richtungsvektoren; deshalb wird
  nicht auf 8 Himmelsrichtungen gerundet. Der Pfeil dreht stufenlos. (Die ursprüngliche Idee „8
  Richtungen" entfällt damit.)
- **Quelle = letzte Laufrichtung:** Die Blickrichtung folgt der Bewegung (Tasten wie Pointer). Es gibt
  keine zusätzliche Eingabe.
- **Im Stehen bleibt die Richtung:** Kein Zurückdrehen auf eine Standardrichtung, kein Ausblenden des
  Pfeils beim Stehen.
- **Standard-Blickrichtung:** Nach dem ersten Start/Login zeigt die Figur in eine feste Richtung
  (Konstante im Plan), bis zur ersten Bewegung.
- **Für alle sichtbar:** Der Pfeil gehört zur Figur und wird allen Spielern angezeigt (die Richtung
  geht in die Spielstand-Übertragung ein).
- **Nur Anzeige:** Kein Einfluss auf Tempo, Reichweite oder Treffer. Einzige mechanische Verwendung
  ist die Spawn-Richtung in 014.
- **Server ist Autorität** (Constitution 2): Der Client schickt nur die Bewegungsrichtung (001); der
  Server leitet daraus die Blickrichtung ab und verteilt sie.
- **Nicht gespeichert:** Die Blickrichtung gilt pro Session und startet nach erneutem Anmelden wieder
  mit der Standardrichtung.
- **Alle Zahlen sind Konstanten im Plan** (Standard-Blickrichtung, Darstellungsgröße/Abstand des
  Pfeils); die Spec nennt keine Zahlen.

<!-- Keine Technik in dieser Datei. Das gehört in plan.md. -->
