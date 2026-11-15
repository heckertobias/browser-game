# 017 – Plan

Gehört zu: [spec.md](spec.md)

## Überblick

```
Client  ── client/src/assets/                 LICENSES.md · licenses.test.ts
        ── assets/sprites/humanoid/           rig.json (Beschreibung) · body-default.png · clothing-ninja.png
        ── sprites/rig.ts                     reine Logik: Beschreibung prüfen, Zelle im Blatt je Animation und Richtung
        ── sprites/direction.ts               reine Logik: Blickrichtung → eine von vier Richtungen
        ── sprites/compose.ts                 reine Logik: Teile einfärben und übereinanderlegen, Grau für Wartende
        ── sprites/spriteSet.ts               Aussehen → fertiges Blatt (Cache); Laden
        ── sprites/browser.ts                 Bild laden, Pixel lesen, Canvas erzeugen (nur Browser)
        ── render/render.ts                   Figur als Sprite statt Quadrat; Pfeil nur noch auf Wunsch; Quadrat als Rückfall
        ── state/prediction.ts, interpolation.ts  „läuft“ für eigene und fremde Figuren
        ── engine/game.ts, network/net.ts     Pfeil-Schalter (nur lokal), Sprites übernehmen
        ── app/App.tsx                        Sprites beim Start laden; Fehler → Quadrate
        ── client/scripts/import-sprites.mjs  Pack-Figur → Teile im eigenen Format (einmalig je Figur)
Server  ── Commands (015): neuer Command toggleFacingArrows() → Nachricht nur an den Absender
Doku    ── ADR 0016: Grafiken nur CC0 oder selbst erstellt; Register client/src/assets/LICENSES.md
        ── ADR 0017: Sprite-Format (Figur aus Teilen, Beschreibung statt Pack-Aufbau)
```

Pfade ohne `client/` liegen unter `client/src/features/game/`. `sprites/` ist ein neuer Unterordner dort.

Die Figur ist reine Darstellung. Position, Tempo und Blickrichtung kommen unverändert aus 001 und 016.
Der Server merkt sich den Pfeil-Schalter nicht; er bestätigt nur den Command.

**Voraussetzung:** 015 (`CommandRegistry`, `CommandContext`, `CommandDispatcher`, Abfangen im `GameLoop`) und 016
(Blickrichtung, Pfeil). 013 hat `render.ts` um Ziele und Projektile erweitert (`CombatScene`), 014 bringt Gegner; das
bleibt unberührt.

## Sprite-Format (wird ADR 0017)

Das Pack liefert fertige Figuren als ein Bild je Figur. Das reicht nicht, sobald Kleidung, Frisuren und Waffen
tauschbar sein sollen. Der Code kennt deshalb nie den Aufbau des Packs, sondern nur dieses Format.

**Begriffe**

- **Rig** = ein Figurentyp (hier `humanoid`): ein Ordner mit einer Beschreibung `rig.json` und den Bildern seiner
  Teile.
- **Teil** = ein Bild (PNG) für genau einen **Slot**, z. B. `body/default` im Slot `body`.
- **Aussehen** = welches Teil in welchem Slot steckt, optional mit Farbe. Das sind Daten, kein Code.

**`rig.json`** (Stand 017):

```json
{
  "cell": { "width": 48, "height": 48 },
  "origin": { "x": 24, "y": 24 },
  "directions": ["down", "up", "left", "right"],
  "animations": {
    "idle": { "firstRow": 0, "frames": 1 },
    "walk": { "firstRow": 0, "frames": 4, "frameMs": 100 }
  },
  "slots": ["body", "clothing"],
  "parts": {
    "body/default": { "slot": "body", "image": "body-default.png" },
    "clothing/ninja": { "slot": "clothing", "image": "clothing-ninja.png", "tint": true }
  }
}
```

**Regeln**

1. **Ein Raster je Rig.** Alle Teile eines Rigs haben dasselbe Blatt: Spalten = `directions`, Zeilen = Bilder. Größe
   `directions.length × cell.width` mal `rows × cell.height`, mit `rows` = größtes `firstRow + frames`. Für `humanoid`
   also 192×192. Ein Teil braucht deshalb keine eigene Beschreibung seiner Lage.
2. **Zelle größer als der Körper.** Der Körper ist 16×16 (`PLAYER_SIZE`) und liegt in der Mitte der Zelle. Die Zelle
   ist 48×48, also 3×3 Kacheln: rundum eine ganze Kachel Platz für Teile, die hinausragen (Waffe in der Hand, Hut).
3. **`origin`** ist der Punkt der Zelle, der auf der Weltposition der Figur liegt (hier die Mitte des Körpers).
4. **Animation** = benannter Bereich von Zeilen. `frameMs` ist Pflicht, sobald `frames > 1`. Bereiche dürfen sich
   überlappen (`idle` ist das erste Bild von `walk`). Eine neue Animation ist ein neuer Eintrag und neue Zeilen.
5. **`slots`** legt die Zeichenreihenfolge fest, von unten nach oben. Ein neuer Slot (`hair`, `weapon`) ist ein
   Eintrag in der Liste.
6. **Einfärbbare Teile** (`tint: true`) liegen in Graustufen vor (r = g = b). Die Laufzeit rechnet je Kanal
   `round(Grau × Farbe / 255)`. Das hellste Grau (255) ergibt genau die Farbe, dunklere Stufen ihre Schattierung.
7. **Alpha ist 0 oder 255.** Ein Pixel mit Alpha 255 ersetzt das Pixel darunter.
8. **Zusammensetzen einmal je Aussehen.** Die Laufzeit legt die Teile in Slot-Reihenfolge übereinander und cacht das
   fertige Blatt. Gezeichnet wird ein Bild je Figur.

**Bewusst noch nicht gebaut** (kommt mit der jeweiligen Spec; das Format lässt es zu, ohne bestehende Teile zu
ändern): Zeichenreihenfolge je Richtung (Waffe hinter dem Körper beim Blick nach oben), Ankerpunkte für Waffen,
Teile bei Bedarf nachladen, Aussehen vom Server (Vertrag), weitere Rigs (Gegner).

**Ausweichlösung: Zusammensetzen auf dem Server.** Zuerst setzt der Client zusammen. Zeigt sich das als Problem
(zu viele Teile zum Laden, Figuren außerhalb des Spiels), setzt der Server je Aussehen einmal zusammen, speichert das
fertige Blatt und liefert es per HTTP aus; der Client cacht die Datei. Damit dieser Weg offen bleibt, gilt:

- Das Format bleibt reine Daten (`rig.json`, PNGs) ohne Annahmen über den Browser.
- Zusammensetzen und Einfärben sind reine Funktionen über Pixel (`compose`, `tint`), nach den Regeln 6 und 7 genau
  festgelegt. Ein zweiter Nachbau in C# liefert dasselbe Bild.
- Zeichnen und Zusammensetzen sind getrennt: `render` bekommt ein fertiges Blatt und weiß nicht, woher es kommt.
- Ein Aussehen hat einen stabilen Schlüssel (`appearanceKey`), der später als Dateiname dienen kann.

Nicht in Frage kommt, dass ein Client sein fertiges Blatt hochlädt: Der Server würde ihm ein Bild glauben, das alle
sehen (Constitution §2).

**Später: Figuren außerhalb des Spiels über ein eigenes Paket.** Soll eine Figur auf einer Webseite erscheinen
(Profil, Startseite), wird `sprites/` als eigenes Paket herausgelöst und dort genauso eingebunden wie im Spiel. Das
ist der bevorzugte Weg für alles, was im Browser läuft; die Ausweichlösung auf dem Server bleibt für Orte ohne
JavaScript (Mails, Vorschaubilder). Damit das Herauslösen einfach bleibt, gilt ab jetzt: Code in `sprites/`
importiert nichts aus dem übrigen Spiel (kein Zustand, kein Netz, kein React, keine Konstanten aus `core/`). Nur
`sprites/browser.ts` nutzt das DOM. Spielbezogen und beim Herauslösen im Spiel zu lassen sind `playerAppearance`,
`HUMANOID_RIG`, `HUMANOID_IMAGES` und `assets.test.ts`.

Boden (018) und Deko (019) folgen demselben Grundsatz mit einem **Atlas**: ein Bild plus Beschreibung mit benannten
Einträgen. ADR 0017 nennt beide Formen; der Atlas wird in 018 gebaut.

## Konstanten

Alle Werte sind reine Darstellung (Constitution §8, „nicht gemeint“) und stehen im Client.

| Name | Wert | Wo | Bedeutung |
|---|---|---|---|
| `PLAYER_SIZE` | 16 | `core/constants.ts` (besteht) | Größe des Körpers in Weltpixeln (AK-1); Bezug für Name und Pfeil |
| `cell` | 48×48 | `rig.json` | Zelle eines Bilds im Blatt (Format-Regel 2, AK-20) |
| `origin` | 24, 24 | `rig.json` | Punkt der Zelle auf der Weltposition der Figur |
| `walk.frames` | 4 | `rig.json` | Bilder der Laufanimation |
| `walk.frameMs` | 100 | `rig.json` | Dauer eines Laufbilds (AK-7); bei der manuellen Prüfung nachstellbar |
| `FACING_TIE_EPSILON` | 1e-4 | `sprites/direction.ts` | Toleranz für „genau schräg“ (AK-5); fängt float32-Rundung der Blickrichtung ab |
| `AWAY_ALPHA` | 0,5 | `render/render.ts` (besteht) | Transparenz wartender Figuren (AK-9) |
| `PACK_CELL`, `PACK_COLUMNS`, `PACK_WALK_ROWS` | 16; unten/oben/links/rechts; 0–3 | `client/scripts/import-sprites.mjs` | Aufbau des Pack-Blatts; nur das Import-Skript kennt ihn (am Asset prüfen) |

## Entschiedene Fragen (Plan)

1. **Eigenes Format statt Pack-Blatt.** Ins Repo kommen nur Teile im Format oben, nicht das Blatt des Packs. Das Pack
   ist die Quelle (AK-17), der Code hängt nicht an seinem Aufbau.
2. **Zwei Teile aus zwei Farbvarianten.** Gewählt wird eine Figur, die es im Pack in mehreren Farbvarianten gibt
   (z. B. die Ninja-Figuren). Die Pixel, die sich zwischen zwei Varianten unterscheiden, sind die Kleidung. Das
   Import-Skript schreibt daraus `body-default.png` (alles außer der Kleidung) und `clothing-ninja.png` (nur die
   Kleidung, in Graustufen). Unter der Kleidung hat der Körper keine Pixel; das genügt, solange es ein Kleidungsteil
   gibt.
3. **Graustufen der Kleidung:** Helligkeit nach Rec. 601 (wie `desaturate`), geteilt durch die größte Helligkeit der
   Kleidung, mal 255. Die hellste Kleidungsfarbe wird so genau zur Spielerfarbe.
4. **Vier Richtungen** (AK-4, AK-5): `c = cos(facing)`, `s = sin(facing)`. Gilt `|c| ≥ |s| − FACING_TIE_EPSILON`, zeigt
   die Figur die Seite (`c ≥ 0` → rechts, sonst links), sonst `s > 0` → unten, sonst oben (y wächst nach unten, 016).
   Genau 45° liegt damit immer auf der Seite, auch nach float32-Rundung.
5. **Aussehen aus der Spielerfarbe** (AK-2, AK-18): Der Server schickt weiter nur die Farbe (001). Der Client bildet
   daraus `{ body: { part: "body/default" }, clothing: { part: "clothing/ninja", tint: color } }`. Das ist die einzige
   Stelle im Code, die Teile beim Namen nennt.
6. **Wartende Figuren** (AK-9): eigenes Blatt je Aussehen, erst zusammengesetzt, dann jedes Pixel grau; gezeichnet mit
   `AWAY_ALPHA`. Sie zeigen immer `idle`.
7. **„Läuft“** (AK-7, AK-8): eigene Figur, wenn sich die vorhergesagte Position im letzten Schritt geändert hat
   (`Predictor.moving`); fremde Figur, wenn sich ihre Position zwischen den beiden Snapshots unterscheidet, zwischen
   denen gerade interpoliert wird (`SnapshotBuffer.moving`). Keine neue Nachricht vom Server.
8. **Bild aus der Uhr:** `frame = floor(now / frameMs) mod frames`. Alle laufenden Figuren laufen im Gleichschritt;
   das ist einfach und fällt bei 16×16 kaum auf.
9. **Pfeil-Schalter über den Server** (AK-11–13): `/run toggleFacingArrows()` geht wie jeder Command aus 015 an den
   Server. Er kennt die Liste der Commands (015), zählt das Chat-Tempo und meldet Tippfehler. Der Command schickt
   `ToggleFacingArrows` nur an den Absender (AK-12); der Client kippt einen lokalen Schalter. Der Server speichert
   nichts, nach Neuladen ist der Schalter aus (AK-13).
10. **Pfeil-Standard in `render()` bleibt „an“.** `RenderOptions.facingArrows` fehlt → Pfeil wie in 016. So bleiben die
    bestehenden Tests der reinen Zeichenfunktion unverändert. Die Vorgabe „aus“ (AK-10) setzt `Game`.
11. **Rückfall** (AK-14): Bis die Sprites geladen sind und wenn das Laden scheitert (Beschreibung ungültig, ein Bild
    fehlt), zeichnet `render()` die Quadrate aus 001 mit Spielerfarbe und Grau für Wartende. Kein Fehlerhinweis.
    Nennt ein Aussehen ein unbekanntes Teil, bleibt nur dieser Slot leer.
12. **Laden im `App`, nicht im `Game`:** `Game` läuft in Vitest ohne `Image`/Canvas. `App` lädt die Sprites und
    übergibt sie mit `game.setSprites(…)`. Geladen werden alle Teile des Rigs; bei zwei Teilen ist das billig.
13. **Scharf** (AK-3): Die Zelle wird 1:1 an `round(x − origin.x)`, `round(y − origin.y)` gezeichnet, aus einer
    ganzzahligen Quell-Zelle; `imageSmoothingEnabled = false` gilt schon (007). Mit ganzzahligem `k` und auf
    Gerätepixel gerundeter Kamera (ADR 0010) liegt jedes Sprite-Pixel auf genau `k×k` Gerätepixeln. Der Körper liegt
    damit genau dort, wo bisher das Quadrat lag.
14. **Import-Skript statt Handarbeit:** `client/scripts/import-sprites.mjs` überführt eine Pack-Figur in Teile. So ist
    die Herleitung nachvollziehbar und weitere Pack-Figuren (Charakterauswahl) kommen auf demselben Weg. Die Dateien
    des Packs kommen nicht ins Repo; die Teile im Repo sind danach die Wahrheit und dürfen von Hand weitergezeichnet
    werden.
15. **Prüfung der echten Dateien:** Ein Vitest liest `rig.json` und jedes Teil und prüft die Format-Regeln. Ein neues
    Teil ist damit automatisch geprüft.
16. **Register und ADRs** (AK-16): `client/src/assets/` ist der eine Ort für fremde und eigene Grafiken und Schriften
    (auch für 018–020). `LICENSES.md` dort listet jede Datei mit Herkunft, Stand, Lizenz und Änderungen. Ein Vitest
    prüft, dass jede Datei im Ordner dort mit `CC0 1.0` oder `eigen` steht. ADR 0016 hält die Lizenzregel fest, ADR
    0017 das Format.

## Verträge

**Protobuf** (`contracts/proto/browsergame/v1/game.proto`), abwärtskompatibel. `ServerMessage` 9 ist auf `main` für
`ToggleFacingArrows` vorgemerkt (7 = `AttackRejected`, 8 = `CommandRejected` aus 015, 10 ff. = 013). Keine Eingabe
vom Client: Der Command kommt als `SendChat` (011/015).

```proto
message ServerMessage {
  oneof payload {
    // … 1–8, 10 ff.
    // Nur an den Absender von /run toggleFacingArrows(): Pfeile der Blickrichtung umschalten (017).
    ToggleFacingArrows toggle_facing_arrows = 9;
  }
}

// Leer. Der Client schaltet nur seine eigene Anzeige um; der Server merkt sich nichts (017 AK-12, AK-13).
message ToggleFacingArrows {}
```

Das Aussehen geht nicht über das Netz. Der Client leitet es aus der Farbe ab (Entscheidung 5).

**OpenAPI:** keine Änderung.

## Server

Alles in `Features/Game/Commands/Application/` aus 015.

- **`CommandContext`:** neue Eigenschaft `public Action<ServerMessage> Reply { get; init; } = static _ => { };`.
  Als `init` mit Standardwert, damit bestehende Aufrufe und Tests aus 014/015 unverändert kompilieren.
- **`CommandRegistry`:** `toggleFacingArrows` (0 Parameter):
  `context.Reply(new ServerMessage { ToggleFacingArrows = new ToggleFacingArrows() })`. Ändert nichts an der `World`
  (AK-12). Groß-/Kleinschreibung wie alle Commands (015).
- **`GameLoop`:** beim Bauen des `CommandContext` für einen Absender
  `Reply = message => sender.Outbox.SendRequired(message.ToByteArray())`.
- Keine Persistenz, keine Konstante, keine Konfiguration (AK-13; 015 AK-9/10).

## Client

### Assets (`client/src/assets/`, neu)
- `LICENSES.md`: Tabelle `| Datei | Herkunft | Stand | Lizenz | Änderungen |`. `Datei` ist der Pfad relativ zu
  `assets/`. Zeilen für `sprites/humanoid/body-default.png` und `sprites/humanoid/clothing-ninja.png`:
  Ninja Adventure – Asset Pack (Pixel-boy & AAA), `https://pixel-boy.itch.io/ninja-adventure-asset-pack`, Pfade der
  beiden Varianten im Pack, Stand (Update-Datum der geladenen Version), `CC0 1.0`, Änderungen „abgeleitet mit
  `import-sprites.mjs`: Körper ohne Kleidung“ bzw. „… Kleidung in Graustufen“, beide „Zelle 48×48“.
- `licenses.test.ts`: `Object.keys(import.meta.glob("./**/*.{png,ttf,otf,woff,woff2}"))` gegen
  `import licenses from "./LICENSES.md?raw"`. Jede Datei hat genau eine Tabellenzeile, die mit `` | `<pfad>` | ``
  beginnt und in der Spalte „Lizenz“ `CC0 1.0` oder `eigen` hat. Umgekehrt nennt jede Tabellenzeile eine vorhandene
  Datei.
- `sprites/humanoid/rig.json` wie oben; `body-default.png`, `clothing-ninja.png` aus dem Import-Skript.

### `client/scripts/import-sprites.mjs` (neu)
- Aufruf: `npm run sprites:import -- character --base <A.png> --variant <B.png> --rig src/assets/sprites/humanoid
  --body body-default.png --clothing clothing-ninja.png`. A und B sind die Blätter zweier Farbvarianten derselben
  Pack-Figur.
- Liest `rig.json` (Zelle, Ursprung, Richtungen, Zeilen). Kennt den Aufbau des Packs (`PACK_*`, Tabelle oben).
- Je Richtung und Laufbild: Pack-Zelle nach `origin − PACK_CELL / 2` in die Zielzelle. Maske = Pixel, in denen sich A
  und B unterscheiden. Körper = A ohne Maske. Kleidung = Maske in Graustufen (Entscheidung 3), Alpha 255.
- Bricht mit Meldung ab, wenn A und B verschieden groß sind oder die Maske leer ist. Gibt die Zahl der
  Kleidungspixel und ihre Originalfarben aus (für `LICENSES.md`).
- Neue Abhängigkeit **`pngjs`** (devDependency, PNG lesen und schreiben in Node), dazu `@types/pngjs` für den Test.

### `sprites/rig.ts` (neu, rein)
- `type SpriteDirection = "down" | "up" | "left" | "right"`.
- `interface Rig { cell; origin; directions; animations; slots; parts }` wie `rig.json`, alles `readonly`.
- `parseRig(data: unknown): Rig` prüft: ganze, positive Zellgröße; `origin` in der Zelle; `directions` nicht leer,
  ohne Doppelte, nur die vier Richtungen; je Animation `firstRow ≥ 0`, `frames ≥ 1`, `frameMs > 0` bei `frames > 1`;
  `slots` ohne Doppelte; jedes Teil nennt einen Slot aus `slots` und ein Bild. Wirft `Error` mit der Fundstelle.
- `sheetSize(rig): { width: number; height: number }` (Format-Regel 1).
- `frameRect(rig, animation: string, direction: SpriteDirection, now: number): { sx: number; sy: number }`:
  `sx = Spalte(direction) × cell.width`, `sy = (firstRow + frame) × cell.height`, `frame` nach Entscheidung 8
  (`frames = 1` → 0). Unbekannte Animation oder Richtung wirft.

### `sprites/direction.ts` (neu, rein)
- `spriteDirection(facing: number): SpriteDirection` nach Entscheidung 4.
- `FACING_TIE_EPSILON` (Tabelle oben) mit kurzem Kommentar; steht hier, nicht in `core/`, weil `sprites/` nichts aus
  dem übrigen Spiel importiert.

### `sprites/compose.ts` (neu, rein)
- `interface Pixels { readonly width: number; readonly height: number; readonly data: Uint8ClampedArray }`.
- `tint(pixels: Pixels, color: number): Pixels` (Format-Regel 6, Alpha unverändert).
- `compose(layers: readonly Pixels[]): Pixels` (Format-Regel 7; Reihenfolge = unten → oben; gleiche Größe verlangt).
- `greyscale(pixels: Pixels): Pixels` (Entscheidung 6, Alpha unverändert). Alle drei liefern neue Arrays.

### `sprites/spriteSet.ts` (neu)
- `type Appearance = Readonly<Record<string, { readonly part: string; readonly tint?: number }>>` (Slot → Teil).
- `playerAppearance(color: number): Appearance` (Entscheidung 5); je Farbe dasselbe Objekt.
- `appearanceKey(appearance: Appearance, slots: readonly string[]): string` – stabiler Schlüssel in
  Slot-Reihenfolge (Teil und Farbe je Slot); gleiche Aussehen ergeben denselben Schlüssel. Dient dem Cache.
- `class SpriteSet`: `constructor(rig: Rig, parts: ReadonlyMap<string, Pixels>, toSource: (p: Pixels) =>
  CanvasImageSource)`; `readonly rig`; `sheet(appearance: Appearance, away: boolean): CanvasImageSource` setzt beim
  ersten Aufruf je `(Aussehen, away)` zusammen (Slots in `rig.slots`-Reihenfolge, `tint` nur bei `tint: true`,
  unbekanntes Teil → Slot leer) und cacht.
- `loadSpriteSet(rigData: unknown, imageUrls: Readonly<Record<string, string>>, decode: (url: string) =>
  Promise<Pixels>, toSource): Promise<SpriteSet | null>` – `null`, wenn `parseRig` wirft, ein Bild fehlt, `decode`
  scheitert oder ein Blatt nicht `sheetSize(rig)` hat (AK-14); wirft nie.
- `HUMANOID_RIG` (Import von `@/assets/sprites/humanoid/rig.json`) und `HUMANOID_IMAGES` (Dateiname → URL, per
  `import.meta.glob` oder gleichwertig).

### `sprites/browser.ts` (neu)
- `loadImage(url): Promise<HTMLImageElement>` (`new Image()`, `src`, `await image.decode()`); auch für 018/019.
- `browserDecode(url): Promise<Pixels>` (über `loadImage`, Canvas, `getImageData`) und `browserSource(p)` (Canvas,
  `putImageData`). Nicht in Vitest getestet (kein DOM).

### `sprites/assets.test.ts` (neu, Entscheidung 15)
- Liest `rig.json` und die PNGs von `humanoid` mit `node:fs` und `pngjs`. Prüft: `parseRig` nimmt die Beschreibung an;
  jedes Teil hat `sheetSize(rig)`; Alpha nur 0 oder 255; Teile mit `tint: true` haben r = g = b und ihr hellstes Pixel
  ist 255; `idle`, `walk` und alle vier Richtungen sind da; kein Bild im Ordner ohne Eintrag in `parts`; die Pixel von
  `body/default` und `clothing/ninja` zusammen liegen in jeder Zelle im mittleren Quadrat der Größe `PLAYER_SIZE`.

### `render/render.ts`
- `DrawablePlayer.moving?: boolean`.
- `export interface RenderOptions { sprites?: SpriteSet | null; facingArrows?: boolean; now?: number }` als
  letzter, optionaler Parameter von `render()` (Standard `{}`), nach `scene` aus 013.
- Figur: Mit `sprites` →
  `ctx.globalAlpha = away ? AWAY_ALPHA : 1`,
  `ctx.drawImage(sprites.sheet(playerAppearance(color), away), sx, sy, cell.width, cell.height,
  round(x − origin.x), round(y − origin.y), cell.width, cell.height)` mit
  `frameRect(rig, moving && !away ? "walk" : "idle", spriteDirection(facing ?? π/2), now)`.
  Ohne `sprites` → Quadrat wie bisher.
- Pfeil nur, wenn `facing !== undefined && (options.facingArrows ?? true)` (Entscheidung 10).
- Name, Reihenfolge Figur → Pfeil → Name und Culling bleiben (AK-1). Der Name bleibt auf den Körper bezogen
  (`round(y − PLAYER_SIZE / 2) − 2`), nicht auf die Zelle. `CULL_MARGIN` (64) deckt die Zelle ab.
- `fakeContext.ts` (Testhilfe): **additiv** `drawImage(...)` als neue Op `{ kind: "image", sx, sy, sw, sh, dx, dy,
  dw, dh, source, alpha }` und `fillText(text, x, y)` als Op `{ kind: "text", text, x, y, font, alpha }`. Bestehende
  Filter `rects()`/`paths()` bleiben; neue Helfer `images()`, `texts()`.

### `state/prediction.ts`
- `get moving(): boolean { return this.x !== this.prevX || this.y !== this.prevY; }`.

### `state/interpolation.ts`
- `moving(renderTime: number): ReadonlySet<string>`: IDs, die in beiden Snapshots um `renderTime` vorkommen, denselben
  `spawnSeq` haben und deren `x` oder `y` sich unterscheidet. Liegt `renderTime` außerhalb (kein späterer oder kein
  früherer Snapshot): leere Menge. Die Suche nach den beiden Snapshots wird mit `sample` geteilt (private Hilfe);
  das Ergebnis von `sample` ändert sich nicht.

### `network/net.ts`
- `ConnectionHandlers.onToggleFacingArrows?(): void`; `createReceiver`: `case "toggleFacingArrows"` ruft ihn.

### `engine/game.ts`
- Felder `private facingArrows = false` (AK-10) und `private sprites: SpriteSet | null = null`.
- `connect(...)`-Handler `onToggleFacingArrows: () => { this.facingArrows = !this.facingArrows; }` (AK-11).
- `setSprites(sprites: SpriteSet | null): void`.
- `draw(now)`: `const moving = this.snapshots.moving(now − INTERPOLATION_DELAY_MS)`; fremde Figuren mit
  `moving: moving.has(p.id)`; eigene mit `moving: this.predictor.moving`;
  `render(…, { sprites: this.sprites, facingArrows: this.facingArrows, now })`.

### `app/App.tsx`
- Nach `game.start()`:
  `void loadSpriteSet(HUMANOID_RIG, HUMANOID_IMAGES, browserDecode, browserSource).then((s) => { game.setSprites(s); });`

### Doku
- `docs/decisions/0016-grafiken-nur-cc0.md` (Entscheidung 16): Regel „nur CC0 1.0 oder selbst erstellt“ für Grafiken
  und Schriften; Register `client/src/assets/LICENSES.md`; Prüfung per Vitest; abgeleitete Werke erlaubt; keine
  Credits im Spiel nötig. Alternativen: CC-BY (Namensnennung im Spiel, 017 Out of Scope), SIL OFL für Schriften
  (offen in 020).
- `docs/decisions/0017-sprite-format.md`: Inhalt des Abschnitts „Sprite-Format“ oben, dazu der Atlas für 018/019 als
  zweite Form. Kontext: tauschbare Kleidung, Frisuren, Waffen. Alternativen: Pack-Blätter unverändert mit Konstanten
  im Code (bisheriger Plan; ein Teiletausch wäre ein Umbau), Einfärben über exakte Farbwerte (bricht bei jeder neuen
  Figur), Ankerpunkte statt voller Blätter (weniger Zeichenarbeit, mehr Logik; später möglich), eine
  Spiele-Bibliothek (neue Abhängigkeit ohne Bedarf, Canvas 2D reicht), Zusammensetzen auf dem Server (als
  Ausweichlösung festgehalten, siehe oben), Hochladen fertiger Blätter durch den Client (verworfen, §2). Ausblick: `sprites/` als eigenes Paket für
  Webseiten (siehe oben).
- `docs/project-structure.md`: `client/src/assets/` und `features/game/sprites/` in den Client-Baum,
  `client/scripts/import-sprites.mjs`; ADR-Zähler „0001–0017“.

## Abdeckung der Akzeptanzkriterien

| AK | Wie erfüllt | Wie getestet |
|---|---|---|
| 1 | Zelle so gezeichnet, dass der Körper auf dem alten Quadrat liegt; Name unverändert | Vitest `render-sprite.test.ts`: mit `sprites` je Figur ein `image`-Op in Zellgröße an `round(x − origin.x), round(y − origin.y)`, kein 16×16-Rechteck; `text`-Op mit dem Namen an derselben Stelle wie ohne Sprites; `assets.test.ts`: Pixel der Figur liegen im mittleren 16×16 |
| 2 | Kleidungsteil mit Spielerfarbe eingefärbt | Vitest `compose.test.ts`: `tint` mit zwei Farben → Grau 255 wird genau die Farbe, dunkleres Grau anteilig; `spriteSet.test.ts`: zwei Farben → nur die Pixel der Kleidung unterscheiden sich; **manuell:** zwei Geräte |
| 3 | ganzzahlige Ziel- und Quellkoordinaten, keine Glättung | Vitest: `dx, dy, sx, sy` ganzzahlig, `dw = sw`, `dh = sh`, `imageSmoothingEnabled === false`; **manuell:** PC (DPR 1, 1,25, 2) und Handy |
| 4 | `spriteDirection` | Vitest `direction.test.ts`: 0 → rechts, π/2 → unten, π → links, −π/2 → oben, 30° → rechts, 60° → unten, 170° → links |
| 5 | Toleranz bei 45° | Vitest: ±45°, ±135° exakt und als `Math.fround(...)` → Seite |
| 6 | Standard-Blickrichtung π/2 (016) | Vitest: `spriteDirection(Math.fround(Math.PI / 2))` → unten; `Game`: erste eigene Figur zeigt die Spalte „unten“ |
| 7 | `moving` + `frameRect` | Vitest `rig.test.ts`: `frameRect(…, "walk", …)` wechselt alle `frameMs` die Zeile, nach `frames` wieder die erste; `Predictor.moving` nach Schritt mit Richtung `true`; `SnapshotBuffer.moving` enthält eine Figur, deren Position sich zwischen den Snapshots ändert; **manuell** |
| 8 | Stillstand → `idle`, Richtung bleibt | Vitest: `Predictor.moving` nach Schritt mit (0, 0) `false`; `SnapshotBuffer.moving` ohne Positionsänderung leer; `frameRect(…, "idle", …, t)` gleich für alle `t` |
| 9 | graues Blatt, `AWAY_ALPHA`, kein Laufen | Vitest: `greyscale` liefert r = g = b, Alpha gleich; `render`: wartende Figur zieht `sheet(…, true)`, Alpha < 1, `idle` auch bei `moving: true` |
| 10 | `Game.facingArrows = false` | Vitest `game-facing-arrows.test.ts`: nach Welcome + Snapshot keine `fill`-Pfade |
| 11 | `toggleFacingArrows` → `ToggleFacingArrows` → Schalter | Unit (Server): Command ruft `Reply` genau einmal mit `ToggleFacingArrows`; Integration (WS): Absender bekommt die Nachricht; Vitest: nach einer Nachricht Pfeil an allen sichtbaren Figuren, nach der zweiten keiner; `net`: Fall wird weitergereicht |
| 12 | `Reply` nur an den Absender, `World` unverändert | Unit: Positionen, HP, Blickrichtung aller Figuren unverändert; Integration (WS): zweiter Spieler bekommt weder `ToggleFacingArrows` noch `ChatMessage` |
| 13 | nur lokaler Zustand | Vitest: neue `Game`-Instanz zeigt keine Pfeile, auch nach Toggle in einer früheren; Server hat keinen Zustand (Code-Review); **manuell:** Neuladen |
| 14 | Quadrate ohne Sprites | Vitest: `loadSpriteSet` mit scheiterndem `decode`, ungültiger Beschreibung, fehlendem Bild oder falscher Blattgröße → `null`, wirft nicht; `render` ohne `sprites` zeichnet 16×16-Rechtecke; **manuell:** Grafik im Browser blockieren |
| 15 | ein `drawImage` je Figur, Blätter gecacht | Vitest: `sheet` setzt je `(Aussehen, away)` nur einmal zusammen (`toSource` gezählt); **manuell:** 50 laufende Figuren auf dem Testhandy (siehe Risiken) |
| 16 | Register + Test + ADRs | Vitest `licenses.test.ts`; Review von ADR 0016 und 0017 |
| 17 | Teile aus einer Pack-Figur abgeleitet, Farben außer der Kleidung unverändert | `assets.test.ts` (Körper 16×16 in der Zellmitte); Herkunft und Änderungen in `LICENSES.md` (Review); Ausgabe des Import-Skripts |
| 18 | zwei Teile, `compose`, `tint` nur auf der Kleidung | Vitest `compose.test.ts`: oberes Teil ersetzt nur seine deckenden Pixel; `spriteSet.test.ts`: Körperpixel bei jeder Farbe gleich; `assets.test.ts`: `humanoid` hat `body/default` (ohne `tint`) und `clothing/ninja` (`tint`) |
| 19 | Teile und Slots sind Daten in `rig.json` | Vitest `spriteSet.test.ts` mit eigenem Test-Rig: zweites Kleidungsteil und dritter Slot nur in der Beschreibung → `sheet` nutzt sie in Slot-Reihenfolge; `rig.test.ts`: `parseRig` nimmt gültige Beschreibungen an und meldet jede Regelverletzung; `assets.test.ts` prüft jedes Teil im Ordner |
| 20 | Zelle 48×48 um den 16×16-Körper, ganze Zelle wird gezeichnet | Vitest: Test-Teil mit Pixeln in den Ecken der Zelle bleibt nach `compose` erhalten; `render` zeichnet die ganze Zelle (`dw = cell.width`, `dh = cell.height`); `assets.test.ts`: Zelle von `humanoid` lässt rund um den Körper mindestens `PLAYER_SIZE` Platz |

## Constitution-Check

- [x] **Server ist Autorität:** Die Figur zeigt nur, was Server (Position, Blickrichtung, Farbe) und Vorhersage
  liefern. Der Command läuft über den Server wie alle aus 015.
- [x] **Typen nur aus `contracts/`:** `ToggleFacingArrows` in Protobuf. `Rig` und `Appearance` gehen nicht über das
  Netz.
- [x] **Offline-Fortschritt:** nicht betroffen.
- [x] **Keine unnötigen neuen Abhängigkeiten:** `pngjs` und `@types/pngjs` als devDependencies; Grund: Import-Skript
  und Prüfung der echten Bilder brauchen einen PNG-Leser in Node. Beide Versionen müssen mindestens 7 Tage alt sein.
  Neu sind außerdem Grafiken aus Ninja Adventure (CC0, Update vom 2026-03-29).
- [x] **Spielwerte:** keine neuen. Animationstempo, Größen und Farben sind Darstellung (§8 „nicht gemeint“).
- [ ] **KISS – bewusste Abweichung:** Format, Slots und größere Zelle gehen über das hinaus, was zwei feste Teile
  brauchen. Begründung: Der Wechsel des Formats wäre später ein Neu-Export aller Grafiken; ADR 0017 hält das fest.
  Gebaut wird nur, was 017 nutzt (zwei Slots, zwei Animationen); alles Weitere steht unter „bewusst noch nicht
  gebaut“.

## Risiken / Abweichungen

- **Bestehender Test ändert sich:** `engine/game-facing.test.ts` (016) erwartet Pfeile ohne Zutun. Nach AK-10 sind sie
  aus. Die drei Tests müssen vor dem Zeichnen `ToggleFacingArrows` empfangen; ihre Prüfungen bleiben. Das braucht die
  Freigabe des Users (Frage an den Koordinator).
- **Testhilfe `fakeContext.ts`** bekommt `drawImage`/`fillText`-Ops dazu. Additiv, keine bestehende Prüfung ändert sich.
- **Asset-Download:** Das Pack (89 MB) kommt von itch.io ohne festen Download-Link. Jemand muss es einmal laden; ins Repo
  kommen nur die abgeleiteten Teile.
- **Pack-Aufbau:** Das Update #8 („Animation V2“) kann den Aufbau geändert haben. Dann nur die `PACK_*`-Werte im
  Import-Skript anpassen; gibt es kein kombiniertes Blatt mehr, anhalten und fragen.
- **Farbvarianten:** Unterscheiden sich zwei Varianten in mehr als der Kleidung (oder gibt es keine passenden),
  liefert die Maske kein sauberes Kleidungsteil. Dann anhalten und fragen (Maske von Hand).
- **Das Pack hat keine Teile.** Jedes weitere Kleidungsstück, jede Frisur und jede Waffe ist Zeichenarbeit in allen
  Bildern. Tauschbare Kleidung braucht außerdem einen gezeichneten Körper darunter (Entscheidung 2).
- **Zellgröße ändern** heißt alle Teile des Rigs neu exportieren. Deshalb ist sie von Anfang an 48×48.
- **Alle Teile werden beim Start geladen.** Bei vielen Teilen kommt Nachladen bei Bedarf dazu (eigene Spec); reicht
  das nicht, greift die Ausweichlösung „Zusammensetzen auf dem Server“ (Abschnitt Sprite-Format).
- **Palette:** Die eingefärbte Kleidung nutzt die Spielerfarben aus 001, nicht die Palette des Packs (Frage an den
  User).
- **AK-15 braucht 50 laufende Figuren.** Ohne Bots gibt es die nicht mit einem Gerät (Frage an den User).
- **ADRs im `impl/`-Branch:** Die Spec verlangt beide ADRs; sie gehören zu dieser Spec und kommen daher mit T1, nicht
  über `chore/` (ADR 0011).
- **Gleichschritt aller Figuren** (Entscheidung 8) – falls störend, später eine Phase je Figur.
