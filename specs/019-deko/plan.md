# 019 – Plan

Gehört zu: [spec.md](spec.md)

> **Vorläufig.** Die Spec hat noch offene Fragen. Der Plan geht von den Empfehlungen unten aus (Variante A) und
> markiert abhängige Stellen mit **⚠ F-n**. Ändert eine Antwort die Variante, wird der Plan vor `bereit` angepasst.

## Überblick

```
Client  ── client/src/assets/                 nature.png (Tileset mit Bäumen, Büschen, Steinen aus Ninja Adventure) + LICENSES.md
        ── features/game/world/decorations.ts  reine Regeln: welche Objekte wo stehen (nutzt hash2 und groundAt aus 018)
        ── render/render.ts                    Objekte und Figuren nach Fußlinie sortiert; Namen obenauf
        ── engine/game.ts, app/App.tsx         Tileset laden und übergeben
Server  ── keine Änderung (⚠ F-1, F-3)
```

Deko ist wie der Boden (018) reine Darstellung: Man läuft hindurch (⚠ F-1), die Positionen folgen aus festen Regeln.
Alle Clients derselben Version sehen dieselben Objekte an denselben Stellen (AK-1).

**Voraussetzung:** 018 (`world/hash.ts`, `world/ground.ts`, `TILE_SIZE`) und 017 (`RenderOptions`, `loadImage`,
Register).

## Offene Fragen der Spec und Empfehlung

| | Frage (spec.md) | Empfehlung | Begründung |
|---|---|---|---|
| F-1 | Durchlaufen oder Kollision? | **Durchlaufen** im ersten Lauf; Kollision als eigene Spec | Kollision braucht Server-Logik, Vertrag und Vorhersage im Client |
| F-2 | Verdeckt ein Objekt eine Figur dahinter? | **Ja**, Zeichnen nach Fußlinie; Namen bleiben obenauf | Billig (eine Sortierung) und wirkt räumlich; ohne Kollision sonst unnatürlich |
| F-3 | Wer legt die Positionen fest? | **Regeln im Client** wie beim Boden (018 F-1) | Kein Editor, keine Daten, gleiche Lage für alle; Karte/Editor später mit Kollision |
| F-4 | Animierte Deko im ersten Lauf? | **Nein** | Out of Scope sagt schon „offen“; spart Animationslogik |

## Konstanten

Reine Darstellung, Client. Keine Spielwerte, solange Objekte nichts blockieren (⚠ F-1).

| Name | Wert | Wo | Bedeutung |
|---|---|---|---|
| `DECO_CHUNK_TILES` | 8 | `core/constants.ts` | Objekte werden je Block von 8×8 Kacheln (128×128 Weltpixel) bestimmt |
| `DECO_MAX_PER_CHUNK` | 3 | `core/constants.ts` | höchstens so viele Objekte je Block |
| `DECO_CLEARANCE_TILES` | 1 | `core/constants.ts` | Abstand zu Weg und Platz (018), damit Wege frei bleiben |
| `DECO_CULL_MARGIN` | 48 | `core/constants.ts` | Rand fürs Culling (größtes Objekt ragt so weit über seinen Fußpunkt) |
| `DECORATION_KINDS` | aus dem Tileset | `world/decorations.ts` | je Art (`tree`, `bush`, `rock`): Quellrechteck `sx, sy, w, h` und Gewicht |

## Entschiedene Fragen (Plan)

1. **Objekt** = `{ kind, tx, ty }`, Fußpunkt unten mittig in Kachel `(tx, ty)`: Welt `(tx·16 + 8, ty·16 + 16)`. Das
   Bild wird mit seiner Unterkante auf den Fußpunkt gesetzt, waagrecht mittig, auf ganze Weltpixel gerundet.
2. **Regeln** `decorationsInChunk(cx, cy)`: Anzahl `hash2(cx, cy) mod (DECO_MAX_PER_CHUNK + 1)`. Für jedes Objekt
   `i` liefern weitere `hash2`-Werte die Kachel im Block und die Art (nach Gewicht). Verworfen wird eine Kachel, die
   im Umkreis `DECO_CLEARANCE_TILES` einen Weg oder Platz hat (`groundAt` aus 018), und eine Kachel, die im Block
   schon belegt ist. Rein, ohne Zustand.
3. **Sichtbare Objekte:** `decorationsInView(origin, viewWidth, viewHeight)` sammelt alle Blöcke, die das Sichtfeld
   plus `DECO_CULL_MARGIN` schneiden (bei 933×400 höchstens etwa 10×6 Blöcke).
4. **Reihenfolge** (⚠ F-2): Boden → Objekte und Figuren gemeinsam nach Fußlinie aufsteigend (Figur: `y + 8`, Objekt:
   Fußpunkt `y`; bei Gleichstand Objekt zuerst) → Pfeile mit ihrer Figur → **alle Namen zuletzt**. Ohne Verdecken
   (F-2 = nein) entfällt die Sortierung: Objekte vor allen Figuren.
5. **Scharf** (AK-2): ganzzahlige Quell- und Zielkoordinaten, keine Glättung (wie 017/018).
6. **Rückfall:** Ohne geladenes Tileset werden keine Objekte gezeichnet; das Spiel läuft.
7. **Gleiche Stellen für alle** (AK-1): Die Regeln hängen nur von Kachel- und Blockkoordinaten ab, nicht von Zeit,
   Spieler oder Zufall.

## Verträge

Keine Änderung (⚠ F-1, F-3). Bei Kollision oder Karte vom Server: Objekte als Weltdaten in der `World`, an den Client
im `Welcome` (nächste freie Nummer dort ist 9; abstimmen mit dem dann aktuellen Stand).

## Server

Keine Änderung (⚠ F-1, F-3).

## Client

### Assets
- `client/src/assets/nature.png`: Tileset mit Bäumen, Büschen und Steinen aus Ninja Adventure (z. B.
  `TilesetNature.png`), unverändert; Zeile in `LICENSES.md`.

### `world/decorations.ts` (neu, rein)
- `type DecorationKind = "tree" | "bush" | "rock"`; `interface Decoration { kind; tx; ty }`.
- `decorationsInChunk(cx, cy): Decoration[]` (Entscheidung 2), `decorationsInView(...)` (Entscheidung 3).
- `DECORATION_KINDS` (Quellrechteck und Gewicht je Art, am Tileset bestimmt).

### `render/render.ts`
- `RenderOptions.decorations?: CanvasImageSource | null`.
- Zeichenreihenfolge nach Entscheidung 4. Die Schleife über Figuren wird zu einer Schleife über eine nach Fußlinie
  sortierte Liste aus Figuren und Objekten; Namen in einer zweiten Schleife danach. Was 013/014 an Zielen zeichnen,
  sortiert sich als Figur mit ein (gleiche Fußlinie `y + 8`), ihre HP-Balken kommen mit den Namen zuletzt.

### `engine/game.ts`, `app/App.tsx`
- `Game.setDecorationTiles(image: CanvasImageSource | null)`; an `render` als `decorations`.
- `App`: laden wie das Boden-Tileset (018); Fehler → keine Objekte.

## Abdeckung der Akzeptanzkriterien

| AK | Wie erfüllt | Wie getestet |
|---|---|---|
| 1 | feste Regeln je Block | Vitest `decorations.test.ts`: zwei Aufrufe für denselben Block liefern dasselbe; höchstens `DECO_MAX_PER_CHUNK` je Block; kein Objekt auf oder neben Weg und Platz; keine doppelte Kachel; über 1 000 Blöcke kommen alle Arten vor; `decorationsInView` enthält genau die Objekte der geschnittenen Blöcke; Vitest `render-deco.test.ts`: Objekte erscheinen als `image`-Ops an den erwarteten Stellen; **manuell:** zwei Geräte am selben Ort |
| 2 | ganzzahlig, keine Glättung | Vitest: Ziel- und Quellkoordinaten aller Objekt-Ops ganzzahlig; Reihenfolge: Figur hinter einem Baum (kleinere Fußlinie) vor dem Baum gezeichnet, Figur davor danach, alle Namen nach allen Bildern (⚠ F-2); **manuell:** PC und Handy |

## Constitution-Check

- [x] **Server ist Autorität:** Objekte beeinflussen den Spielstand nicht (⚠ F-1).
- [x] **Typen nur aus `contracts/`:** keine neuen Typen über das Netz.
- [x] **Offline-Fortschritt:** nicht betroffen.
- [x] **Keine unnötigen neuen Abhängigkeiten:** kein Paket; ein Tileset aus dem Pack aus 017 (CC0, älter als 7 Tage).
- [x] **Spielwerte:** keine, solange Objekte nichts blockieren.
- [x] **KISS:** Regeln statt Karte, eine Sortierung.

## Risiken / Abweichungen

- **Kollision später:** Kommt sie (eigene Spec), wandern die Regeln oder eine Karte auf den Server. Die Regel-Funktion
  im Client wird dann durch Daten vom Server ersetzt; `render.ts` bleibt.
- **Markante Objekte:** Zufällig gestreute Objekte helfen der Orientierung nur begrenzt (User Story 2). Feste
  Landmarken (z. B. ein großer Baum am Platz) wären ein kleiner Zusatz in `decorationsInChunk(0, 0)` – nur, wenn
  gewünscht.
- **Bestehende Tests:** Namen wandern in eine zweite Schleife. Die Tests aus 016/017 prüfen Pfeil und Figur, nicht die
  Reihenfolge der Namen; sie bleiben unverändert.
- **Ziele aus 013/014** müssen mit in die Sortierung. Ist `render.ts` bis dahin anders aufgebaut, wird nur die
  Sortierung angepasst, nicht deren Zeichnen.
