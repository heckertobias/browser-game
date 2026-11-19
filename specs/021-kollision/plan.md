# 021 – Plan

Gehört zu: [spec.md](spec.md)

## Überblick

```
Vertrag ── game.proto: AreaMap 8–9 (was blockiert), Welcome 11–13 (Hitbox), AttackRejected.Reason 4 (keine freie Sicht)
Server  ── Map/Domain/Obstacles.cs        blockierte Kacheln (Bewegung, Sicht) aus Karte + Regeln, Rand geklemmt
        ── Map/Domain/Collision.cs        Hitbox bewegen mit Gleiten; Überlappung; Sichtlinie
        ── Domain/World.cs                Bewegung über Collision.Move; Startposition prüfen
        ── Application/GameLoop.cs        ungültige gespeicherte Position → Startpunkt; Regeln und Hitbox im Welcome
        ── Combat/Domain/Combat.cs        Fernkampf nur mit freier Sicht; Gegner erscheinen nicht im Hindernis
        ── Application/GameSettings.cs    Hitbox, blockierende Bodenarten und Objektarten
Client  ── features/game/world/collision.ts  dieselbe Bewegung mit Gleiten für die Vorhersage
        ── state/prediction.ts            Vorhersage mit Kollision
        ── combat/AttackHint.tsx          Hinweis „Keine freie Sicht“
```

Der Server entscheidet über Bewegung und Sicht (Constitution §2). Der Client sagt seine eigene Bewegung mit genau
derselben Rechnung voraus, damit die Figur an einer Wand nicht ruckelt; Abweichungen gleicht der Snapshot aus wie
bisher.

**Voraussetzung:** 018 (`AreaMap`, `KindAt` mit Klemmen, Karte im `Welcome`), 019 (Objekte in der Karte), 013
(`Combat.TryAttack`, `AttackRejected`, Hinweise), 014 (`Combat.SpawnEnemy`), 015 (`resetPosition` nutzt die
Startposition).

## Konstanten

Spielwerte im Sinn von §8: zentral in `GameSettings`, an `World` und `Combat` übergeben, an den Client im `Welcome`.

| Name | Wert | Wo | Bedeutung |
|---|---|---|---|
| `HitboxWidth` | 12 | `GameSettings` → `World`, `Combat`; Client aus `Welcome` | Breite der Kollisionsbox einer Figur (Spieler, Gegner) |
| `HitboxHeight` | 8 | ebenso | Höhe der Box: nur die Füße, damit man dicht vor Hindernissen und hinter Baumkronen stehen kann |
| `HitboxOffsetY` | 4 | ebenso | Mitte der Box unter der Position; Box = `[x − 6, x + 6) × [y, y + 8)`, die untere Hälfte des 16er-Körpers |
| `BlockingGround` | Wasser, Berg | `GameSettings` → `Obstacles`; Client aus `AreaMap` | Bodenarten, die man nicht betreten kann (AK-1) |
| `SightBlockingGround` | Berg | `GameSettings` → `Obstacles` | Bodenarten, über die man nicht zaubern kann (AK-7; Wasser nicht) |
| `BlockingObjects` | `tree`, `bush`, `rock` | `GameSettings` → `Obstacles`; Client aus `AreaMap` | Objektarten, die ihre Kachel für Bewegung und Sicht sperren (AK-5, AK-7); `tallgrass` nicht |

## Entschiedene Fragen (Plan)

1. **Kachelraster statt Formen.** Ein Hindernis ist immer eine ganze Kachel: Boden der Art aus `BlockingGround` oder
   die Kachel eines Objekts aus `BlockingObjects` (beim Baum der Stamm, die Krone darüber sperrt nicht). `Obstacles`
   rechnet das einmal beim Laden in zwei Bool-Raster (Bewegung, Sicht). Abfragen außerhalb der Karte klemmen wie in 018:
   Jenseits des Rands liegt Berg, also kommt man nicht hinaus (AK-2).
2. **Bewegen mit Gleiten** (AK-1, AK-3), getrennt je Achse, erst x, dann y:
   - Neues `x = x + dx`. Überlappt die Box an der neuen Stelle eine Bewegungs-Kachel, die sie an der alten Stelle
     nicht überlappt hat, rückt sie bis an deren Kante: nach rechts `x = Spalte·16 − HitboxWidth/2`, nach links
     `x = (Spalte + 1)·16 + HitboxWidth/2` (nächstgelegene Spalte in Laufrichtung).
   - Dann dasselbe für y mit dem neuen x.
   - Boxen sind halb offen (`[l, r)`): Bündig an einer Kante zu stehen ist keine Überlappung, ein Epsilon braucht es
     nicht.
   - Schräg gegen eine Wand wird nur die gesperrte Achse gekappt; die andere läuft weiter. Das ist das Gleiten.
   - Kacheln, die die Box schon an der alten Stelle überlappt, sperren nicht. So kommt eine Figur, die doch einmal in
     einem Hindernis steht (z. B. nach einer Kartenänderung), heraus und bleibt nicht stecken.
   - Voraussetzung: Ein Schritt ist kürzer als eine Kachel (`Speed / TickRateHz` = 10 < 16), sonst könnte die Box eine
     Kachel überspringen. `World` prüft das im Konstruktor.
3. **Gleiche Rechnung im Client.** `world/collision.ts` ist eine Zeile-für-Zeile-Übertragung von `Collision.Move` (beide
   rechnen in `double`, gleiche Reihenfolge der Operationen). Eine gemeinsame Fixture-Datei mit Fällen (Start,
   Richtung, Karte, erwartetes Ergebnis) prüft beide Seiten; ein Unterschied fällt im Test auf, nicht im Spiel.
4. **Ungültige gespeicherte Position** (AK-4): Beim Betreten prüft der `GameLoop` die gespeicherte Position mit
   `Collision.Overlaps`. Überlappt die Box ein Hindernis (auch jenseits des Rands, dort ist Berg), setzt er die Figur
   wie eine neue an eine zufällige Startposition. Die neue Position wird mit dem nächsten regulären Speichern gesichert.
   Eine Figur, die noch in der `World` wartet (003 AK-20), wird nicht geprüft; sie stand schon unter denselben Regeln.
5. **Startbereich frei:** Startposition, Respawn (012) und `resetPosition` (015) nutzen weiter den `SpawnRadius` um
   (0, 0). Der Test der echten Karte stellt sicher, dass dort kein Hindernis liegt (019 hält Objekte schon
   `START_CLEARANCE` entfernt). Darum braucht die Startposition keine Schleife „neu würfeln, bis frei“.
6. **Gegner** (AK-6): Gegner aus 014 bewegen sich nicht. Die Hindernisse gelten für sie beim Erscheinen:
   `SpawnEnemy` rückt den Punkt vor dem Spieler in Schritten von 1 Weltpixel auf den Spieler zu, bis die Box frei ist;
   schlimmstenfalls erscheint der Gegner auf dem Spieler. Bewegen sich Gegner später, nutzen sie `Collision.Move` wie
   Spieler; diese Spec legt dafür die gemeinsame Funktion an.
7. **Sichtlinie** (AK-7): Von der Position des Angreifers zur Position des Ziels werden alle Kacheln auf der Strecke
   der Reihe nach besucht (Grid-Traversal nach Amanatides/Woo). Ist eine davon im Sicht-Raster gesperrt, gibt es keine
   freie Sicht. Geprüft wird in `TryAttack` nur beim Fernkampf, nach Reichweite und Blickrichtung und **vor** dem
   Mana: Ein Fehlversuch kostet nichts und startet keine Abklingzeit (013 AK-12).
8. **Projektile prüfen nichts mehr** (AK-8): Der Flug aus 013 bleibt unverändert und kennt die Karte nicht.
9. **Nahkampf** braucht keine Sichtlinie (Spec nennt nur den Fernkampf). Durch einen einzelnen Baumstamm kann man auf
   kurze Distanz zuschlagen; das bleibt bewusst so (Risiken).
10. **001 AK-7** („keine Grenze“) gilt ab 021 nur noch innerhalb des Gebiets. Die Spec 001 bekommt einen Hinweis auf
    021; ihr Text bleibt sonst, wie er zur Umsetzung war.

## Verträge

**Protobuf** (`contracts/proto/browsergame/v1/game.proto`), abwärtskompatibel. Freie Nummern: `AreaMap` 8 ff. (1–6 aus
018, 7 aus 019), `Welcome` 11 ff. (10 aus 018), `AttackRejected.Reason` 4.

```proto
message AreaMap {
  // … 1–7
  // Was die Bewegung sperrt (021). Die Sicht prüft nur der Server.
  repeated GroundKind blocking_ground = 8;
  repeated string blocking_objects = 9;
}

message Welcome {
  // … 1–10
  // Kollisionsbox einer Figur in Weltpixeln (021): Breite, Höhe, Mitte unterhalb der Position.
  float hitbox_width = 11;
  float hitbox_height = 12;
  float hitbox_offset_y = 13;
}

message AttackRejected {
  enum Reason {
    // … 0–3
    // Berg oder blockierendes Objekt zwischen Angreifer und Ziel (021 AK-7).
    REASON_NO_LINE_OF_SIGHT = 4;
  }
  Reason reason = 1;
}
```

Die Regeln gehen mit der Karte an den Client, nicht als Kopie im Client-Code (§8). Die Sicht-Regeln braucht der Client
nicht; er sagt Angriffe nicht voraus.

**OpenAPI:** keine Änderung.

## Server

- `Map/Domain/Obstacles.cs` (neu, rein): `Obstacles(AreaMap map, IReadOnlySet<GroundKind> blockingGround,
  IReadOnlySet<GroundKind> sightBlockingGround, IReadOnlySet<string> blockingObjects)`;
  `BlocksMovement(tx, ty)`, `BlocksSight(tx, ty)` (Entscheidung 1). `Obstacles.None` für Tests ohne Karte.
- `Map/Domain/Hitbox.cs`: `record Hitbox(double Width, double Height, double OffsetY)`.
- `Map/Domain/Collision.cs` (neu, rein, statisch): `Move(Obstacles, Hitbox, x, y, dx, dy) → (x, y)` (Entscheidung 2),
  `Overlaps(Obstacles, Hitbox, x, y)`, `HasLineOfSight(Obstacles, x0, y0, x1, y1)` (Entscheidung 7).
- `World`: neue Konstruktorparameter `Obstacles obstacles` und `Hitbox hitbox` (Standard: `Obstacles.None`, damit
  bestehende Tests unverändert bleiben); `Step` bewegt mit `Collision.Move`; Prüfung „Schritt < Kachel“ (Entscheidung
  2); `IsFree(x, y)` für den `GameLoop`.
- `GameLoop.AddOrTakeOver`: gespeicherte Position nur, wenn `World.IsFree` (Entscheidung 4); `Welcome` mit Hitbox,
  `AreaMap` mit `blocking_ground` und `blocking_objects`.
- `Combat`: bekommt `Obstacles` und `Hitbox`; `TryAttack` → `AttackResult.NoLineOfSight` (Entscheidung 7) → im
  `GameLoop` `AttackRejected(REASON_NO_LINE_OF_SIGHT)`; `SpawnEnemy` nach Entscheidung 6.
- `GameSettings`: Werte aus der Tabelle; `GameSetup` baut `Obstacles` aus der geladenen Karte.

## Client

### `world/collision.ts` (neu, rein)
- `interface Obstacles { blocksMovement(tx, ty): boolean }`; `obstaclesFromMap(map, blockingGround, blockingObjects)`
  (Raster einmal beim `Welcome`, Rand geklemmt wie `kindAt`).
- `moveWithCollision(obstacles, hitbox, x, y, dx, dy): Vec` – Übertragung von `Collision.Move`.

### `state/prediction.ts`
- `Predictor` bekommt `obstacles` und `hitbox` (aus dem `Welcome`; fehlen sie, wie bisher ohne Kollision) und bewegt in
  `apply` mit `moveWithCollision`. Blickrichtung wie bisher aus der Eingabe, auch wenn die Bewegung gesperrt ist
  (016: Blick folgt der gewünschten Laufrichtung, der Server macht es genauso).

### `combat/AttackHint.tsx`, `localization/i18n.ts`
- `REASON_NO_LINE_OF_SIGHT` → Hinweis „Keine freie Sicht“ / „No line of sight“, wie die übrigen Gründe aus 013.

## Abdeckung der Akzeptanzkriterien

| AK | Wie erfüllt | Wie getestet |
|---|---|---|
| 1 | `Collision.Move` gegen Wasser und Berg | xUnit `CollisionTests`: gegen Wasser nach rechts, links, oben, unten → Box endet bündig an der Kante; zehn weitere Ticks → Position gleich; xUnit `WorldTests`: Spieler läuft in den See → bleibt am Ufer; Fixture-Fälle in xUnit und Vitest `collision.test.ts` gleich; **manuell** |
| 2 | Rand geklemmt = Berg | xUnit `ObstaclesTests`: Abfragen jenseits aller Seiten sperren; `WorldTests`: 1 000 Ticks Richtung Kartenrand → Position innerhalb der Karte vor dem Bergrand; **manuell** |
| 3 | Achsen getrennt | xUnit und Vitest (Fixture): schräg gegen eine senkrechte Wand → x gekappt, y läuft weiter; gegen eine Ecke → gleitet an der Seite entlang, die frei ist; Vitest `prediction-collision.test.ts`: Vorhersage = Server-Rechnung, kein Rücksprung beim Abgleich; **manuell** |
| 4 | Prüfung beim Betreten | Integration (WS): gespeicherte Position im See, im Berg und weit außerhalb der Karte → Figur im `SpawnRadius` um (0, 0), danach gespeichert; gültige gespeicherte Position bleibt; wartende Figur wird übernommen, ohne Prüfung |
| 5 | Objekte aus `BlockingObjects` sperren ihre Kachel | xUnit `ObstaclesTests`: Baum, Busch, Stein sperren Bewegung und Sicht; hohes Gras nicht; `CollisionTests`: Figur läuft gegen einen Baumstamm → stoppt; durch hohes Gras → läuft weiter; Vitest: `obstaclesFromMap` aus einem `AreaMap` mit Objekten; **manuell** |
| 6 | Gegner erscheinen frei; gemeinsame Funktion | xUnit `CombatTests`: `SpawnEnemy` mit Wasser vor dem Spieler → Gegner auf dem nächsten freien Punkt Richtung Spieler, nie im Hindernis; mit Wand direkt vor dem Spieler → auf dem Spieler; **manuell** |
| 7 | Sichtlinie vor dem Mana | xUnit `CollisionTests`: `HasLineOfSight` frei, über Wasser frei, durch Berg und Baum gesperrt, waagrecht, senkrecht, schräg; `CombatTests`: Fernkampf durch einen Baum → `NoLineOfSight`, Mana und `NextAttackTick` unverändert; Nahkampf daneben unverändert; Integration (WS): `AttackRejected(NO_LINE_OF_SIGHT)` nur an den Angreifer; Vitest `AttackHint`: Text für den neuen Grund; **manuell** |
| 8 | Flug unverändert | xUnit `CombatTests`: Projektil starten, danach im Test-`Obstacles` die Linie sperren → schlägt trotzdem ein; bestehende Projektiltests aus 013 grün |

## Constitution-Check

- [x] **Server ist Autorität:** Bewegung, Sicht und Startposition rechnet der Server; der Client sagt nur seine eigene
  Bewegung voraus und übernimmt den Snapshot.
- [x] **Typen nur aus `contracts/`:** Regeln, Hitbox und neuer Grund aus dem Proto.
- [x] **Offline-Fortschritt:** nicht betroffen.
- [x] **Keine unnötigen neuen Abhängigkeiten:** keine.
- [x] **Spielwerte:** Hitbox und blockierende Arten zentral in `GameSettings`, an `World`, `Combat` und den Client
  übergeben.
- [x] **KISS:** ganze Kacheln statt Formen, zwei Bool-Raster, Achsen getrennt.

## Risiken / Abweichungen

- **AK-6 ohne bewegte Gegner:** Gegner aus 014 stehen still. AK-6 ist damit beim Erscheinen erfüllt (Entscheidung 6);
  sobald eine Spec Gegner bewegt, nutzt sie `Collision.Move`.
- **Gleichheit von Client und Server:** Weicht die Übertragung ab, ruckelt die eigene Figur an Wänden (Abgleich).
  Die gemeinsame Fixture fängt das in den Tests ab.
- **Ganze Kacheln wirken grob:** Ein Busch sperrt seine ganze Kachel, auch wenn das Bild kleiner ist. Für Steine und
  Büsche passt das; feinere Formen kämen als Rechteck je Objektart im Atlas oder in den Regeln.
- **Nahkampf durch Baumstämme** (Entscheidung 9) ist möglich. Stört es, bekommt der Nahkampf dieselbe Sichtprüfung
  (eine Zeile, Spec-Änderung nötig).
- **Kartenänderungen** können Figuren in neue Hindernisse setzen. Sie laufen heraus (Entscheidung 2); beim nächsten
  Betreten setzt AK-4 sie an den Start.
