# ADR 0018 – Sprite-Format: Figuren aus Teilen

**Status:** akzeptiert (2026-11-27)

## Kontext

Ab [017](../../specs/017-spielfigur/spec.md) zeigt das Spiel Pixel-Art statt farbiger Quadrate. Die Grafiken stammen
aus dem Asset-Pack Ninja Adventure (CC0, [ADR 0017](0017-grafiken-nur-cc0.md)). Das Pack liefert je Figur ein fertiges
Bild mit festem Aufbau. Später sollen Kleidung, Frisuren und Waffen tauschbar sein. Mit fertigen Bildern wäre jeder
Teiletausch ein Umbau, und Code, der den Aufbau des Packs kennt, bricht mit jeder neuen Quelle. Boden
([018](../../specs/018-boden/spec.md)) und Deko ([019](../../specs/019-deko/spec.md)) haben dieselbe Frage.

## Entscheidung

Der Code kennt nie den Aufbau des Packs, nur ein eigenes Format. Das Pack ist die Quelle: Ein Import-Skript
(`client/scripts/import-sprites.mjs`) überführt Grafiken einmal ins Format. Die Dateien des Packs kommen nicht ins
Repo. Die Dateien im Format sind danach die Wahrheit und dürfen von Hand weitergezeichnet werden.

Das Format hat zwei Formen:

- **Rig** für Figuren (017), unten beschrieben.
- **Atlas** für Boden und Deko (018, 019): ein Bild plus Beschreibung mit benannten Einträgen. Der Code nennt Einträge
  beim Namen, nie bei ihrer Lage im Bild. Die Einzelheiten legt 018 fest.

### Rig

**Begriffe**

- **Rig** = ein Figurentyp (z. B. `humanoid`): ein Ordner `client/src/assets/sprites/<rig>/` mit einer Beschreibung
  `rig.json` und den Bildern seiner Teile.
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
2. **Zelle größer als der Körper.** Der Körper ist 16×16 und liegt in der Mitte der Zelle. Die Zelle ist 48×48, also
   3×3 Kacheln: rundum eine ganze Kachel Platz für Teile, die hinausragen (Waffe in der Hand, Hut).
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
ändern): Zeichenreihenfolge je Richtung (Waffe hinter dem Körper beim Blick nach oben), Ankerpunkte für Waffen, Teile
bei Bedarf nachladen, Aussehen vom Server (Vertrag), weitere Rigs (Gegner).

### Wo zusammengesetzt wird

Zuerst setzt der Client zusammen. Zeigt sich das als Problem (zu viele Teile zum Laden, Figuren außerhalb des
Spiels), setzt der Server je Aussehen einmal zusammen, speichert das fertige Blatt und liefert es per HTTP aus; der
Client cacht die Datei. Damit dieser Weg offen bleibt, gilt:

- Das Format bleibt reine Daten (`rig.json`, PNGs) ohne Annahmen über den Browser.
- Zusammensetzen und Einfärben sind reine Funktionen über Pixel, nach den Regeln 6 und 7 genau festgelegt. Ein
  zweiter Nachbau in C# liefert dasselbe Bild.
- Zeichnen und Zusammensetzen sind getrennt: Das Zeichnen bekommt ein fertiges Blatt und weiß nicht, woher es kommt.
- Ein Aussehen hat einen stabilen Schlüssel, der später als Dateiname dienen kann.

Nicht in Frage kommt, dass ein Client sein fertiges Blatt hochlädt: Der Server würde ihm ein Bild glauben, das alle
sehen ([Constitution](../../specs/constitution.md) §2).

### Code-Grenze

Soll eine Figur später auf einer Webseite erscheinen (Profil, Startseite), wird der Code unter
`client/src/features/game/sprites/` als eigenes Paket herausgelöst und dort genauso eingebunden wie im Spiel. Das ist
der bevorzugte Weg für alles, was im Browser läuft; das Zusammensetzen auf dem Server bleibt für Orte ohne JavaScript
(Mails, Vorschaubilder). Damit das Herauslösen einfach bleibt, importiert Code in `sprites/` nichts aus dem übrigen
Spiel (kein Zustand, kein Netz, kein React, keine Konstanten aus `core/`). Der Linter erzwingt das. Nur
`sprites/browser.ts` nutzt das DOM. Was nur das Spiel betrifft (welches Aussehen ein Spieler hat, welche Dateien
geladen werden), bleibt beim Herauslösen im Spiel.

## Konsequenzen

- Ein neues Teil ist ein PNG und ein Eintrag in `parts`, ein neuer Slot ein Eintrag in `slots`, eine neue Animation
  ein Eintrag und neue Zeilen. Die Darstellung wird dafür nicht umgebaut.
- Das Pack hat keine Teile. Jedes weitere Kleidungsstück, jede Frisur und jede Waffe ist Zeichenarbeit in allen
  Bildern. Tauschbare Kleidung braucht außerdem einen gezeichneten Körper darunter.
- Die Zellgröße zu ändern heißt, alle Teile des Rigs neu zu exportieren. Deshalb ist sie von Anfang an 48×48.
- Ein Vitest prüft die echten Dateien gegen die Regeln; ein neues Teil ist damit automatisch geprüft.
- Bewusste Abweichung von KISS ([Constitution](../../specs/constitution.md) §5): Format, Slots und größere Zelle gehen
  über das hinaus, was zwei feste Teile brauchen. Ein späterer Wechsel des Formats wäre ein Neu-Export aller
  Grafiken. Gebaut wird nur, was 017 nutzt (zwei Slots, zwei Animationen).

## Alternativen

- **Pack-Blätter unverändert, Aufbau als Konstanten im Code:** am einfachsten, aber ein Teiletausch wäre ein Umbau.
- **Einfärben über exakte Farbwerte des Packs:** kein eigenes Kleidungsteil nötig, bricht aber bei jeder neuen Figur.
- **Ankerpunkte statt voller Blätter:** weniger Zeichenarbeit, mehr Logik; später möglich.
- **Eine Spiele-Bibliothek:** neue Abhängigkeit ohne Bedarf, Canvas 2D reicht.
- **Zusammensetzen auf dem Server:** als Ausweichlösung festgehalten (siehe oben).
- **Hochladen fertiger Blätter durch den Client:** verworfen, der Server darf dem Client kein Bild glauben, das alle
  sehen.
