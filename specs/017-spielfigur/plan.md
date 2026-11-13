# 017 – Plan

Gehört zu: [spec.md](spec.md)

## Überblick

```
Client  ── client/src/assets/                 player.png (Ninja Adventure, unverändert) · LICENSES.md · licenses.test.ts
        ── render/spriteFrames.ts             reine Logik: Blickrichtung → eine von vier Richtungen, Zelle im Blatt
        ── render/playerSprites.ts            Blatt laden, Kleidung je Spielerfarbe einfärben, Grau für Wartende, Cache
        ── render/render.ts                   Figur als Sprite statt Quadrat; Pfeil nur noch auf Wunsch; Quadrat als Rückfall
        ── state/prediction.ts, interpolation.ts  „läuft“ für eigene und fremde Figuren
        ── engine/game.ts, network/net.ts     Pfeil-Schalter (nur lokal), Sprites übernehmen
        ── app/App.tsx                        Blatt beim Start laden; Fehler → Quadrate
Server  ── Commands (015): neuer Command toggleFacingArrows() → Nachricht nur an den Absender
Doku    ── ADR 0016: Grafiken nur CC0 oder selbst erstellt; Register client/src/assets/LICENSES.md
```

Die Figur ist reine Darstellung. Position, Tempo und Blickrichtung kommen unverändert aus 001 und 016.
Der Server merkt sich den Pfeil-Schalter nicht; er bestätigt nur den Command.

**Voraussetzung:** 015 (`CommandRegistry`, `CommandContext`, `CommandDispatcher`, Abfangen im `GameLoop`) und 016
(Blickrichtung, Pfeil). 013 und 014 ändern bis dahin `render.ts` (Dummy, Gegner, Projektile); das bleibt unberührt.

## Konstanten

Alle Werte sind reine Darstellung (Constitution §8, „nicht gemeint“) und stehen im Client.

| Name | Wert | Wo | Bedeutung |
|---|---|---|---|
| `PLAYER_SIZE` | 16 | `core/constants.ts` (besteht) | Zielgröße der Figur in Weltpixeln (AK-1); Sprite wird 1:1 gezeichnet |
| `SPRITE_CELL` | 16 | `core/constants.ts` | Kantenlänge einer Zelle im Blatt (Raster von Ninja Adventure, AK-17) |
| `WALK_FRAMES` | 4 | `core/constants.ts` | Bilder der Laufanimation |
| `WALK_FRAME_MS` | 100 | `core/constants.ts` | Dauer eines Laufbilds (Animationstempo, AK-7); bei der manuellen Prüfung nachstellbar |
| `FACING_TIE_EPSILON` | 1e-4 | `core/constants.ts` | Toleranz für „genau schräg“ (AK-5); fängt float32-Rundung der Blickrichtung ab |
| `SHEET_COLUMN` | `{ down: 0, up: 1, left: 2, right: 3 }` | `render/spriteFrames.ts` | Spalte je Richtung im Blatt (am Asset prüfen) |
| `IDLE_ROW` | 0 | `render/spriteFrames.ts` | Zeile des Standbilds (am Asset prüfen) |
| `WALK_FIRST_ROW` | 0 | `render/spriteFrames.ts` | erste Zeile der Laufbilder; Zeilen `WALK_FIRST_ROW … + WALK_FRAMES − 1` (am Asset prüfen) |
| `CLOTHING_COLORS` | aus dem Asset | `render/playerSprites.ts` | Originalfarben der Kleidung, hell → dunkel (Entscheidung 4) |
| `CLOTHING_SHADES` | `[1, 0.78, 0.56]` | `render/playerSprites.ts` | Helligkeit je Kleidungsfarbe relativ zur Spielerfarbe; gleiche Länge wie `CLOTHING_COLORS` |
| `AWAY_ALPHA` | 0,5 | `render/render.ts` (besteht) | Transparenz wartender Figuren (AK-9) |

Die Werte „am Asset prüfen“ sind der Aufbau des bekannten Ninja-Adventure-Blatts (`SpriteSheet.png`, 16×16-Zellen,
Spalten unten/oben/links/rechts, Zeilen 0–3 Laufen). Weicht die geladene Version ab, werden nur die Konstanten angepasst.

## Entschiedene Fragen (Plan)

1. **Ein Original-Blatt, Einfärben zur Laufzeit.** Ins Repo kommt das `SpriteSheet.png` einer Ninja-Adventure-Figur
   unverändert als `client/src/assets/player.png`. Die Kleidung wird beim Laden pro Spielerfarbe umgefärbt
   (Entscheidung 4). So gibt es keine abgeleitete Grafik, die von Hand gepflegt werden muss; Raster und Palette des
   Packs bleiben (AK-17).
2. **Figurwahl:** eine Figur, die es im Pack in mehreren Farbvarianten gibt (z. B. die Ninja-Figuren). Die Pixel, die
   sich zwischen zwei Varianten unterscheiden, sind die Kleidung; ihre Farben in der gewählten Variante ergeben
   `CLOTHING_COLORS`. Diese Farben dürfen sonst nirgends in der Figur vorkommen. Das wird bei der Wahl einmal geprüft
   (z. B. Pixel zählen im Browser) und in `LICENSES.md` unter „Änderungen“ vermerkt.
3. **Vier Richtungen** (AK-4, AK-5): `c = cos(facing)`, `s = sin(facing)`. Gilt `|c| ≥ |s| − FACING_TIE_EPSILON`, zeigt
   die Figur die Seite (`c ≥ 0` → rechts, sonst links), sonst `s > 0` → unten, sonst oben (y wächst nach unten, 016).
   Genau 45° liegt damit immer auf der Seite, auch nach float32-Rundung.
4. **Einfärben** (AK-2): Jedes Pixel mit Alpha > 0, dessen RGB genau `CLOTHING_COLORS[i]` ist, wird zu
   `round(Kanal(Spielerfarbe) × CLOTHING_SHADES[i])`. Alle anderen Pixel bleiben gleich. Das Ergebnis wird je Farbe
   einmal als Canvas erzeugt und gecacht (höchstens 12 Farben aus 001 × 2 Zustände).
5. **Wartende Figuren** (AK-9): eigenes Blatt je Farbe, erst eingefärbt, dann jedes Pixel per `desaturate` (besteht in
   `render.ts`) grau; gezeichnet mit `AWAY_ALPHA`. Sie zeigen immer das Standbild.
6. **„Läuft“** (AK-7, AK-8): eigene Figur, wenn sich die vorhergesagte Position im letzten Schritt geändert hat
   (`Predictor.moving`); fremde Figur, wenn sich ihre Position zwischen den beiden Snapshots unterscheidet, zwischen
   denen gerade interpoliert wird (`SnapshotBuffer.moving`). Keine neue Nachricht vom Server.
7. **Laufbild aus der Uhr:** `frame = floor(now / WALK_FRAME_MS) mod WALK_FRAMES`. Alle laufenden Figuren laufen im
   Gleichschritt; das ist einfach und fällt bei 16×16 kaum auf.
8. **Pfeil-Schalter über den Server** (AK-11–13): `/run toggleFacingArrows()` geht wie jeder Command aus 015 an den
   Server. Er kennt die Liste der Commands (015), zählt das Chat-Tempo und meldet Tippfehler. Der Command schickt
   `ToggleFacingArrows` nur an den Absender (AK-12); der Client kippt einen lokalen Schalter. Der Server speichert
   nichts, nach Neuladen ist der Schalter aus (AK-13).
9. **Pfeil-Standard in `render()` bleibt „an“.** `RenderOptions.facingArrows` fehlt → Pfeil wie in 016. So bleiben die
   bestehenden Tests der reinen Zeichenfunktion unverändert. Die Vorgabe „aus“ (AK-10) setzt `Game`.
10. **Rückfall** (AK-14): Bis das Blatt geladen ist und wenn das Laden scheitert, zeichnet `render()` die Quadrate aus
    001 mit Spielerfarbe und Grau für Wartende. Kein Fehlerhinweis.
11. **Laden im `App`, nicht im `Game`:** `Game` läuft in Vitest ohne `Image`/Canvas. `App` lädt das Blatt und übergibt
    es mit `game.setPlayerSprites(…)`.
12. **Scharf** (AK-3): Die Figur wird an ganzzahligen Weltkoordinaten (`Math.round` wie bisher) mit der Größe
    `PLAYER_SIZE` aus einer ganzzahligen Quell-Zelle gezeichnet; `imageSmoothingEnabled = false` gilt schon (007).
    Mit ganzzahligem `k` und auf Gerätepixel gerundeter Kamera (ADR 0010) liegt jedes Sprite-Pixel auf genau `k×k`
    Gerätepixeln.
13. **Register und ADR** (AK-16): `client/src/assets/` ist der eine Ort für fremde und eigene Grafiken und Schriften
    (auch für 018–020). `LICENSES.md` dort listet jede Datei mit Herkunft, Stand, Lizenz und Änderungen. Ein Vitest
    prüft, dass jede Datei im Ordner dort mit `CC0 1.0` oder `eigen` steht. ADR 0016 hält die Regel fest.

## Verträge

**Protobuf** (`contracts/proto/browsergame/v1/game.proto`), abwärtskompatibel. Feldnummern sind mit 013 und 015
abgestimmt: `ServerMessage` 7 = `AttackRejected` (013), 8 = `CommandRejected` (015), **9 = `ToggleFacingArrows`
(017)**. Keine Eingabe vom Client: Der Command kommt als `SendChat` (011/015).

```proto
message ServerMessage {
  oneof payload {
    // … 1–6, 7: AttackRejected (013), 8: CommandRejected (015)
    // Nur an den Absender von /run toggleFacingArrows(): Pfeile der Blickrichtung umschalten (017).
    ToggleFacingArrows toggle_facing_arrows = 9;
  }
}

// Leer. Der Client schaltet nur seine eigene Anzeige um; der Server merkt sich nichts (017 AK-12, AK-13).
message ToggleFacingArrows {}
```

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
- `player.png`: `SpriteSheet.png` der gewählten Figur, unverändert (Entscheidung 1, 2).
- `LICENSES.md`: Tabelle `| Datei | Herkunft | Stand | Lizenz | Änderungen |`. Zeile für `player.png`:
  Ninja Adventure – Asset Pack (Pixel-boy & AAA), `https://pixel-boy.itch.io/ninja-adventure-asset-pack`, Pfad im
  Pack, Stand (Update-Datum der geladenen Version), `CC0 1.0`, „keine; Kleidung wird zur Laufzeit eingefärbt“.
- `licenses.test.ts`: `Object.keys(import.meta.glob("./*.{png,ttf,otf,woff,woff2}"))` gegen
  `import licenses from "./LICENSES.md?raw"`. Jede Datei hat genau eine Tabellenzeile, die mit `` | `<datei>` | ``
  beginnt und in der Spalte „Lizenz“ `CC0 1.0` oder `eigen` hat. Umgekehrt nennt jede Tabellenzeile eine vorhandene
  Datei.

### `core/constants.ts`
- `SPRITE_CELL`, `WALK_FRAMES`, `WALK_FRAME_MS`, `FACING_TIE_EPSILON` (Tabelle oben), je mit kurzem Kommentar.

### `render/spriteFrames.ts` (neu, rein)
- `type SpriteDirection = "down" | "up" | "left" | "right"`.
- `spriteDirection(facing: number): SpriteDirection` nach Entscheidung 3.
- `spriteCell(direction, moving: boolean, now: number): { sx: number; sy: number }` – Quellpunkt in Pixeln:
  `sx = SHEET_COLUMN[direction] × SPRITE_CELL`; `sy = (moving ? WALK_FIRST_ROW + frame : IDLE_ROW) × SPRITE_CELL`
  mit `frame` nach Entscheidung 7.

### `render/images.ts` (neu)
- `loadImage(url): Promise<HTMLImageElement>` (`new Image()`, `src`, `await image.decode()`). Wird auch von 018/019
  genutzt. Nicht in Vitest getestet (kein DOM).

### `render/playerSprites.ts` (neu)
- `interface Pixels { readonly width: number; readonly height: number; readonly data: Uint8ClampedArray }`.
- `recolor(base: Pixels, color: number): Pixels` (Entscheidung 4), `greyscale(pixels: Pixels): Pixels`
  (Entscheidung 5, Alpha unverändert). Beide rein, liefern neue Arrays.
- `class PlayerSprites`: `constructor(base: Pixels, toSource: (p: Pixels) => CanvasImageSource)`;
  `sheet(color: number, away: boolean): CanvasImageSource` erzeugt beim ersten Aufruf je `(color, away)` und cacht.
- `loadPlayerSprites(url, decode: (url: string) => Promise<Pixels>, toSource): Promise<PlayerSprites | null>` –
  `null`, wenn `decode` scheitert (AK-14); wirft nie.
- `browserDecode(url)` (über `loadImage`, Canvas, `getImageData`) und `browserSource(p)` (Canvas, `putImageData`) als
  Browser-Umsetzung; nicht in Vitest getestet.
- `export { default as PLAYER_SHEET_URL } from "@/assets/player.png"` (oder gleichwertiger Import).
- `CLOTHING_COLORS`, `CLOTHING_SHADES` (Tabelle oben).

### `render/render.ts`
- `DrawablePlayer.moving?: boolean`.
- `export interface RenderOptions { sprites?: PlayerSprites | null; facingArrows?: boolean; now?: number }` als
  letzter, optionaler Parameter von `render()` (Standard `{}`); vorhandene Parameter aus 013/014 bleiben davor.
- Figur: Mit `sprites` →
  `ctx.globalAlpha = away ? AWAY_ALPHA : 1`,
  `ctx.drawImage(sprites.sheet(color, away), sx, sy, SPRITE_CELL, SPRITE_CELL, left, top, PLAYER_SIZE, PLAYER_SIZE)`
  mit `spriteCell(spriteDirection(facing ?? π/2), moving && !away, now)`. Ohne `sprites` → Quadrat wie bisher.
- Pfeil nur, wenn `facing !== undefined && (options.facingArrows ?? true)` (Entscheidung 9).
- Name, Reihenfolge Figur → Pfeil → Name und Culling bleiben (AK-1).
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
- Felder `private facingArrows = false` (AK-10) und `private sprites: PlayerSprites | null = null`.
- `connect(...)`-Handler `onToggleFacingArrows: () => { this.facingArrows = !this.facingArrows; }` (AK-11).
- `setPlayerSprites(sprites: PlayerSprites | null): void`.
- `draw(now)`: `const moving = this.snapshots.moving(now − INTERPOLATION_DELAY_MS)`; fremde Figuren mit
  `moving: moving.has(p.id)`; eigene mit `moving: this.predictor.moving`;
  `render(…, { sprites: this.sprites, facingArrows: this.facingArrows, now })`.

### `app/App.tsx`
- Nach `game.start()`:
  `void loadPlayerSprites(PLAYER_SHEET_URL, browserDecode, browserSource).then((s) => { game.setPlayerSprites(s); });`

### Doku
- `docs/decisions/0016-grafiken-nur-cc0.md` (Entscheidung 13): Regel „nur CC0 1.0 oder selbst erstellt“ für Grafiken
  und Schriften; Register `client/src/assets/LICENSES.md`; Prüfung per Vitest; abgeleitete Werke erlaubt; keine
  Credits im Spiel nötig. Alternativen: CC-BY (Namensnennung im Spiel, 017 Out of Scope), SIL OFL für Schriften
  (offen in 020).
- `docs/project-structure.md`: `client/src/assets/` in den Client-Baum; ADR-Zähler „0001–0016“.

## Abdeckung der Akzeptanzkriterien

| AK | Wie erfüllt | Wie getestet |
|---|---|---|
| 1 | Sprite in `PLAYER_SIZE` an derselben Stelle wie das Quadrat; Name unverändert | Vitest `render-sprite.test.ts`: mit `sprites` je Figur ein `image`-Op mit `dw = dh = PLAYER_SIZE` an `round(x − 8), round(y − 8)`, kein 16×16-Rechteck; `text`-Op mit dem Namen an derselben Stelle wie ohne Sprites |
| 2 | `recolor` | Vitest `playerSprites.test.ts`: zwei Farben → Kleidungspixel tragen `color × shade`, alle anderen Pixel gleich dem Original; **manuell:** zwei Geräte, nur die Kleidung unterscheidet sich |
| 3 | ganzzahlige Ziel- und Quellkoordinaten, keine Glättung | Vitest: `dx, dy, sx, sy` ganzzahlig, `imageSmoothingEnabled === false`; **manuell:** PC (DPR 1, 1,25, 2) und Handy |
| 4 | `spriteDirection` | Vitest `spriteFrames.test.ts`: 0 → rechts, π/2 → unten, π → links, −π/2 → oben, 30° → rechts, 60° → unten, 170° → links |
| 5 | Toleranz bei 45° | Vitest: ±45°, ±135° exakt und als `Math.fround(...)` → Seite |
| 6 | Standard-Blickrichtung π/2 (016) | Vitest: `spriteDirection(Math.fround(Math.PI / 2))` → unten; `Game`: erste eigene Figur zeigt Zelle „unten“ |
| 7 | `moving` + `spriteCell` | Vitest: `spriteCell` wechselt alle `WALK_FRAME_MS` die Zeile, nach `WALK_FRAMES` wieder die erste; `Predictor.moving` nach Schritt mit Richtung `true`; `SnapshotBuffer.moving` enthält eine Figur, deren Position sich zwischen den Snapshots ändert; **manuell** |
| 8 | Stillstand → Standbild, Richtung bleibt | Vitest: `Predictor.moving` nach Schritt mit (0, 0) `false`; `SnapshotBuffer.moving` ohne Positionsänderung leer; `spriteCell(…, false, t)` gleich für alle `t` |
| 9 | graues Blatt, `AWAY_ALPHA`, kein Laufen | Vitest: `greyscale` liefert r = g = b, Alpha gleich; `render`: wartende Figur zieht `sheet(color, true)`, Alpha < 1, Standbild auch bei `moving: true` |
| 10 | `Game.facingArrows = false` | Vitest `game-facing-arrows.test.ts`: nach Welcome + Snapshot keine `fill`-Pfade |
| 11 | `toggleFacingArrows` → `ToggleFacingArrows` → Schalter | Unit (Server): Command ruft `Reply` genau einmal mit `ToggleFacingArrows`; Integration (WS): Absender bekommt die Nachricht; Vitest: nach einer Nachricht Pfeil an allen sichtbaren Figuren, nach der zweiten keiner; `net`: Fall wird weitergereicht |
| 12 | `Reply` nur an den Absender, `World` unverändert | Unit: Positionen, HP, Blickrichtung aller Figuren unverändert; Integration (WS): zweiter Spieler bekommt weder `ToggleFacingArrows` noch `ChatMessage` |
| 13 | nur lokaler Zustand | Vitest: neue `Game`-Instanz zeigt keine Pfeile, auch nach Toggle in einer früheren; Server hat keinen Zustand (Code-Review); **manuell:** Neuladen |
| 14 | Quadrate ohne Blatt | Vitest: `loadPlayerSprites` mit scheiterndem `decode` → `null`, wirft nicht; `render` ohne `sprites` zeichnet 16×16-Rechtecke; **manuell:** Asset im Browser blockieren |
| 15 | ein `drawImage` je Figur, Blätter gecacht | Vitest: `sheet` erzeugt je `(color, away)` nur einmal; **manuell:** 50 laufende Figuren auf dem Testhandy (siehe Risiken) |
| 16 | Register + Test + ADR | Vitest `licenses.test.ts`; Review von ADR 0016 |
| 17 | Original-Blatt, Einfärben nur der Kleidung | Vitest wie AK-2 (Nicht-Kleidung unverändert); `player.png` unverändert aus dem Pack, Herkunft in `LICENSES.md` (Review) |

## Constitution-Check

- [x] **Server ist Autorität:** Die Figur zeigt nur, was Server (Position, Blickrichtung) und Vorhersage liefern.
  Der Command läuft über den Server wie alle aus 015.
- [x] **Typen nur aus `contracts/`:** `ToggleFacingArrows` in Protobuf.
- [x] **Offline-Fortschritt:** nicht betroffen.
- [x] **Keine unnötigen neuen Abhängigkeiten:** keine npm- oder NuGet-Pakete. Neu ist eine Grafik aus Ninja Adventure
  (CC0, Update vom 2026-03-29, älter als 7 Tage); Grund: Spec verlangt das Pack.
- [x] **Spielwerte:** keine neuen. Animationstempo, Größen und Farben sind Darstellung (§8 „nicht gemeint“).
- [x] **KISS:** ein Original-Blatt, eine Farbersetzung, ein Schalter ohne Zustand auf dem Server.

## Risiken / Abweichungen

- **Bestehender Test ändert sich:** `engine/game-facing.test.ts` (016) erwartet Pfeile ohne Zutun. Nach AK-10 sind sie
  aus. Die drei Tests müssen vor dem Zeichnen `ToggleFacingArrows` empfangen; ihre Prüfungen bleiben. Das braucht die
  Freigabe des Users (Frage an den Koordinator).
- **Testhilfe `fakeContext.ts`** bekommt `drawImage`/`fillText`-Ops dazu. Additiv, keine bestehende Prüfung ändert sich.
- **Asset-Download:** Das Pack (89 MB) kommt von itch.io ohne festen Download-Link. Jemand muss es einmal laden; ins Repo
  kommt nur `player.png`.
- **Blatt-Aufbau:** Das Update #8 („Animation V2“) kann den Aufbau geändert haben. Dann nur die Konstanten anpassen;
  gibt es kein kombiniertes Blatt mehr, anhalten und fragen.
- **Palette:** Die eingefärbte Kleidung nutzt die Spielerfarben aus 001, nicht die Palette des Packs (Frage an den
  User).
- **AK-15 braucht 50 laufende Figuren.** Ohne Bots gibt es die nicht mit einem Gerät (Frage an den User).
- **ADR im `impl/`-Branch:** Die Spec verlangt die ADR; sie gehört zu dieser Spec und kommt daher mit T1, nicht über
  `chore/`.
- **Gleichschritt aller Figuren** (Entscheidung 7) – falls störend, später eine Phase je Figur.
