# ADR 0003 – Logische Auflösung 640×360

**Status:** ersetzt durch [ADR 0010](0010-sichtfeld-kurze-seite.md) (2026-10-26)

## Kontext

Laut Vision sehen alle Spieler einen gleich großen Weltausschnitt, und ein größerer Bildschirm zeigt dasselbe nur
schärfer. Gerendert wird also in einer festen logischen Auflösung und dann hochskaliert. Pixel-Art sieht nur bei
**ganzzahliger** Skalierung sauber aus; sonst werden Pixel unterschiedlich breit.
_(Die [Vision](../../specs/000-vision/spec.md) wurde mit [007](../../specs/007-mobil/spec.md) geändert: Das Spielbild
füllt das Fenster ohne Ränder, nur die kürzere Seite zeigt überall etwa gleich viel Welt. Siehe ADR 0010.)_

Ursprünglich war 800×600 (4:3) als Beispiel genannt. Fast alle heutigen Bildschirme sind 16:9, und 800×600 skaliert
auf keine gängige Auflösung ganzzahlig.

## Entscheidung

Logische Auflösung **640×360** (16:9). Sie skaliert ganzzahlig auf alle gängigen Bildschirme:

| Bildschirm | Faktor |
|---|---|
| 1280×720 | ×2 |
| 1920×1080 | ×3 |
| 2560×1440 | ×4 |
| 3840×2160 (4K) | ×6 |

- Skaliert wird mit dem größten ganzzahligen Faktor, der ins Fenster passt; der Rest bekommt Ränder (Letterbox).
  Ist das Fenster kleiner als 640×360, wird ausnahmsweise fraktional verkleinert.
- Hochauflösende Texturen (z. B. für 4K) sind später möglich, ohne den sichtbaren Weltausschnitt zu ändern.
- Der Wert steht im Client an genau einer Stelle als Konstante.

## Konsequenzen

- Sprites werden auf ein Raster ausgelegt, das zu 640×360 passt (z. B. 16×16-Figuren).
- Fenster mit anderem Seitenverhältnis (Ultrawide, Hochformat am Handy) zeigen Ränder statt mehr Welt. Das ist
  gewollt (Fairness), kostet aber Fläche; im Hochformat am Handy ist das Bild klein.
- Eine spätere Änderung des Werts ändert, wie viel Welt man sieht. Das betrifft Balancing und braucht ein neues ADR.
