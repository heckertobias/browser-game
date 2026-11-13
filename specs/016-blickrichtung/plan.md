# 016 – Plan

Gehört zu: [spec.md](spec.md)

## Überblick

```
Server  ── World.Step(): Player.Facing aus der angewandten Laufrichtung (Winkel), unverändert bei Stillstand
        ── GameLoop: PlayerState.facing im Snapshot
Client  ── prediction.ts: eigene Blickrichtung wie die Position vorhersagen und abgleichen
        ── interpolation.ts: Blickrichtung anderer Figuren über den kürzeren Bogen interpolieren
        ── render.ts: kleiner Pfeil an jeder Figur
```

## Konstanten

| Name | Wert | Wo | Bedeutung |
|---|---|---|---|
| `DefaultFacing` | π/2 (nach unten, „zur Kamera“) | Server `GameSettings` | Standard-Blickrichtung (AK-4) |
| `MinFacingInput` | 0,01 | Server `GameSettings` → `World`; Client aus `Welcome` | kürzere Richtungsvektoren ändern die Blickrichtung nicht (AK-5) |
| `ARROW_GAP` | 3 | Client `constants.ts` | Abstand des Pfeils vom Rand der Figur (Weltpixel) |
| `ARROW_LENGTH` | 5 | Client `constants.ts` | Länge der Pfeilspitze |
| `ARROW_WIDTH` | 6 | Client `constants.ts` | Breite der Pfeilbasis |

Winkel in Bogenmaß, 0 = rechts (+x), π/2 = unten (+y, Bildschirm-Koordinaten wie die Welt).

## Entschiedene Fragen (Plan)

1. **Quelle ist die vom Server angewandte Richtung** (`Player.DirX/DirY` nach Eingabe oder Nachlauf in `World.Step()`).
   Ist ihre Länge mindestens `MinFacingInput`, wird `Facing = atan2(DirY, DirX)` (AK-1, AK-2, AK-12). Sonst bleibt
   der alte Wert (AK-3, AK-5). Die Totzone des Zeigers aus 001 liefert ohnehin (0, 0); `MinFacingInput` fängt
   zusätzlich winzige Vektoren eines manipulierten Clients ab.
2. **Neue Figur → `DefaultFacing`** (AK-4, AK-11). Wie bei HP/Mana (012) gilt das für jede neue Figur; die Übernahme
   einer wartenden Figur behält ihre Blickrichtung. `RestartInputs` setzt die Richtung auf 0, nicht die Blickrichtung.
3. **Respawn und Zurücksetzen (012, 015)** ändern die Blickrichtung nicht. Die Spec verlangt nichts dafür; KISS.
4. **Eigene Figur vorhersagen:** Der Server ist Autorität (AK-12), aber die eigene Blickrichtung käme sonst erst nach
   einem RTT. `Predictor` behandelt sie wie die Position: Bei jedem Schritt aus der eigenen Eingabe setzen, beim
   Abgleich den Server-Wert übernehmen und die offenen Eingaben erneut anwenden. Eine Blickrichtungs-Eingabe wird nicht
   geschickt.
5. **Pfeil:** gefülltes Dreieck außerhalb der Figur in Blickrichtung (Abstand `PLAYER_SIZE/2 + ARROW_GAP` vom
   Mittelpunkt), weiß mit 1 px dunklem Rand, damit es auf Figur und Hintergrund erkennbar ist (AK-6). Wartende Figuren
   (`away`) zeigen ihn mit derselben Transparenz wie die Figur. Gezeichnet wird mit unrundetem Winkel (stufenlos, AK-7);
   Pixel-Snapping betrifft nur die Position der Figur.

## Verträge

**Protobuf** (`contracts/proto/browsergame/v1/game.proto`), abwärtskompatibel. Feldnummern sind über die Pläne 002 und
011–016 abgestimmt: `PlayerState` 7 = `spawn_seq` (012), 8 = `facing` (016); `Welcome` 6–7 = Max-Werte (012),
8 = `min_facing_input` (016). Der Client bekommt den Schwellwert vom Server (Constitution §8).

```proto
message Welcome {
  // … 1–7 wie bisher
  // Kürzere Richtungsvektoren ändern die Blickrichtung nicht (016).
  float min_facing_input = 8;
}

message PlayerState {
  // … 1–6 wie bisher, 7: spawn_seq (012)
  // Blickrichtung in Bogenmaß, 0 = +x, π/2 = +y (unten). Folgt der Laufrichtung (016).
  float facing = 8;
}
```

**OpenAPI:** keine Änderung.

## Server

- `Player.Facing` (`double`); `World`-Konstruktor bekommt `defaultFacing` und `minFacingInput` (Constitution §8).
- `World.Step()`: nach dem Bestimmen von `DirX/DirY` (vor der Bewegung):
  `if (hypot(DirX, DirY) >= MinFacingInput) Facing = atan2(DirY, DirX)`. Wartende Figuren werden übersprungen wie bisher.
- `GameLoop.BroadcastSnapshot`: `Facing = (float)p.Facing`.
- Kein Speichern (AK-11): `PositionStore` bleibt unverändert.
- 013 liest `Player.Facing` für Angriffe, 014 für den Spawn-Punkt (AK-10); in 016 selbst gibt es keine weitere
  Verwendung (AK-9).

## Client

- **`core/vector.ts`**: `facingOf(dir, current, minInput)` (gleiche Regel wie der Server, Schwellwert aus `Welcome`) und `lerpAngle(a, b, t)` über den
  kürzeren Bogen.
- **`prediction.ts`**: `facing` (Start: Wert aus dem ersten Snapshot), `step` setzt ihn per `facingOf`, `reconcile`
  übernimmt den Server-Wert und wendet die offenen Eingaben erneut an.
- **`interpolation.ts`**: `RemotePlayer.facing`, interpoliert mit `lerpAngle`. Nach einem Sprung (`spawnSeq`, 012) ohne
  Zwischenwerte.
- **`render.ts`**: `DrawablePlayer.facing`; `arrowPoints(x, y, facing)` (reine Funktion) und Zeichnen des Dreiecks nach
  dem Quadrat der Figur. Konstanten in `constants.ts`.
- **`game.ts`**: eigene Figur mit `predictor.facing`.

## Abdeckung der Akzeptanzkriterien

| AK | Wie erfüllt | Wie getestet |
|---|---|---|
| 1 | `atan2` der angewandten Richtung | Unit: Richtung (1, 0) → 0, (0, −1) → −π/2, Zeiger-Richtung (0,6; 0,8) → atan2 exakt |
| 2 | freier Winkel | Unit: 37° rein → 37° raus (auf float genau); Vitest: `facingOf` gleiches Ergebnis wie Server-Tabelle |
| 3 | Stillstand behält | Unit: Richtung → (0, 0) nach Nachlauf → Blickrichtung unverändert |
| 4 | `DefaultFacing` | Unit: neue Figur → π/2; Integration (WS): erster Snapshot `facing` = π/2 |
| 5 | `MinFacingInput` | Unit: (0,005; 0) ändert nicht; Vitest: `facingOf` gleich |
| 6 | Pfeil | Vitest: `arrowPoints` liegt außerhalb der Figur in Blickrichtung; **manuell:** sichtbar auf Hintergrund und Raster |
| 7 | unrundeter Winkel, `lerpAngle` | Vitest: `lerpAngle` über ±π (z. B. 170° → −170° läuft über 180°); **manuell:** Kreis laufen mit Zeiger |
| 8 | `facing` für alle im Snapshot | Integration (WS): zwei Spieler, einer läuft nach links → der andere sieht `facing` = π |
| 9 | keine Verwendung in Tempo/Kampf | Unit: Schritt-Strecke unabhängig von `Facing`; 013-Tests bleiben unverändert |
| 10 | `Player.Facing` lesbar | wird in 014 (AK-1) getestet |
| 11 | neue Figur → Standard, kein Speichern | Integration: laufen, abmelden, anmelden → π/2 |
| 12 | Server leitet ab, keine Client-Eingabe | Vertrag hat kein Blickrichtungs-Feld in `Input`; Integration: Snapshot-Wert folgt nur der Eingabe-Richtung |

## Constitution-Check

- [x] **Server ist Autorität:** Der Server leitet die Blickrichtung aus der Bewegung ab; der Client sagt sie nur für
  die eigene Anzeige voraus und übernimmt den Server-Wert.
- [x] **Typen nur aus `contracts/`:** ein Feld in Protobuf.
- [x] **Offline-Fortschritt:** nicht betroffen.
- [x] **Keine neuen Abhängigkeiten.**
- [x] **KISS:** ein Winkel pro Figur, ein Dreieck im Rendering.

## Risiken / Abweichungen

- **Feldnummer 7 gehört 012:** Kommt 016 vor 012, bleibt 7 frei und `facing` ist trotzdem 8.
- **Kleiner Winkel-Sprung bei Ankunft am Zeiger:** Beim Halten-und-Folgen dreht die Figur kurz vor dem Ziel mit, wenn
  sie über den Zeiger hinausschießt (letzter Schritt). Die Totzone aus 001 verhindert das meist; sonst ist es ein
  einzelner Frame.
