# 018 – Plan

Gehört zu: [spec.md](spec.md)

## Überblick

```
Inhalt  ── server/BrowserGame.Server/Features/Game/Map/Maps/meadow.json   Karte des ersten Gebiets (bearbeitbar, AK-6)
        ── scripts/generate-map.py                                    erzeugt meadow.json einmal nach Schema (AK-4)
Vertrag ── game.proto: Welcome.area (AreaMap, Feld 10), enum GroundKind
Server  ── Features/Game/Map/Domain/        AreaMap (Kacheln, Bodenart je Kachel, Rand fortgesetzt) · MapFile (lesen, prüfen)
        ── Features/Game/Map/Infrastructure/ MapLoader (Datei beim Start laden; ungültig → Start scheitert)
        ── Application/GameLoop.cs           Karte im Welcome mitschicken
Client  ── client/src/assets/tiles/          ground.png + ground.json (Atlas, aus Ninja Adventure zusammengestellt) + LICENSES.md
        ── features/game/sprites/atlas.ts     reine Logik: Atlas-Beschreibung prüfen, Eintrag nach Namen (auch für 019)
        ── features/game/world/areaMap.ts     Karte aus dem Welcome, Bodenart je Kachel (Rand fortgesetzt)
        ── features/game/world/hash.ts        deterministischer Ganzzahl-Hash (Gras-Varianten, 019)
        ── features/game/world/groundTiles.ts reine Regeln: Bodenstück je Kachel (Übergänge, Varianten), sichtbare Kacheln
        ── render/render.ts                   Kacheln statt Gitter; Startmarkierung; Gitter bleibt als Rückfall
        ── engine/game.ts, app/App.tsx        Karte übernehmen, Atlas laden und übergeben
```

Die Karte gehört dem **Server**. Er lädt sie beim Start aus einer Datei und schickt sie jedem Client im `Welcome`.
Damit sehen alle denselben Boden (AK-3), die Datei ist ohne Codeänderung bearbeitbar (AK-6), und 021 (Kollision)
kann auf genau dieselbe Karte bauen, ohne dass Server und Client zwei Kopien pflegen. In 018 beeinflusst die Karte
weder Bewegung noch Spielstand; sie wird nur gezeichnet.

**Voraussetzung:** 017 (ADR 0016 und 0017, `client/src/assets/` mit `LICENSES.md` und Test, `sprites/browser.ts`,
`RenderOptions`, `import-sprites.mjs`, `pngjs`).

## Kartenformat

Eine Karte ist eine JSON-Datei. Sie beschreibt **Bodenarten**, nicht Bodenstücke: Welche Grafik an einer Kachel liegt
(Übergang Weg → Gras, Gras-Variante), rechnet der Client aus den Nachbarn. So bleibt die Datei klein, von Hand
lesbar und für einen späteren Editor einfach: Er setzt nur Arten.

```json
{
  "format": 1,
  "name": "meadow",
  "tileSize": 16,
  "origin": { "x": 200, "y": 90 },
  "legend": { ".": "grass", "=": "path", "~": "water", "^": "mountain" },
  "rows": [
    "^^^^^^^^^^…",
    "^^^^......…"
  ]
}
```

- `rows`: eine Zeichenkette je Kachelzeile, von oben nach unten; jedes Zeichen ist eine Kachel. Alle Zeilen sind gleich
  lang. Breite = Länge einer Zeile, Höhe = Anzahl Zeilen.
- `legend`: Zeichen → Bodenart. Erlaubte Arten in 018: `grass`, `path`, `water`, `mountain`.
- `origin`: die Kartenkachel, die die Weltkachel (0, 0) ist. Der Startpunkt (Welt (0, 0), 003) liegt damit in der Karte
  an `origin`. Weltkachel `(tx, ty)` = Kartenkachel `(tx + origin.x, ty + origin.y)`.
- `tileSize`: Kantenlänge einer Kachel in Weltpixeln (16, Raster des Packs).
- 019 ergänzt `objects`; ein unbekanntes Feld ist ein Fehler, damit Tippfehler auffallen.

## Konstanten

| Name | Wert | Wo | Bedeutung |
|---|---|---|---|
| Kartengröße | 400 × 180 Kacheln | `meadow.json` | 10 × 8 Bildschirme zu 640 × 360 Weltpixeln (ADR 0010) = 6400 × 2880 Weltpixel (AK-4) |
| `origin` | 200, 90 | `meadow.json` | Startpunkt in der Mitte der Karte; Welt x ∈ [−3200, 3200), y ∈ [−1440, 1440) |
| `tileSize` | 16 | `meadow.json` | Kachelgröße in Weltpixeln |
| `MOUNTAIN_BAND` | 4 Kacheln | `scripts/generate-map.py` | Breite des Bergrands (Spec: legt der Plan fest) |
| `PATH_WIDTH` | 2 Kacheln | `scripts/generate-map.py` | Breite der Wege |
| `LAKE` | Mitte (300, 45), Halbachsen 36 × 18 Kacheln | `scripts/generate-map.py` | Lage und Größe des Sees (Kartenkoordinaten) |
| `GRASS_VARIANT_PERMILLE` | 150 | `world/groundTiles.ts` | Anteil der Graskacheln mit einer Variante (Blumen, Büschel) in Promille |
| `START_MARKER_COLOR` | `#ff00ff` | `core/constants.ts` | Farbe der Startmarkierung; bewusst nicht im Stil der Welt (AK-7) |
| `START_MARKER_SIZE` | 12 | `core/constants.ts` | Kantenlänge des Kreuzes der Startmarkierung in Weltpixeln |
| Einträge `grass`, `grass.<n>`, `path.<form>`, `water.<form>`, `mountain` | 16×16 je Eintrag | `assets/tiles/ground.json` | Lage der Bodenstücke im Atlas |

Die Karte enthält keine Spielwerte im Sinn von §8, solange der Boden nichts blockiert. Mit 021 legt der Server fest,
welche Arten blockieren.

## Entschiedene Fragen (Plan)

1. **Server lädt, Client zeichnet.** Die Datei liegt beim Spielserver und wird mit ihm ausgeliefert (im Image). Der
   Server lädt sie einmal beim Start; ist sie ungültig, startet er nicht (Fehler im Log mit Zeile und Grund). So kann
   keine kaputte Karte unbemerkt live gehen.
2. **Im `Welcome`, nicht per HTTP.** Ein Gebiet ist 72 000 Kacheln, also 72 KB als ein Byte je Kachel. Das kommt
   einmal je Verbindung. Ein eigener HTTP-Endpunkt mit Cache wäre sparsamer, braucht aber OpenAPI, Auth und einen
   zweiten Ladeweg. Mit mehreren Gebieten schickt der Server später nur das Gebiet, in dem die Figur ist.
3. **Rand fortgesetzt** (AK-5): `kindAt(tx, ty)` klemmt die Kartenkoordinaten auf `[0, Breite − 1]` und
   `[0, Höhe − 1]`. Jenseits der Karte liegt also die äußerste Kachel, endlos. Die Regel gilt im Client (Zeichnen) und
   im Server (021) gleich.
4. **Übergänge nur zu Gras.** Wege und Wasser wählen ihre Form aus den acht Nachbarn: Mitte, vier Kanten, vier
   Außenecken, vier Innenecken (13 Formen, Name `path.<form>` bzw. `water.<form>`). Ein Nachbar zählt als „gleich“,
   wenn er dieselbe Art hat; alles andere wird als Gras behandelt. Berge sind ein einziges Bodenstück ohne Übergang
   (Spec).
5. **Damit die 13 Formen reichen**, gelten zwei Regeln für jede Karte, geprüft im Server-Test der echten Datei:
   - Weg und Wasser sind **2×2-geschlossen**: Jede Weg- bzw. Wasserkachel liegt in einem 2×2-Block derselben Art (keine
     ein Kachel breiten Zipfel).
   - Zwischen zwei verschiedenen Nicht-Gras-Arten liegt mindestens eine **Graskachel** (auch diagonal), außer zwischen
     Berg und Berg.
6. **Gras-Varianten:** `hash2(tx, ty) mod 1000 < GRASS_VARIANT_PERMILLE` → Variante `variants[hash2(ty, tx) mod n]`,
   sonst `grass`. `variants` sind die Atlas-Einträge `grass.<n>`, nach Namen sortiert; `n` kommt aus dem Atlas.
   `hash2` ist ein fester 32-Bit-Mix (`Math.imul`), ohne Zufall und ohne Zustand. Gerechnet wird in Weltkacheln, damit
   Varianten bei einer Verschiebung von `origin` mitwandern.
7. **Zeichnen:** Für jede sichtbare Kachel ein `drawImage` aus dem Atlas an `(tx·16, ty·16)` in Größe 16. Sichtbar ist
   `floor(origin / 16)` bis `floor((origin + view) / 16)` in beiden Achsen, höchstens etwa 60 × 27 ≈ 1600 Aufrufe je
   Bild (größter Ausschnitt 933×400, ADR 0010).
8. **Lückenlos** (AK-2): Ziel- und Quellkoordinaten sind ganzzahlig, die Kamera ist auf Gerätepixel gerundet und `k`
   ist ganzzahlig (ADR 0010). Jede Kachel endet genau an einer Gerätepixel-Grenze, die nächste beginnt dort.
   `imageSmoothingEnabled = false` verhindert, dass Nachbarstücke im Atlas durchscheinen.
9. **Rückfall:** Bis Karte und Atlas da sind oder wenn eines scheitert, zeichnet `render()` das Gitter wie bisher. So
   bleibt 001 AK-7 (Muster zeigt Bewegung) immer erfüllt.
10. **Startmarkierung** (AK-7): ein Kreuz in `START_MARKER_COLOR` mit `START_MARKER_SIZE` an Welt (0, 0) und darunter
    der Text „Start (Test)“ in derselben Farbe. Gezeichnet über dem Boden, unter den Figuren, auch über dem Gitter.
    Grelles Magenta kommt im Pack nicht vor, damit ist klar: Das gehört nicht zur Welt. Entfernt wird sie mit einer
    späteren Spec (eine Zeile in `render.ts` und die zwei Konstanten).
11. **Erzeugen nach Schema, danach nur noch die Datei.** `scripts/generate-map.py` (Python, nur Standardbibliothek wie
    `check-docs.py`) schreibt `meadow.json` einmal: Bergrand, ein Wegekreuz durch den Startpunkt (Ost–West und
    Nord–Süd, je bis zwei Kacheln vor den Berg), ein See oben rechts und ein Weg vom Kreuz zum Seeufer. Der See wird
    auf einem Raster aus 2×2-Blöcken gerastert (Entscheidung 5). Danach ist die Datei die Wahrheit; wer sie von Hand
    ändert, ruft das Skript nicht mehr auf (Kommentar im Skript).
12. **Ort der Regeln:** Client `features/game/world/` (Weltinhalt, nicht Zeichnen; 019 und 021 nutzen ihn mit), Server
    `Features/Game/Map/` (Unterordner von `Game` wie `Chat`, `Combat`).

## Verträge

**Protobuf** (`contracts/proto/browsergame/v1/game.proto`), abwärtskompatibel. `Welcome` 10 ist frei (1–9 belegt,
9 = `projectile_speed` aus 013). 019 und 021 ergänzen `AreaMap`.

```proto
message Welcome {
  // … 1–9
  // Gebiet, in dem die Figur ist (018). Fehlt es, zeichnet der Client das Gitter.
  AreaMap area = 10;
}

// Bodenart einer Kachel (018).
enum GroundKind {
  GROUND_KIND_UNSPECIFIED = 0;
  GROUND_KIND_GRASS = 1;
  GROUND_KIND_PATH = 2;
  GROUND_KIND_WATER = 3;
  GROUND_KIND_MOUNTAIN = 4;
}

// Feste Karte eines Gebiets (018). Jenseits der Karte setzt sich die äußerste Kachel endlos fort.
message AreaMap {
  // Kantenlänge einer Kachel in Weltpixeln.
  uint32 tile_size = 1;
  uint32 width = 2;
  uint32 height = 3;
  // Weltkachel der Kartenkachel (0, 0); Weltkachel (tx, ty) = Kartenkachel (tx − origin_tx, ty − origin_ty).
  int32 origin_tx = 4;
  int32 origin_ty = 5;
  // Eine GroundKind je Kachel, Zeile für Zeile von oben links; Länge width × height.
  bytes ground = 6;
}
```

In der Datei steht `origin` als Kartenkachel der Weltkachel (0, 0); im Vertrag als Weltkachel der Kartenkachel (0, 0),
also `origin_tx = −origin.x`. So rechnen Client und Server ohne Vorzeichenwechsel.

**OpenAPI:** keine Änderung.

## Server

- `Map/Domain/AreaMap.cs`: `TileSize`, `Width`, `Height`, `OriginTx`, `OriginTy`, `GroundKind[] Ground`;
  `KindAt(int tx, int ty)` klemmt (Entscheidung 3); `TileOf(double x, double y)` → Weltkachel (`floor(x / TileSize)`).
- `Map/Domain/MapFile.cs`: `Parse(string json) → AreaMap`; wirft `MapFormatException` mit Fundstelle bei: falschem
  `format`, fehlendem oder unbekanntem Feld, leerer Karte, ungleich langen Zeilen, unbekanntem Zeichen, unbekannter
  Art in `legend`, `origin` außerhalb der Karte, `tileSize` ≤ 0. Nutzt `System.Text.Json`, kein neues Paket.
- `Map/Infrastructure/MapLoader.cs`: liest `Maps/meadow.json` neben der Anwendung (`AppContext.BaseDirectory`) oder den
  Pfad aus `Game:MapPath` (Tests); als Singleton in `GameSetup` registriert. Ungültig → Ausnahme beim Start.
- `meadow.json` liegt unter `Features/Game/Map/Maps/`; die `.csproj` kopiert sie in Ausgabe und Publish.
- `GameLoop`: baut die `AreaMap`-Nachricht einmal beim Start und setzt sie in jedes `Welcome`.

## Client

### Assets
- `client/src/assets/tiles/ground.png` und `ground.json`: Atlas mit Gras, Gras-Varianten, je 13 Weg- und Wasserformen
  und einem Bergstück, aus den Boden- und Wasser-Tilesets von Ninja Adventure zusammengestellt (Wasser: ein
  Einzelbild der Animation, Spec 019: keine Animationen). Zeile in `LICENSES.md` (Herkunft, Pfad im Pack, Stand,
  `CC0 1.0`, „Ausschnitte, neu angeordnet“).
- `client/scripts/import-sprites.mjs` (aus 017) bekommt den Modus `atlas`: Eingabe ist ein oder mehrere Pack-Bilder und
  eine Liste `Name = Bild + Rechteck im Pack`; Ausgabe sind Atlas-Bild und `entries`. Die Liste steht als
  `atlas-ground.json` neben dem Skript, damit die Herkunft jedes Stücks nachvollziehbar bleibt.

### `sprites/atlas.ts` (neu, rein)
- `interface AtlasEntry { x; y; width; height; origin: { x; y } }`; `interface Atlas { image: string; entries:
  ReadonlyMap<string, AtlasEntry> }`. `origin` (Punkt im Rechteck, der auf der Weltposition liegt) ist optional,
  Standard 0, 0; der Boden braucht ihn nicht, Deko (019) schon.
- `parseAtlas(data: unknown): Atlas` prüft ganze, nicht negative Rechtecke mit positiver Größe und `origin` im
  Rechteck. Wirft `Error` mit der Fundstelle.
- `group(atlas, name): readonly string[]` – Namen `name.<…>`, sortiert.
- `sprites/assets.test.ts` (aus 017) prüft zusätzlich jeden Atlas unter `assets/tiles/`: `parseAtlas` nimmt ihn an,
  jedes Rechteck liegt im Bild, Alpha nur 0 oder 255.

### `world/areaMap.ts` (neu, rein)
- `interface AreaMap { tileSize; width; height; originTx; originTy; ground: Uint8Array }`.
- `areaMapFromProto(area: AreaMapProto | undefined): AreaMap | null` – `null`, wenn Länge von `ground` nicht
  `width × height` ist oder eine Größe 0 ist.
- `kindAt(map, tx, ty): GroundKind` (Entscheidung 3).

### `world/hash.ts` (neu, rein)
- `hash2(a: number, b: number): number` → vorzeichenlose 32-Bit-Zahl.

### `world/groundTiles.ts` (neu, rein)
- `edgeShape(map, tx, ty): Shape` mit `Shape = "center" | "n" | "e" | "s" | "w" | "ne" | "nw" | "se" | "sw" |
  "inner-ne" | "inner-nw" | "inner-se" | "inner-sw"`; `null` für eine Lage, die keine der 13 Formen ist (darf bei
  einer Karte nach Entscheidung 5 nicht vorkommen; dann Mitte).
- `groundTile(map, tx, ty, variants): string` – Name des Bodenstücks (Entscheidungen 4, 6).
- `visibleTiles(origin, viewWidth, viewHeight, tileSize): { tx0, ty0, tx1, ty1 }` (Entscheidung 7).
- `GROUND_ENTRIES`: alle Namen, die die Regeln liefern können.

### `render/render.ts`
- `RenderOptions.ground?: { map: AreaMap; atlas: Atlas; image: CanvasImageSource; variants: readonly string[] } | null`.
- Nach der Hintergrund-Füllung: mit `ground` alle Kacheln aus `visibleTiles` per `drawImage`, kein Gitter; ohne
  `ground` Gitter wie bisher. Danach die Startmarkierung (Entscheidung 10), dann Ziele, Figuren und Namen wie bisher.

### `engine/game.ts`, `app/App.tsx`
- `Game` übernimmt `areaMapFromProto(welcome.area)` und setzt mit `setGroundAtlas({ atlas, image })` (aus `App`) die
  Option `ground`, sobald beides da ist. `variants` (`group(atlas, "grass")`) werden einmal bestimmt, nicht je Bild.
- `App`: `parseAtlas(GROUND_ATLAS)` und `loadImage(GROUND_IMAGE_URL)`; scheitert eines oder fehlt ein Name aus
  `GROUND_ENTRIES` im Atlas, bleibt es beim Gitter.

## Abdeckung der Akzeptanzkriterien

| AK | Wie erfüllt | Wie getestet |
|---|---|---|
| 1 | Kacheln statt Gitter | Vitest `groundTiles.test.ts`: Gras, Weg und Wasser auf einer kleinen Testkarte; `edgeShape` für Mitte, Kante, Außen- und Innenecke; Variantenanteil bei 10 000 Graskacheln 15 % ± 3 %; jeder Name aus `GROUND_ENTRIES` steht im echten `ground.json`; Vitest `atlas.test.ts`: `parseAtlas`, `group`; Vitest `render-ground.test.ts`: mit Karte und Atlas `image`-Ops aus den Rechtecken der Einträge, keine Gitterlinien; ohne Atlas oder ohne Karte Gitter; **manuell** |
| 2 | ganzzahlig, lückenlos, ohne Glättung | Vitest: für Sichtfelder aus ADR 0010 (1280×720 @1, 1536×730 @1,25, 412×915 @2,625, 3440×1440 @1) liegen alle Kacheln auf Vielfachen von 16, sind 16 groß, decken das Sichtfeld ohne Lücke ab, Quellpunkte ganzzahlig; **manuell:** PC und Handy, kein Saum beim Laufen |
| 3 | Karte vom Server, Regeln ohne Zufall | xUnit `MapLoaderTests`: zweimal laden → gleiche Bytes; Integration (WS): zwei Clients bekommen dasselbe `area`; Vitest: `groundTile` für dieselbe Kachel zweimal gleich; **manuell:** zwei Geräte am selben Ort, Server-Neustart |
| 4 | `meadow.json` aus dem Skript | xUnit `MeadowMapTests` (echte Datei): 400 × 180, `origin` 200, 90; alle Kacheln im Rand von `MOUNTAIN_BAND` sind Berg und sonst keine; es gibt Weg und Wasser, Wasser zusammenhängend; Weltkachel (0, 0) ist Weg oder Gras; Regeln aus Entscheidung 5; **manuell** |
| 5 | Klemmen am Rand | xUnit `AreaMapTests` und Vitest `areaMap.test.ts`: `KindAt`/`kindAt` jenseits aller vier Seiten und Ecken = äußerste Kachel; Vitest `render-ground.test.ts`: Sichtfeld halb außerhalb der Karte → lückenlos, außen Bergstücke; **manuell:** an den Rand und darüber hinaus laufen |
| 6 | Datei, beim Start geladen | xUnit `MapFileTests`: gültige Datei, jeder Fehlerfall aus „Server“ mit Fundstelle; Integration: eigene Datei über `Game:MapPath` → `Welcome.area` zeigt deren Kacheln; ungültige Datei → Host startet nicht |
| 7 | Kreuz und Text in Magenta | Vitest `render-ground.test.ts`: Kreuz und Text an (0, 0) in `START_MARKER_COLOR`, nach dem Boden und vor den Figuren, auch mit Gitter; **manuell** |

## Constitution-Check

- [x] **Server ist Autorität:** Der Server besitzt die Karte; der Boden beeinflusst den Spielstand in 018 nicht.
- [x] **Typen nur aus `contracts/`:** `AreaMap` und `GroundKind` aus dem Proto. Die Datei ist kein Netzwerk-Typ; nur
  der Server liest sie.
- [x] **Offline-Fortschritt:** nicht betroffen.
- [x] **Keine unnötigen neuen Abhängigkeiten:** kein Paket (`System.Text.Json` ist da, `pngjs` kommt mit 017);
  Bodenstücke aus dem Pack aus 017 (CC0, älter als 7 Tage).
- [x] **Spielwerte:** keine, solange der Boden nichts blockiert.
- [x] **KISS:** eine Datei aus Zeichen, eine Nachricht, Übergänge aus den Nachbarn statt gespeicherter Bodenstücke.

## Risiken / Abweichungen

- **Größe des `Welcome`:** 72 KB je Verbindung. Auf dem Handy bei schlechtem Netz spürbar, aber einmalig. Wird es ein
  Problem, kommt die Karte per HTTP mit Cache (Entscheidung 2).
- **Leistung:** Bis zu ~1600 `drawImage` und ~13 000 Nachbar-Abfragen je Bild. Ruckelt es auf dem Testhandy (zusammen
  mit 017 AK-15), kommen vorgerenderte Blöcke (z. B. 16×16 Kacheln je Offscreen-Canvas) dazu. Das ändert nur
  `render.ts`.
- **Tileset-Logik:** Die 13 Formen setzen voraus, dass das Pack Übergänge auf der Weg- bzw. Wasserkachel zeichnet.
  Arbeitet es anders (Übergang auf der Graskachel), wird `edgeShape` auf Graskacheln angewandt; Namen und Tests passen
  sich mit an.
- **Fremdes Kartenformat:** Ein verbreitetes Format wie das des Editors *Tiled* hätte einen Editor gratis, speichert
  aber Bodenstücke des Packs statt Arten. Das widerspricht ADR 0017 (Namen statt Pack-Aufbau). Der Editor kommt mit
  einer eigenen Spec.
- **Gespeicherte Positionen außerhalb der Karte** bleiben in 018 möglich (man läuft über den Rand). 021 versetzt
  solche Figuren an den Startpunkt.
- **Trainings-Dummy (013)** steht bei (0, −128) auf dem Nord–Süd-Weg. Das passt; verschiebt sich der Weg, steht er im
  Gras.
