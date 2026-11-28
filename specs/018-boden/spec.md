# 018 – Boden statt Gitter

**Status:** in arbeit  <!-- entwurf | bereit | in arbeit | umgesetzt -->

Setzt voraus: [017 – Spielfigur als Pixel-Art](../017-spielfigur/spec.md) (eigenes Grafik-Format, Lizenzregel)

## Ziel

Die Welt hat einen **Boden** aus Pixel-Art statt des Gitters auf einfarbigem Grund. Der Boden folgt einer **festen
Karte**. Die Welt besteht aus **Gebieten** mit eigenem Thema, die durch Berge, Wasser o. Ä. voneinander getrennt
sind. Im ersten Lauf gibt es **ein Gebiet**: eine Graslandschaft.

Grafiken stammen aus Ninja Adventure oder folgen seinem Stil und liegen im eigenen Format aus 017. Der Boden ist über
benannte Bodenstücke beschrieben, nicht über den Aufbau des Packs.

## User Stories

- Als **Spieler** möchte ich eine Landschaft statt eines Gitters sehen, damit sich die Welt wie ein Ort
  anfühlt.
- Als **Spieler** möchte ich an Wegen, dem See und den Bergen erkennen, wo ich bin, damit ich mich orientieren kann.
- Als **Entwickler** möchte ich die Karte als eigene, bearbeitbare Beschreibung haben, damit sie später von Hand oder
  mit einem Editor geändert werden kann, ohne Code zu ändern.

## Akzeptanzkriterien

- **AK-1:** Gegeben ich bin im Spiel, dann sehe ich einen Pixel-Art-Boden und kein Gitter mehr.
- **AK-2:** Gegeben ein beliebiger Bildschirm (007), dann ist der Boden scharf und lückenlos; zwischen den
  Bodenstücken sind keine Nähte oder Linien zu sehen.
- **AK-3:** Gegeben zwei Spieler stehen an derselben Stelle, dann sehen beide denselben Boden, auch nach einem
  Neustart oder Update des Servers, solange die Karte nicht geändert wurde.
- **AK-4:** Gegeben die Karte des ersten Gebiets, dann ist sie 10 × 8 Bildschirme groß (1 Bildschirm = das übliche
  16:9-Sichtfeld aus [ADR 0010](../../docs/decisions/0010-sichtfeld-kurze-seite.md)) und zeigt eine Graslandschaft mit Wegen und
  einem See; ringsum am Kartenrand liegen Berge.
- **AK-5:** Gegeben ich laufe über den Kartenrand hinaus, dann setzt sich der Boden am Rand endlos fort: Endet die
  Karte in Bergen, gehen die Berge weiter; endet sie in Wasser, geht das Wasser weiter. Ich sehe nie leere Fläche.
- **AK-6:** Gegeben die Karte, dann liegt sie als eigene Beschreibung vor, die man ohne Codeänderung bearbeiten kann;
  eine geänderte Beschreibung ändert den Boden im Spiel.
- **AK-7:** Gegeben ich bin im Spiel, dann ist der Startpunkt sichtbar markiert, und die Markierung ist klar als
  vorläufige Testhilfe zu erkennen (z. B. auffällig, nicht im Stil der Welt).

## Out of Scope

- **Kollision:** Berge und Wasser blockieren in dieser Spec noch nichts; man kann hindurchlaufen. Das kommt mit
  [021 – Kollision](../021-kollision/spec.md).
- **Schwimmen** (später; unendliches Wasser jenseits des Kartenrands wird dann über Erschöpfung begrenzt).
- **Weitere Gebiete** und Übergänge zwischen Gebieten.
- **Abwechslungsreiche Berge:** Im ersten Lauf ist der Bergrand ein einziges, wiederholtes Bodenstück.
- **Editor** für die Karte (später, z. B. im Admin).
- Deko-Objekte wie Bäume und Büsche ([019](../019-deko/spec.md)).

## Offene Fragen

_Keine._

## Entschiedene Fragen

- **Feste Karte statt Regeln:** Die Welt ist eine feste Karte. Für den Anfang wird das erste Gebiet einmal nach einem
  Schema erzeugt und als bearbeitbare Beschreibung abgelegt (AK-6). Später soll ein Editor sie ändern können.
- **Gebiete:** Die Welt wird in Gebiete mit eigenem Thema unterteilt, getrennt durch Berge, Wasser o. Ä. Erster Lauf:
  ein Gebiet.
- **Größe:** 10 × 8 Bildschirme, gemessen am üblichen 16:9-Sichtfeld (ADR 0010). Die Berge am Rand gehören zur Karte.
- **Jenseits der Karte:** Allgemeine Regel: Der äußerste Rand der Karte wird endlos fortgesetzt (AK-5). Die Welt
  bleibt damit ohne sichtbares Ende; [001](../001-walking-skeleton/spec.md) AK-7 („keine Grenze“) gilt bis 021 weiter.
- **Bodenarten im ersten Gebiet:** Gras (mit Varianten), Wege, ein See, Berge am Rand. Bäume und Büsche sind Deko
  (019).
- **Berge:** zunächst ein einziges Bodenstück, wiederholt; die Breite des Bergstreifens legt der Plan fest.
- **Kollision:** eigene Spec (021); 018 ist nur Karte und Darstellung.
- **Startpunkt:** wird für Tests sichtbar markiert, aber so, dass klar ist, dass das nur vorläufig ist (AK-7). Wo er
  auf der Karte liegt, legt der Plan fest.

<!-- Keine Technik in dieser Datei. Das gehört in plan.md. -->
