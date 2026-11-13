# 018 – Plan

Gehört zu: [spec.md](spec.md)

> **Vorläufig.** Die Spec hat noch offene Fragen. Der Plan geht von den Empfehlungen unten aus (Variante A) und
> markiert abhängige Stellen mit **⚠ F-n**. Ändert eine Antwort die Variante, wird der Plan vor `bereit` angepasst.

## Überblick

```
Client  ── client/src/assets/                 ground.png (Boden-Tileset aus Ninja Adventure, unverändert) + Zeile in LICENSES.md
        ── features/game/world/hash.ts         deterministischer Ganzzahl-Hash (auch für 019)
        ── features/game/world/ground.ts       reine Regeln: Bodenart je Kachel, Übergänge, Kachel im Tileset
        ── render/render.ts                    sichtbare Kacheln statt Gitter; Gitter bleibt als Rückfall
        ── engine/game.ts, app/App.tsx         Tileset laden und übergeben
Server  ── keine Änderung (⚠ F-1, F-4)
```

Der Boden ist reine Darstellung: Er beeinflusst weder Bewegung noch Spielstand (keine Kollision, ⚠ F-4). Alle Clients
derselben Version rechnen aus denselben Regeln denselben Boden. Deshalb braucht es keinen Vertrag und keinen Server.

**Voraussetzung:** 017 (ADR 0016, `client/src/assets/` mit `LICENSES.md` und Test, `render/images.ts`,
`RenderOptions`).

## Offene Fragen der Spec und Empfehlung

| | Frage (spec.md) | Empfehlung | Begründung |
|---|---|---|---|
| F-1 | Feste Karte oder Regeln? | **Regeln**, im Client, deterministisch | Keine Daten, kein Editor, kein Vertrag; passt zur grenzenlosen Welt aus 001 |
| F-2 | Wie groß ist die Welt, was ist am Rand? | **Grenzenlos wie bisher**, kein Rand | 001 AK-7 („stoße auf keine Grenze“) bleibt gültig; eine Grenze bräuchte Server-Logik |
| F-3 | Welche Bodenarten? | **Gras (mit Varianten) und ein Weg** (Erde) | Gras allein hilft nicht bei der Orientierung (User Story 2); ein Weg genügt |
| F-4 | Unbegehbare Flächen (Wasser)? | **Nein**; Kollision als eigene Spec | Kollision braucht Server-Logik und Vertrag (Constitution §2) |
| F-5 | Startpunkt markieren? | **Ja, durch einen Platz** am Kreuz der Wege | Ohne eigenes Objekt; der Platz ist Boden |

## Konstanten

Reine Darstellung, Client (`core/constants.ts`). Keine Spielwerte im Sinn von §8, solange der Boden nichts blockiert
(⚠ F-4).

| Name | Wert | Bedeutung |
|---|---|---|
| `TILE_SIZE` | 16 | Kantenlänge einer Bodenkachel in Weltpixeln (Raster des Packs) |
| `PATH_HALF_WIDTH_TILES` | 1 | Wege sind 2 Kacheln breit: Kachelzeilen bzw. -spalten −1 und 0 (⚠ F-3) |
| `PLAZA_RADIUS_TILES` | 5 | Platz um den Startpunkt: Kacheln −5 … 4 in x und y, also ±80 Weltpixel (⚠ F-5) |
| `GRASS_VARIANT_PERMILLE` | 150 | Anteil der Graskacheln mit einer Variante (Blumen, Büschel) in Promille |
| `GROUND_TILES` | aus dem Tileset | Quellzellen: `grass`, `grassVariants[]`, `path` je Form (Entscheidung 3) |

## Entschiedene Fragen (Plan)

1. **Kachelkoordinaten:** Kachel `(tx, ty)` deckt `[tx·16, tx·16 + 16) × [ty·16, ty·16 + 16)` ab;
   `tx = floor(x / TILE_SIZE)`.
2. **Bodenart** `groundAt(tx, ty)`: `"path"`, wenn `−PLAZA ≤ tx, ty < PLAZA` (Platz) oder
   `−PATH_HALF ≤ ty < PATH_HALF` (Weg Ost–West) oder `−PATH_HALF ≤ tx < PATH_HALF` (Weg Nord–Süd); sonst `"grass"`.
   (⚠ F-3, F-5)
3. **Übergänge Weg → Gras:** Eine Wegkachel wählt ihre Form aus den acht Nachbarn: Mitte, vier Kanten, vier
   Außenecken, vier Innenecken (13 Formen). `pathShape(tx, ty)` ist rein; die Zuordnung Form → Zelle im Tileset steht
   in `GROUND_TILES.path` und wird am Tileset bestimmt.
4. **Gras-Varianten:** `hash2(tx, ty) mod 1000 < GRASS_VARIANT_PERMILLE` → Variante
   `grassVariants[hash2(ty, tx) mod n]`, sonst `grass`. `hash2` ist ein fester 32-Bit-Mix (`Math.imul`), ohne Zufall
   und ohne Zustand.
5. **Zeichnen:** Für jede sichtbare Kachel ein `drawImage` aus dem Tileset an `(tx·16, ty·16)` in Größe 16. Sichtbar
   ist `floor(origin / 16)` bis `floor((origin + view) / 16)` in beiden Achsen. Das sind höchstens etwa
   60 × 27 ≈ 1600 Aufrufe je Bild (größter Ausschnitt 933×400, ADR 0010).
6. **Lückenlos** (AK-2): Ziel- und Quellkoordinaten sind ganzzahlig, die Kamera ist auf Gerätepixel gerundet und `k`
   ist ganzzahlig (ADR 0010). Damit endet jede Kachel genau an einer Gerätepixel-Grenze, die nächste beginnt dort.
   `imageSmoothingEnabled = false` verhindert, dass Nachbarzellen des Tilesets durchscheinen.
7. **Hintergrund bleibt zuerst:** Die bisherige Füllung mit `BACKGROUND` wird weiter als erstes gezeichnet (deckt
   gebrochene Ränder ab; Tests aus 016 nutzen sie als Hintergrund).
8. **Rückfall:** Bis das Tileset geladen ist oder wenn es scheitert, zeichnet `render()` das Gitter wie bisher. So
   bleibt 001 AK-7 (Muster zeigt Bewegung) immer erfüllt.
9. **Ort der Regeln:** `features/game/world/` (neuer Unterordner von `game`). Die Regeln sind Weltinhalt, nicht
   Zeichnen; 019 nutzt sie mit.

## Verträge

Keine Änderung (⚠ F-1, F-4: bei fester Karte oder Kollision kommen Kartendaten bzw. Grenzen vom Server).

## Server

Keine Änderung (⚠ F-1, F-2, F-4).

## Client

### Assets
- `client/src/assets/ground.png`: Boden-Tileset aus Ninja Adventure (z. B. `TilesetFloor.png`), unverändert.
  Zeile in `LICENSES.md` (Herkunft, Pfad im Pack, Stand, `CC0 1.0`, „keine“).

### `world/hash.ts` (neu)
- `hash2(a: number, b: number): number` → vorzeichenlose 32-Bit-Zahl; gleiche Eingaben, gleiches Ergebnis.

### `world/ground.ts` (neu, rein)
- `type Ground = "grass" | "path"`; `groundAt(tx, ty): Ground` (Entscheidung 2).
- `type PathShape = "center" | "n" | "e" | "s" | "w" | "ne" | "nw" | "se" | "sw" | "inner-ne" | "inner-nw" |
  "inner-se" | "inner-sw"`; `pathShape(tx, ty): PathShape` (Entscheidung 3).
- `groundCell(tx, ty): { sx: number; sy: number }` – Quellpunkt im Tileset (Entscheidung 3, 4).
- `visibleTiles(origin: Vec, viewWidth: number, viewHeight: number): { tx0, ty0, tx1, ty1 }` (Entscheidung 5).
- Konstanten `GROUND_TILES` hier (an das Tileset gebunden), die übrigen in `core/constants.ts`.

### `render/render.ts`
- `RenderOptions.ground?: CanvasImageSource | null`.
- Nach der Hintergrund-Füllung: mit `ground` alle Kacheln aus `visibleTiles` per `drawImage`
  (`sx, sy, 16, 16, tx·16, ty·16, 16, 16`), kein Gitter; ohne `ground` Gitter wie bisher.

### `engine/game.ts`, `app/App.tsx`
- `Game.setGroundTiles(image: CanvasImageSource | null)`; an `render` als `ground`.
- `App`: `loadImage(GROUND_TILES_URL).then(game.setGroundTiles, () => {})` – Fehler → Gitter.

## Abdeckung der Akzeptanzkriterien

| AK | Wie erfüllt | Wie getestet |
|---|---|---|
| 1 | Kacheln statt Gitter | Vitest `ground.test.ts`: Platz um (0, 0), Wege auf den Achsen, Gras daneben, gleiche Eingabe → gleiche Kachel, Variantenanteil bei 10 000 Kacheln 15 % ± 3 %, `pathShape` für Kante, Außen- und Innenecke; Vitest `render-ground.test.ts`: mit Tileset `image`-Ops, keine Gitterlinien; ohne Tileset Gitter; **manuell** |
| 2 | ganzzahlig, lückenlos, ohne Glättung | Vitest: für Sichtfelder aus ADR 0010 (1280×720 @1, 1536×730 @1,25, 412×915 @2,625, 3440×1440 @1) liegen alle Kacheln auf Vielfachen von 16, sind 16 groß, decken das Sichtfeld ohne Lücke ab (benachbarte Kacheln stoßen aneinander), Quellpunkte ganzzahlig; **manuell:** PC und Handy, kein Saum beim Laufen |

## Constitution-Check

- [x] **Server ist Autorität:** Der Boden beeinflusst den Spielstand nicht (⚠ F-4).
- [x] **Typen nur aus `contracts/`:** keine neuen Typen über das Netz.
- [x] **Offline-Fortschritt:** nicht betroffen.
- [x] **Keine unnötigen neuen Abhängigkeiten:** kein Paket; ein Tileset aus dem Pack aus 017 (CC0, älter als 7 Tage).
- [x] **Spielwerte:** keine; der Boden ist Darstellung, solange er nichts blockiert.
- [x] **KISS:** Regeln statt Karte, ein Tileset, direktes Zeichnen ohne Cache.

## Risiken / Abweichungen

- **Leistung:** Bis zu ~1600 `drawImage` je Bild. Ruckelt es auf dem Testhandy (zusammen mit 017 AK-15), kommen
  vorgerenderte Blöcke (z. B. 16×16 Kacheln je Offscreen-Canvas) dazu. Das ändert nur `render.ts`.
- **Tileset-Logik:** Die 13 Formen setzen voraus, dass das Tileset Übergänge auf der Wegkachel zeichnet. Arbeitet es
  anders (Übergang auf der Graskachel), wird `pathShape` auf Graskacheln angewandt; Tests passen sich mit an.
- **Platz und `SpawnRadius`:** Der Platz deckt heute den Startbereich (64 Weltpixel) ab. Ändert sich der Server-Wert,
  folgt der Platz nicht automatisch.
- **Varianten:** Regeländerungen ändern den Boden für alle nach dem Update. Das ist gewollt, solange es keine Karte
  gibt.
- **Bei Antwort „feste Karte“ (F-1) oder „Wasser blockiert“ (F-4):** Kartendaten und Kollision gehören auf den Server
  (Domain `World`), der Client bekommt sie im `Welcome` oder per HTTP; das ist ein größerer Plan mit Vertrag.
