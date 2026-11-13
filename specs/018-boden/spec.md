# 018 – Boden statt Gitter

**Status:** entwurf  <!-- entwurf | bereit | in arbeit | umgesetzt -->

Setzt voraus: [017 – Spielfigur als Pixel-Art](../017-spielfigur/spec.md) (Asset-Pack, Lizenzregel)

## Ziel

Die Welt hat einen **Boden** aus Pixel-Art (z. B. Gras, Wege) statt des Gitters auf einfarbigem Grund.
Grafiken stammen aus Ninja Adventure oder folgen seinem Stil (017).

## User Stories

- Als **Spieler** möchte ich eine Landschaft statt eines Gitters sehen, damit sich die Welt wie ein Ort
  anfühlt.
- Als **Spieler** möchte ich an Wegen und Flächen erkennen, wo ich bin, damit ich mich orientieren kann.

## Akzeptanzkriterien

- **AK-1:** Gegeben ich bin im Spiel, dann sehe ich einen Pixel-Art-Boden und kein Gitter mehr.
- **AK-2:** Gegeben ein beliebiger Bildschirm (007), dann ist der Boden scharf und lückenlos; zwischen den
  Bodenstücken sind keine Nähte oder Linien zu sehen.

## Out of Scope

- Hindernisse und Kollision (z. B. Wasser, das man nicht betreten kann).
- Deko-Objekte (019).

## Offene Fragen

- [ ] Ist die Welt eine fest gezeichnete Karte oder wird der Boden aus Regeln erzeugt?
- [ ] Wie groß ist die Welt, und was sieht man an ihrem Rand?
- [ ] Welche Bodenarten gibt es im ersten Lauf (nur Gras, oder auch Wege, Sand, Wasser)?
- [ ] Gibt es Wasser oder andere Flächen, die man nicht betreten kann? Dann braucht es Kollision (eigene Spec?).
- [ ] Soll der Startpunkt (001/003) sichtbar markiert sein?

<!-- Keine Technik in dieser Datei. Das gehört in plan.md. -->
