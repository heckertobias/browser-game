# 018 – Plan

Gehört zu: [spec.md](spec.md)

> **Veraltet.** Die offenen Fragen sind beantwortet (feste Karte, Gebiet 10 × 8 Bildschirme, See, Kollision in 021).
> Dieser Plan beruht noch auf der alten Empfehlung „Regeln statt Karte“ und muss vor `bereit` neu geschrieben werden.
>
> Ursprünglich: Der Plan ging von den Empfehlungen unten aus (Variante A) und
> markiert abhängige Stellen mit **⚠ F-n**. Ändert eine Antwort die Variante, wird der Plan vor `bereit` angepasst.

## Überblick

```
Client  ── client/src/assets/tiles/           ground.png + ground.json (eigener Atlas, aus Ninja Adventure zusammengestellt) + Zeile in LICENSES.md
        ── features/game/sprites/atlas.ts      reine Logik: Atlas-Beschreibung prüfen, Eintrag nach Namen (auch für 019)
        ── features/game/world/hash.ts         deterministischer Ganzzahl-Hash (auch für 019)
        ── features/game/world/ground.ts       reine Regeln: Bodenart je Kachel, Übergänge, Name des Bodenstücks
        ── render/render.ts                    sichtbare Kacheln statt Gitter; Gitter bleibt als Rückfall
        ── engine/game.ts, app/App.tsx         Atlas laden und übergeben
Server  ── keine Änderung (⚠ F-1, F-4)
```

Der Boden ist reine Darstellung: Er beeinflusst weder Bewegung noch Spielstand (keine Kollision, ⚠ F-4). Alle Clients
derselben Version rechnen aus denselben Regeln denselben Boden. Deshalb braucht es keinen Vertrag und keinen Server.

**Voraussetzung:** 017 (ADR 0016 und 0017, `client/src/assets/` mit `LICENSES.md` und Test, `sprites/browser.ts`,
`RenderOptions`, `import-sprites.mjs`, `pngjs`).

## Atlas (ADR 0017, zweite Form)

Der Code kennt keine Koordinaten des Packs, nur Namen. Ein Atlas ist ein Bild plus Beschreibung:

```json
{
  "image": "ground.png",
  "entries": {
    "grass": { "x": 0, "y": 0, "width": 16, "height": 16 },
    "grass.1": { "x": 16, "y": 0, "width": 16, "height": 16 },
    "path.center": { "x": 0, "y": 16, "width": 16, "height": 16 },
    "path.n": { "x": 16, "y": 16, "width": 16, "height": 16 }
  }
}
```

- Ein Eintrag ist ein Rechteck im Bild. Optional `origin` (`x`, `y` im Rechteck): der Punkt, der auf der Weltposition
  liegt; fehlt er, gilt die linke obere Ecke. Der Boden braucht ihn nicht, Deko (019) schon.
- Namen mit Punkt bilden Gruppen: `grass.1`, `grass.2` … sind die Varianten von `grass`; `path.<form>` die Wegformen.
  Eine weitere Gras-Variante ist ein neuer Eintrag, keine Codeänderung.
- Das Bild enthält nur genutzte Stücke. Es wird mit dem Import-Skript aus dem Pack zusammengestellt und darf danach
  von Hand erweitert werden.

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
| Einträge `grass`, `grass.<n>`, `path.<form>` | 16×16 je Eintrag | `assets/tiles/ground.json`: Lage der Bodenstücke im Atlas (Entscheidung 3, 4) |

## Entschiedene Fragen (Plan)

1. **Kachelkoordinaten:** Kachel `(tx, ty)` deckt `[tx·16, tx·16 + 16) × [ty·16, ty·16 + 16)` ab;
   `tx = floor(x / TILE_SIZE)`.
2. **Bodenart** `groundAt(tx, ty)`: `"path"`, wenn `−PLAZA ≤ tx, ty < PLAZA` (Platz) oder
   `−PATH_HALF ≤ ty < PATH_HALF` (Weg Ost–West) oder `−PATH_HALF ≤ tx < PATH_HALF` (Weg Nord–Süd); sonst `"grass"`.
   (⚠ F-3, F-5)
3. **Übergänge Weg → Gras:** Eine Wegkachel wählt ihre Form aus den acht Nachbarn: Mitte, vier Kanten, vier
   Außenecken, vier Innenecken (13 Formen). `pathShape(tx, ty)` ist rein; das Bodenstück heißt `path.<form>`
   (Atlas).
4. **Gras-Varianten:** `hash2(tx, ty) mod 1000 < GRASS_VARIANT_PERMILLE` → Variante
   `variants[hash2(ty, tx) mod n]`, sonst `grass`. `variants` sind die Atlas-Einträge `grass.<n>`, nach Namen
   sortiert; `n` kommt also aus dem Atlas. `hash2` ist ein fester 32-Bit-Mix (`Math.imul`), ohne Zufall und ohne
   Zustand.
5. **Zeichnen:** Für jede sichtbare Kachel ein `drawImage` aus dem Atlas an `(tx·16, ty·16)` in Größe 16. Sichtbar
   ist `floor(origin / 16)` bis `floor((origin + view) / 16)` in beiden Achsen. Das sind höchstens etwa
   60 × 27 ≈ 1600 Aufrufe je Bild (größter Ausschnitt 933×400, ADR 0010).
6. **Lückenlos** (AK-2): Ziel- und Quellkoordinaten sind ganzzahlig, die Kamera ist auf Gerätepixel gerundet und `k`
   ist ganzzahlig (ADR 0010). Damit endet jede Kachel genau an einer Gerätepixel-Grenze, die nächste beginnt dort.
   `imageSmoothingEnabled = false` verhindert, dass Nachbarstücke im Atlas durchscheinen.
7. **Hintergrund bleibt zuerst:** Die bisherige Füllung mit `BACKGROUND` wird weiter als erstes gezeichnet (deckt
   gebrochene Ränder ab; Tests aus 016 nutzen sie als Hintergrund).
8. **Rückfall:** Bis der Atlas geladen ist oder wenn er scheitert, zeichnet `render()` das Gitter wie bisher. So
   bleibt 001 AK-7 (Muster zeigt Bewegung) immer erfüllt.
9. **Ort der Regeln:** `features/game/world/` (neuer Unterordner von `game`). Die Regeln sind Weltinhalt, nicht
   Zeichnen; 019 nutzt sie mit.

## Verträge

Keine Änderung (⚠ F-1, F-4: bei fester Karte oder Kollision kommen Kartendaten bzw. Grenzen vom Server).

## Server

Keine Änderung (⚠ F-1, F-2, F-4).

## Client

### Assets
- `client/src/assets/tiles/ground.png` und `ground.json`: Atlas mit Gras, Gras-Varianten und den 13 Wegformen, aus
  dem Boden-Tileset von Ninja Adventure (z. B. `TilesetFloor.png`) zusammengestellt.
  Zeile in `LICENSES.md` (Herkunft, Pfad im Pack, Stand, `CC0 1.0`, „Ausschnitte, neu angeordnet“).
- `client/scripts/import-sprites.mjs` (aus 017) bekommt den Modus `atlas`: Eingabe ist ein Pack-Bild und eine Liste
  `Name = Rechteck im Pack`; Ausgabe sind Atlas-Bild und `entries`. Die Liste steht als Datei neben dem Skript
  (`atlas-ground.json`), damit die Herkunft jedes Stücks nachvollziehbar bleibt.

### `sprites/atlas.ts` (neu, rein)
- `interface AtlasEntry { x; y; width; height; origin: { x; y } }`; `interface Atlas { image: string; entries:
  ReadonlyMap<string, AtlasEntry> }`.
- `parseAtlas(data: unknown): Atlas` prüft ganze, nicht negative Rechtecke mit positiver Größe und `origin` im
  Rechteck (Standard 0, 0). Wirft `Error` mit der Fundstelle.
- `group(atlas, name): readonly string[]` – Namen `name.<…>`, sortiert (Varianten, Wegformen).
- `sprites/assets.test.ts` (aus 017) prüft zusätzlich jeden Atlas unter `assets/tiles/`: `parseAtlas` nimmt ihn an,
  jedes Rechteck liegt im Bild, Alpha nur 0 oder 255.

### `world/hash.ts` (neu)
- `hash2(a: number, b: number): number` → vorzeichenlose 32-Bit-Zahl; gleiche Eingaben, gleiches Ergebnis.

### `world/ground.ts` (neu, rein)
- `type Ground = "grass" | "path"`; `groundAt(tx, ty): Ground` (Entscheidung 2).
- `type PathShape = "center" | "n" | "e" | "s" | "w" | "ne" | "nw" | "se" | "sw" | "inner-ne" | "inner-nw" |
  "inner-se" | "inner-sw"`; `pathShape(tx, ty): PathShape` (Entscheidung 3).
- `groundTile(tx, ty, variants: readonly string[]): string` – Name des Bodenstücks (Entscheidung 3, 4).
- `visibleTiles(origin: Vec, viewWidth: number, viewHeight: number): { tx0, ty0, tx1, ty1 }` (Entscheidung 5).
- `GROUND_ENTRIES`: alle Namen, die die Regeln liefern können (`grass` und `path.<form>` für jede `PathShape`).

### `render/render.ts`
- `RenderOptions.ground?: { atlas: Atlas; image: CanvasImageSource } | null`.
- Nach der Hintergrund-Füllung: mit `ground` alle Kacheln aus `visibleTiles` per `drawImage`
  (`entry.x, entry.y, 16, 16, tx·16, ty·16, 16, 16`), kein Gitter; ohne `ground` Gitter wie bisher. Die Varianten
  (`group(atlas, "grass")`) werden beim Setzen einmal bestimmt, nicht je Bild.

### `engine/game.ts`, `app/App.tsx`
- `Game.setGround(ground: { atlas: Atlas; image: CanvasImageSource } | null)`; an `render` als `ground`.
- `App`: `parseAtlas(GROUND_ATLAS)` und `loadImage(GROUND_IMAGE_URL)`; scheitert eines oder fehlt ein Name aus
  `GROUND_ENTRIES` im Atlas → `null`, also Gitter.

## Abdeckung der Akzeptanzkriterien

| AK | Wie erfüllt | Wie getestet |
|---|---|---|
| 1 | Kacheln statt Gitter | Vitest `ground.test.ts`: Platz um (0, 0), Wege auf den Achsen, Gras daneben, gleiche Eingabe → gleiche Kachel, Variantenanteil bei 10 000 Kacheln 15 % ± 3 %, `pathShape` für Kante, Außen- und Innenecke; jeder Name aus `GROUND_ENTRIES` steht im echten `ground.json`; Vitest `atlas.test.ts`: `parseAtlas` und `group`; Vitest `render-ground.test.ts`: mit Atlas `image`-Ops aus den Rechtecken der Einträge, keine Gitterlinien; ohne Atlas Gitter; **manuell** |
| 2 | ganzzahlig, lückenlos, ohne Glättung | Vitest: für Sichtfelder aus ADR 0010 (1280×720 @1, 1536×730 @1,25, 412×915 @2,625, 3440×1440 @1) liegen alle Kacheln auf Vielfachen von 16, sind 16 groß, decken das Sichtfeld ohne Lücke ab (benachbarte Kacheln stoßen aneinander), Quellpunkte ganzzahlig; **manuell:** PC und Handy, kein Saum beim Laufen |

## Constitution-Check

- [x] **Server ist Autorität:** Der Boden beeinflusst den Spielstand nicht (⚠ F-4).
- [x] **Typen nur aus `contracts/`:** keine neuen Typen über das Netz.
- [x] **Offline-Fortschritt:** nicht betroffen.
- [x] **Keine unnötigen neuen Abhängigkeiten:** kein Paket (`pngjs` kommt mit 017); Bodenstücke aus dem Pack aus 017
  (CC0, älter als 7 Tage).
- [x] **Spielwerte:** keine; der Boden ist Darstellung, solange er nichts blockiert.
- [x] **KISS:** Regeln statt Karte, ein Atlas, direktes Zeichnen ohne Cache. Der Atlas mit Namen folgt ADR 0017.

## Risiken / Abweichungen

- **Leistung:** Bis zu ~1600 `drawImage` je Bild. Ruckelt es auf dem Testhandy (zusammen mit 017 AK-15), kommen
  vorgerenderte Blöcke (z. B. 16×16 Kacheln je Offscreen-Canvas) dazu. Das ändert nur `render.ts`.
- **Tileset-Logik:** Die 13 Formen setzen voraus, dass das Pack Übergänge auf der Wegkachel zeichnet. Arbeitet es
  anders (Übergang auf der Graskachel), wird `pathShape` auf Graskacheln angewandt; Namen und Tests passen sich mit an.
- **Platz und `SpawnRadius`:** Der Platz deckt heute den Startbereich (64 Weltpixel) ab. Ändert sich der Server-Wert,
  folgt der Platz nicht automatisch.
- **Varianten:** Regeländerungen ändern den Boden für alle nach dem Update. Das ist gewollt, solange es keine Karte
  gibt.
- **Bei Antwort „feste Karte“ (F-1) oder „Wasser blockiert“ (F-4):** Kartendaten und Kollision gehören auf den Server
  (Domain `World`), der Client bekommt sie im `Welcome` oder per HTTP; das ist ein größerer Plan mit Vertrag.
