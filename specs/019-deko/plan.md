# 019 – Plan

Gehört zu: [spec.md](spec.md)

## Überblick

```
Inhalt  ── Map/Maps/meadow.json                 neues Feld "objects": Art und Kachel je Objekt (AK-3, AK-4)
        ── scripts/generate-map.py              verteilt Bäume, Büsche, Steine und hohes Gras nach Schema
Vertrag ── game.proto: AreaMap.objects (MapObject, Feld 7)
Server  ── Map/Domain/MapFile.cs, AreaMap.cs    Objekte lesen und prüfen; im Welcome mitschicken
Client  ── client/src/assets/tiles/            nature.png + nature.json (Atlas mit Fußpunkten) + LICENSES.md
        ── features/game/world/objects.ts       reine Logik: Objekte aus der Karte, Bildname (Variante), sichtbare Objekte
        ── render/render.ts                     Objekte, Figuren und Ziele gemeinsam nach Fußlinie; Namen und HP-Balken obenauf
        ── engine/game.ts, app/App.tsx          Objekte übernehmen, Atlas laden und übergeben
```

Objekte sind Teil der Karte aus 018: Der Server lädt sie mit der Datei und schickt sie im `Welcome`. In 019 sind sie
reine Darstellung, man läuft hindurch (AK-7). Ob eine Objektart blockiert, legt 021 fest.

**Voraussetzung:** 018 (Kartenformat, `MapFile`, `AreaMap`, `Welcome.area`, `world/hash.ts`, `sprites/atlas.ts`,
Modus `atlas` im Import-Skript, `generate-map.py`) und 017 (`RenderOptions`, `loadImage`, Register, ADR 0017).

## Kartenformat (Erweiterung)

```json
{
  "format": 1,
  "…": "wie 018",
  "objects": [
    { "kind": "tree", "x": 212, "y": 64 },
    { "kind": "tallgrass", "x": 213, "y": 70 }
  ]
}
```

- `kind`: Objektart, eine von `tree`, `bush`, `rock`, `tallgrass`. Eine neue Art ist eine Zeile in der Liste der
  bekannten Arten im Server und ein Eintrag im Atlas.
- `x`, `y`: Kartenkachel (wie `rows`). Der **Fußpunkt** des Objekts ist unten mittig in dieser Kachel.
- `objects` fehlt oder ist leer → keine Objekte.
- Höchstens ein Objekt je Kachel.

## Konstanten

| Name | Wert | Wo | Bedeutung |
|---|---|---|---|
| Objektarten | `tree`, `bush`, `rock`, `tallgrass` | `Map/Domain/MapFile.cs` | bekannte Arten; andere sind ein Fehler in der Datei |
| `TREE_PERMILLE`, `BUSH_PERMILLE`, `ROCK_PERMILLE`, `TALLGRASS_PERMILLE` | 15, 10, 3, 30 | `scripts/generate-map.py` | Anteil der freien Graskacheln je Art (zusammen etwa 2 000 Objekte) |
| `START_CLEARANCE` | 6 Kacheln | `scripts/generate-map.py`, `MeadowMapTests` | kein Objekt so nah am Startpunkt (dort erscheinen Figuren; mit 021 sonst im Baum) |
| `OBJECT_CULL_MARGIN` | 48 | `core/constants.ts` | Rand fürs Culling; kein Objekt ragt weiter über seinen Fußpunkt hinaus (Test am Atlas) |
| Einträge `tree`, `tree.<n>`, `bush`, `rock`, `tallgrass`, `tallgrass.<n>` | Rechteck und Fußpunkt | `assets/tiles/nature.json` | Bilder der Objekte im Atlas; `origin` = Fußpunkt |

Keine Spielwerte, solange Objekte nichts blockieren (021).

## Entschiedene Fragen (Plan)

1. **Objekt** = Art + Weltkachel `(tx, ty)`. Fußpunkt in Welt `(tx·16 + 8, ty·16 + 16)`. Das Bild wird mit dem
   `origin` seines Atlas-Eintrags auf den Fußpunkt gesetzt (ganze Weltpixel).
2. **Variante im Client:** Die Karte nennt nur die Art. Welches Bild einer Art (`tree`, `tree.1`, …) gezeichnet wird,
   wählt `hash2(tx, ty)` (018) aus der Gruppe der Art im Atlas. Eine weitere Variante ist ein neuer Atlas-Eintrag, keine
   Änderung der Karte. Gleiche Kachel → gleiches Bild für alle (AK-1).
3. **Verdeckung = Tiefe nach Fußlinie** (AK-5, AK-6). Gezeichnet wird: Boden → Startmarkierung (018) → Objekte, Ziele
   und Figuren gemeinsam nach Fußlinie aufsteigend → Pfeile mit ihrer Figur → **alle Namen und HP-Balken zuletzt**.
   Fußlinie: Figur `y + PLAYER_SIZE / 2`, Ziel (013/014) Unterkante seines Bilds, Objekt sein Fußpunkt. Bei Gleichstand
   zuerst das Objekt.
   - **Ganz verdeckend** sind hohe Objekte (Baum): Steht eine Figur dahinter (kleinere Fußlinie), wird sie zuerst
     gezeichnet und die Krone liegt über ihr.
   - **Teilweise verdeckend** sind niedrige Objekte (hohes Gras, Bildhöhe ≤ 10 Weltpixel): Steht eine Figur in der
     Kachel, liegt ihre Fußlinie über der des Grases; das Gras wird danach gezeichnet und deckt nur die unteren Pixel
     der Figur ab.
   Ob ganz oder teilweise, ergibt sich also aus der Bildhöhe der Art. Es braucht keine eigene Einstellung und keine
   zweite Zeichenregel.
4. **Scharf** (AK-2): ganzzahlige Quell- und Zielkoordinaten, keine Glättung (wie 017/018).
5. **Rückfall:** Ohne geladenen Atlas werden keine Objekte gezeichnet; das Spiel läuft. Eine Art ohne Eintrag im
   Atlas wird übersprungen.
6. **Prüfung der echten Karte** (AK-4): Objekte stehen nur auf Gras, nicht neben Weg oder Wasser (8 Nachbarn), nicht im
   Bergrand und nicht näher als `START_CLEARANCE` am Startpunkt. Der Trainings-Dummy (013) steht auf dem Weg, ist
   damit frei.
7. **Erzeugen:** `generate-map.py` legt nach dem Boden die Objekte fest: Jede freie Graskachel (Entscheidung 6) wird
   mit fester Saat einer Art zugeordnet oder bleibt leer (Anteile aus der Tabelle). Zwei Bäume stehen nie direkt
   nebeneinander, damit Kronen sich nicht überlappen. Wie in 018 ist danach die Datei die Wahrheit.
8. **Sichtbare Objekte:** Der Client legt beim Übernehmen der Karte ein Raster aus Blöcken zu 16×16 Kacheln an
   (Objekte je Block). Je Bild werden die Blöcke gesammelt, die das Sichtfeld plus `OBJECT_CULL_MARGIN` schneiden. Bei
   2 000 Objekten wäre auch eine Schleife über alle billig; das Raster hält es billig, wenn Karten wachsen.

## Verträge

**Protobuf** (`contracts/proto/browsergame/v1/game.proto`), abwärtskompatibel. `AreaMap` 7 ist frei (1–6 aus 018).

```proto
message AreaMap {
  // … 1–6 (018)
  // Deko-Objekte (019), höchstens eines je Kachel.
  repeated MapObject objects = 7;
}

// Deko-Objekt der Karte (019). Fußpunkt unten mittig in der Kachel.
message MapObject {
  // Art: "tree", "bush", "rock", "tallgrass".
  string kind = 1;
  // Weltkachel.
  int32 tx = 2;
  int32 ty = 3;
}
```

Die Art ist ein Text, kein Enum: Eine neue Art braucht dann keine Vertragsänderung, nur einen Atlas-Eintrag und eine
Zeile in der Liste der bekannten Arten. Bei etwa 2 000 Objekten wächst das `Welcome` um etwa 30 KB.

**OpenAPI:** keine Änderung.

## Server

- `MapFile.Parse` liest `objects`: unbekannte Art, Kachel außerhalb der Karte, zwei Objekte auf einer Kachel oder ein
  unbekanntes Feld → `MapFormatException` mit Fundstelle (Index des Objekts).
- `AreaMap` bekommt `Objects` (Art, Weltkachel); `GameLoop` schreibt sie in `Welcome.area.objects`.
- `World` und Bewegung bleiben unverändert (AK-7).

## Client

### Assets
- `client/src/assets/tiles/nature.png` und `nature.json`: Atlas mit Baum (mit Varianten), Busch, Stein und hohem Gras
  (mit Varianten), aus Ninja Adventure (z. B. `TilesetNature.png`) mit `import-sprites.mjs atlas` zusammengestellt
  (`atlas-nature.json` neben dem Skript, je Eintrag mit Fußpunkt); Zeile in `LICENSES.md`. `assets.test.ts` (018) prüft
  ihn mit.

### `world/objects.ts` (neu, rein)
- `interface MapObject { kind: string; tx: number; ty: number }`; `objectsFromProto(area)`.
- `objectImage(object, groups: ReadonlyMap<string, readonly string[]>): string | null` (Entscheidung 2).
- `class ObjectIndex` (Entscheidung 8): `constructor(objects)`, `inView(origin, viewWidth, viewHeight, margin)`.
- `footY(object)` = `ty·16 + 16`.

### `render/render.ts`
- `RenderOptions.objects?: { index: ObjectIndex; atlas: Atlas; image: CanvasImageSource; groups } | null`.
- Zeichenreihenfolge nach Entscheidung 3: Die Schleifen über Ziele und Figuren werden zu einer Schleife über eine nach
  Fußlinie sortierte Liste aus Objekten, Zielen und Figuren; Namen und HP-Balken in einer zweiten Schleife danach.

### `engine/game.ts`, `app/App.tsx`
- `Game` baut beim `Welcome` den `ObjectIndex`; `setNatureAtlas({ atlas, image })` aus `App`.
- `App`: laden wie den Boden-Atlas (018); Fehler → keine Objekte.

## Abdeckung der Akzeptanzkriterien

| AK | Wie erfüllt | Wie getestet |
|---|---|---|
| 1 | Objekte aus der Karte vom Server, Variante per Hash | Integration (WS): zwei Clients bekommen dieselben `objects`; Vitest `objects.test.ts`: `objectImage` gleich bei gleicher Kachel, alle Varianten kommen vor; `ObjectIndex.inView` liefert genau die Objekte im erweiterten Sichtfeld; Vitest `render-objects.test.ts`: `image`-Ops mit dem Rechteck des Eintrags, `origin` auf dem Fußpunkt; **manuell:** zwei Geräte am selben Ort |
| 2 | ganzzahlig, keine Glättung | Vitest: Ziel- und Quellkoordinaten aller Objekt-Ops ganzzahlig; **manuell:** PC und Handy |
| 3 | Objekte in der Datei | xUnit `MapFileTests`: `objects` gelesen, jeder Fehlerfall mit Fundstelle; Integration: eigene Karte über `Game:MapPath` mit einem Objekt → genau dieses im `Welcome` |
| 4 | Generator, Prüfung der echten Karte | xUnit `MeadowMapTests`: Bäume und Büsche vorhanden; jedes Objekt auf Gras, ohne Weg oder Wasser in den 8 Nachbarn, außerhalb des Bergrands und von `START_CLEARANCE`; keine zwei Bäume benachbart |
| 5 | Tiefe nach Fußlinie, Namen zuletzt | Vitest `render-objects.test.ts`: Figur mit kleinerer Fußlinie als ein Baum → Figur vor dem Baum gezeichnet; größere → danach; alle Namen und HP-Balken nach allen Bildern; Vitest: jedes Bild von `tree` ist höher als `PLAYER_SIZE`; **manuell** |
| 6 | niedrige Objekte, gleiche Regel | Vitest: Figur in einer Kachel mit hohem Gras → Gras danach gezeichnet; jedes Bild von `tallgrass` ist höchstens 10 hoch, also kleiner als die Figur; **manuell:** durch hohes Gras laufen, nur die Beine verdeckt |
| 7 | Bewegung kennt keine Objekte | Bestehende Bewegungstests (`World`) unverändert; Integration: Figur läuft über eine Kachel mit Baum (eigene Karte über `Game:MapPath`); **manuell** |

## Constitution-Check

- [x] **Server ist Autorität:** Die Objekte gehören zur Karte des Servers; in 019 beeinflussen sie den Spielstand nicht.
- [x] **Typen nur aus `contracts/`:** `MapObject` aus dem Proto.
- [x] **Offline-Fortschritt:** nicht betroffen.
- [x] **Keine unnötigen neuen Abhängigkeiten:** kein Paket; Objekte aus dem Pack aus 017 (CC0, älter als 7 Tage).
- [x] **Spielwerte:** keine, solange Objekte nichts blockieren.
- [x] **KISS:** eine Liste in der Karte, eine Sortierung für ganz und teilweise verdeckende Objekte.

## Risiken / Abweichungen

- **Teilweise Verdeckung an der Kachelgrenze:** Läuft eine Figur aus einer Kachel mit hohem Gras nach unten heraus,
  springt sie im Moment, in dem ihre Fußlinie die des Grases überschreitet, nach vorn. Bei einem ganzen Grasfeld deckt
  die nächste Kachel gleich wieder ab. Stört es, bekommt die Art einen Versatz der Fußlinie (ein Feld im Atlas).
- **Bestehende Tests:** Namen und HP-Balken wandern in eine zweite Schleife. Die Tests aus 013, 016 und 017 prüfen
  Bilder, Pfeile und Balken, nicht deren Reihenfolge zu den Namen; sie bleiben unverändert. Ist `render.ts` bis dahin
  anders aufgebaut (014), wird nur die Sortierung angepasst.
- **Größe des `Welcome`:** zusammen mit 018 etwa 100 KB (siehe 018, Risiken).
- **Markante Objekte:** Zufällig verteilte Objekte helfen der Orientierung nur begrenzt (User Story 2). Ein großer Baum
  oder Felsen an festen Stellen ist eine Zeile in der Karte, wenn gewünscht.
