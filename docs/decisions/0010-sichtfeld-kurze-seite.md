# ADR 0010 – Sichtfeld: kurze Seite fest, lange Seite füllt das Format

**Status:** akzeptiert · ersetzt [ADR 0003](0003-logische-aufloesung.md)

## Kontext

ADR 0003 legte eine feste logische Auflösung von 640×360 fest: Alle sehen genau denselben Ausschnitt, der Rest
bekommt Ränder. Das ist fair, kostet aber Fläche: Am Handy hochkant bleibt nur ein schmaler Streifen (640×360 Weltpixel
auf 390 CSS-Pixel Breite), Ultrawide-Monitore zeigen breite schwarze Balken.

Mit [007](../../specs/007-mobil/spec.md) gilt: Das Spielbild füllt immer das Fenster (AK-1), die **kürzere** Seite
zeigt überall ungefähr gleich viel Welt (AK-2), alle Pixel bleiben gleich groß und scharf (AK-3), und ab etwa 21:9 wird
begrenzt (AK-4). Pixel-Art ist nur bei **ganzzahliger** Skalierung scharf; der Weltausschnitt kann deshalb nicht exakt
gleich sein, sondern nur nah dran.

## Entscheidung

- Gerechnet wird in **Gerätepixeln** des Fensters (CSS-Pixel × `devicePixelRatio`). Ein Weltpixel ist genau `k`
  Gerätepixel, `k` ganzzahlig ≥ 1.
- **Kurze Seite:** `k` macht die kurze Seite möglichst nah an **360 Weltpixel** (relativ gemessen: Wechsel von `k` zu
  `k+1` beim geometrischen Mittel `360·√(k(k+1))`), aber **nie mehr als 400**: Läge sie darüber, wird `k` um eins
  erhöht. Unter 360 Gerätepixeln ist `k = 1`; fraktional verkleinert wird nie.
- **Lange Seite:** zeigt `Gerätepixel / k` Weltpixel, höchstens **21:9** der kurzen Seite. Darüber hinaus bleibt das
  Fenster dunkel, das Spielbild sitzt mittig.
- Das Canvas ist so groß wie das Fenster in Gerätepixeln (exakt über `devicePixelContentBoxSize`, sonst gerundet); der
  sichtbare Ausschnitt darf gebrochene Weltpixel enthalten. Die Kamera folgt der eigenen Figur, ihr Ursprung wird auf
  Gerätepixel gerundet.
- Die Werte stehen im Client an genau einer Stelle: `TARGET_SHORT_SIDE = 360`, `MAX_SHORT_SIDE = 400`,
  Höchstformat `21:9`.

### Beispiele

| Fenster (CSS) @ DPR | Gerätepixel | k | sichtbare Welt | kurze Seite |
|---|---|---|---|---|
| 1280×720 @1 | 1280×720 | 2 | 640×360 | ±0 % |
| 1920×1080 @1 | 1920×1080 | 3 | 640×360 | ±0 % |
| 2560×1440 @1 | 2560×1440 | 4 | 640×360 | ±0 % |
| 3840×2160 @1 (4K) | 3840×2160 | 6 | 640×360 | ±0 % |
| 1920×960 @1 (1080p im Browserfenster) | 1920×960 | 3 | 640×320 | −11 % |
| 1536×730 @1,25 (Windows-Laptop 125 %, Fenster) | 1920×913 | 3 | 640×304 | −15 % |
| 1920×1200 @1 | 1920×1200 | 3 | 640×400 | +11 % |
| MacBook 1512×982 @2 | 3024×1964 | 5 | 605×393 | +9 % |
| MacBook, Safari-Fenster 1512×860 @2 | 3024×1720 | 5 | 605×344 | −4 % |
| iPhone hochkant 390×844 @3 | 1170×2532 | 3 | 390×844 | +8 % |
| iPhone quer in Safari 844×340 @3 | 2532×1020 | 3 | 793×340, Balken je 76 Gerätepixel (über 21:9) | −6 % |
| iPhone Pro Max hochkant 430×932 @3 | 1290×2796 | 4 | 323×699 (k = 3 gäbe 430 = +19 %) | −10 % |
| Android 412×915 @2,625 | 1082×2402 gerundet · 1080×2400 exakt | 3 | 361×801 · 360×800 | ±0 % |
| Ultrawide 3440×1440 @1 | 3440×1440 | 4 | 840×360, Balken je 40 | ±0 % |
| 32:9 5120×1440 @1 | 5120×1440 | 4 | 840×360, Balken je 880 | ±0 % |
| 32:9 3840×1080 @1 | 3840×1080 | 3 | 840×360, Balken je 660 | ±0 % |
| kleines Fenster 600×450 @1 | 600×450 | 2 | 300×225 | −38 % |
| winziges Fenster 480×270 @1 | 480×270 | 1 | 480×270 | −25 % |

**Garantie:** Nach oben nie mehr als +11 % (400 Weltpixel). Nach unten hängt es an der Auflösung, weil zwischen `k`
und `k+1` ein Sprung liegt: kurze Seite ab 1601 Gerätepixeln höchstens −11 %, 1201–1600 bis −17 %, 801–1200 bis −26 %,
darunter mehr. Wer weniger sieht, bekommt größere Pixel; einen Vorteil hat niemand.

## Begründung: Fairness und Balancing

- **Kein Vorteil durch Hardware oder Zoom:** Ein größerer oder schärferer Bildschirm erhöht nur `k`. Browser-Zoom
  ändert die Gerätepixel des Fensters nicht und damit auch den Ausschnitt nicht. Ein kleineres Fenster zeigt nie mehr
  als 400 auf der kurzen Seite.
- **Begrenzter Formatvorteil:** Der größte mögliche Ausschnitt ist 933×400 Weltpixel (21:9 bei +11 %), quer oder
  hochkant. Ein 16:9-Bildschirm zeigt typischerweise 640×360, ein Handy hochkant 360–390 × 700–850.
- **Balancing richtet sich nach dem, was alle sehen:** Spielrelevante Reichweiten (Tab-Targeting, Aggro,
  Fernkampf) sollen innerhalb der Hälfte der kleinsten üblichen kurzen Seite liegen, also etwa 150 Weltpixel um die
  Figur, oder Ziele außerhalb des Bildes anzeigen. Die lange Seite ist Überblick, kein Reichweitenvorteil.
- **Server bleibt unabhängig vom Bildschirm:** Er übernimmt keine Bildschirmgröße vom Client (Constitution §2). Heute
  bekommt jeder alle Spieler. Kommt Interest-Management, muss es mindestens den größten Ausschnitt in beiden
  Ausrichtungen abdecken: Radius ≥ √(467² + 200²) ≈ 510 Weltpixel plus Rand für Figurgröße und Interpolation. Damit
  begrenzt es zugleich, was ein manipulierter Client zeigen kann.

## Alternativen

- **Feste logische Auflösung mit Rändern (ADR 0003):** am fairsten, aber Hochformat am Handy winzig und Ränder auf
  allen Formaten außer 16:9. Widerspricht AK-1.
- **Feste Pixelgröße** (z. B. 1 Weltpixel = 2 CSS-Pixel): scharf und einfach, aber größere Fenster sehen viel mehr
  Welt. Unfair.
- **Zoom-Stufen nach Wahl:** jeder wählt seinen Vorteil; braucht dieselbe Obergrenze plus Bedienung. Die Spec verlangt
  es nicht.
- **Fraktionale Skalierung** auf exakt 360: gleicher Ausschnitt, aber ungleich breite oder verwaschene Pixel.
  Widerspricht AK-3.
- **Toleranzband mit Rändern auf der kurzen Seite**, wenn kein `k` in ±10 % fällt (z. B. 1080p-Fenster → 441 statt
  320, gekappt auf 400 mit Balken oben und unten): hält AK-2 enger ein, widerspricht aber AK-1.
- **Kleinster Faktor mit kurzer Seite ≤ 400** (ohne „nah an 360“): noch einfacher, aber hochauflösende Bildschirme
  landen systematisch am oberen Rand (370–400).
- **Gleiche Fläche statt gleicher kurzer Seite:** Hochformat würde schmal und sehr hoch. In der Spec so nicht gewollt.

## Konsequenzen

- `LOGICAL_WIDTH/HEIGHT` entfallen; Kamera, Rendering, Culling und Umrechnung Bildschirm → Welt arbeiten mit einem
  variablen Ausschnitt. Das HUD bleibt in CSS-Pixeln und skaliert nicht mit `k`.
- Sprites bleiben auf ein 16er-Raster ausgelegt; 360 entspricht 22,5 Kacheln. Hochauflösende Texturen sind ab `k ≥ 2`
  möglich, ohne den Ausschnitt zu ändern.
- Der Ausschnitt schwankt je Gerät (siehe Tabelle); bei niedriger Auflösung sieht man spürbar weniger.
- Eine Änderung von 360, 400 oder 21:9 ändert, wie viel Welt man sieht. Das betrifft Balancing und braucht ein neues ADR.
- Ein späteres Interest-Management muss den größten Ausschnitt abdecken (siehe oben).
