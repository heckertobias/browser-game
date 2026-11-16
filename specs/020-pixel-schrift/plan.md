# 020 – Plan

Gehört zu: [spec.md](spec.md)

## Überblick

```
Client  ── client/src/assets/fonts/<schrift>/   5–6 Kandidaten (TTF wie geliefert) + bei OFL die Lizenzdatei; Zeilen in LICENSES.md
        ── render/nameFonts.ts                  Tabelle der Kandidaten (Familie, Datei, native Größe, Abstand), aktuelle Schrift
        ── render/render.ts                     Namen über Figuren in der aktuellen Schrift, auf ganze Pixel ausgerichtet
        ── engine/game.ts, network/net.ts       NextNameFont → nächste Schrift, Hinweis mit ihrem Namen
        ── app/App.tsx                          alle Kandidaten beim Start laden (für das Canvas)
Server  ── Commands (015): /run nameFont() → NextNameFont nur an den Absender
Vertrag ── game.proto: ServerMessage 12 = NextNameFont (nach der Wahl wieder entfernt)
Doku    ── Lizenz-ADR aus 017 (ADR 0016): Ausnahme OFL für Schriften
```

Nur die **Namen über den Figuren** (Canvas) nutzen die Pixel-Schrift. HUD, Chat, Menüs, öffentliche Seiten und Admin
bleiben, wie sie sind.

Die Spec läuft in zwei Phasen: Erst sind alle Kandidaten eingebaut und im Spiel umschaltbar (AK-3). Dann wählt der
User im Spiel eine, und die übrigen samt Umschalten werden entfernt (AK-4).

**Voraussetzung:** 001, 015 (`CommandRegistry`, `CommandContext`, Abfangen im `GameLoop`, Hinweis über dem Chat-Feld).
Aus 017 kommen ADR 0016, `client/src/assets/` mit `LICENSES.md` und `licenses.test.ts` sowie `CommandContext.Reply`;
ist 017 noch nicht umgesetzt, legt T1 bzw. T3 hier das Nötige an (wie in 017 T1 und T3 beschrieben).

## Kandidaten

Vorauswahl nach Lesbarkeit bei kleiner Größe, Lizenz und Zeichenvorrat. T1 prüft jeden am Original (Lizenzdatei,
Herkunft, Glyphen). Fällt einer durch, fliegt er raus; es bleiben mindestens fünf.

| Schrift | Urheber | Lizenz (zu prüfen) | native Größe |
|---|---|---|---|
| Pixel Operator | Jayvee Enaguas | CC0 1.0 | 8 bzw. 16 |
| monogram | datagoblin | CC0 1.0 | 16 (Glyphen ~7 hoch) |
| Press Start 2P | CodeMan38 | OFL 1.1 | 8 |
| Silkscreen | Jason Kottke | OFL 1.1 | 8 |
| Pixelify Sans | Stefie Justprince | OFL 1.1 | 16 (am Original prüfen) |
| Tiny5 | Stefan Schmidt | OFL 1.1 | 5 bzw. 10 |

Muss enthalten: `A–Z`, `a–z` (Namensregel aus 003) und `ä ö ü Ä Ö Ü ß` (spätere Ausweitung, Spec).

## Konstanten

Reine Darstellung, Client (`render/nameFonts.ts`).

| Name | Wert | Bedeutung |
|---|---|---|
| `NAME_FONTS` | Tabelle je Kandidat: `id`, `family`, `url`, `size`, `baselineGap` | Größe = native Größe der Schrift in Weltpixeln, damit jedes Glyphen-Pixel auf genau `k×k` Gerätepixeln liegt; `baselineGap` = Abstand Grundlinie → Oberkante der Figur (2 + Unterlänge), ganzzahlig |
| `NAME_FONT_FALLBACK` | `"monospace"` | letzte Schrift im Font-Stack (fehlendes Zeichen, Schrift noch nicht geladen) |

Nach der Wahl (AK-4) schrumpft `NAME_FONTS` auf einen Eintrag und heißt `NAME_FONT`.

## Entschiedene Fragen (Plan)

1. **TTF wie geliefert.** Keine Umwandlung nach WOFF2 (ein Werkzeug mehr); Pixelschriften sind klein. Je Kandidat nur
   die genutzte Datei (z. B. Regular), in eigenem Ordner `assets/fonts/<id>/` mit der Lizenzdatei.
2. **Canvas** (AK-1): `ctx.font = "<size>px <family>, monospace"`, `textAlign = "left"`, `textBaseline =
   "alphabetic"`. x = `Math.round(player.x − ctx.measureText(name).width / 2)`, y = `top − baselineGap`. Mit
   ganzzahligem `k` liegen die Pixel der Schrift auf Gerätepixeln.
3. **Laden fürs Canvas:** `App` legt je Kandidat ein `FontFace` an, fügt es zu `document.fonts` hinzu und lädt es
   (Fehler still). Bis eine Schrift da ist, zeichnet das Canvas mit `monospace`; danach von selbst in der Pixel-Schrift.
4. **Umschalten über den Server** (AK-3), wie der Pfeil-Schalter in 017: `/run nameFont()` ist ein Command ohne
   Parameter. Er schickt `NextNameFont` nur an den Absender; der Client nimmt die nächste Schrift aus `NAME_FONTS`
   (nach der letzten wieder die erste) und zeigt über dem Chat-Feld kurz „Namensschrift 3/6: Silkscreen“, mit dem
   Mechanismus, der in 015 `CommandRejected` zeigt. Kein Parameter: Der Server muss die Kandidaten nicht kennen, und
   sechs Schritte sind schnell durchgeklickt. Nur auf diesem Gerät, nicht gespeichert; nach Neuladen gilt der erste
   Kandidat.
5. **Wahl und Aufräumen** (AK-4): Die Wahl trifft der User im Spiel (Testumgebung) und trägt sie in `tasks.md` ein.
   Danach: übrige Schriften und Lizenzdateien aus `assets/fonts/` und `LICENSES.md` löschen, Command `nameFont`
   entfernen, `NextNameFont` aus dem Proto entfernen und Feld 12 sowie den Namen als `reserved` markieren, Hinweistext
   und Umschalt-Code im Client entfernen. Was bleibt: eine Schrift, `NAME_FONT`, das Laden im `App`, die Namen im
   Canvas.
6. **Fehlende Zeichen** (AK-2): Browser und Canvas nehmen für ein fehlendes Zeichen die nächste Schrift der Liste
   (`monospace`). Für Namen muss die Pixel-Schrift die Zeichen selbst haben; das prüft ein Test an der Datei
   (Entscheidung 7).
7. **Zeichenvorrat aus der Datei:** Ein Vitest liest jede TTF unter `assets/fonts/` und prüft über die `cmap`-Tabelle
   (Format 4, sonst 12), dass alle Pflichtzeichen eine Glyphe haben. Der Leser ist eine kleine Testhilfe (~60 Zeilen,
   kein Paket). So beruht AK-2 nicht auf einer abgeschriebenen Liste.
8. **Lizenz** (AK-5): `LICENSES.md` nennt je Datei Herkunft, Stand und Lizenz (`CC0 1.0` oder `OFL-1.1`).
   `licenses.test.ts` (017) lässt `OFL-1.1` nur für Dateien unter `assets/fonts/` zu und verlangt dann `OFL.txt` im
   selben Ordner. Die Lizenzdatei wird mit dem Client gebaut und ausgeliefert (Vite kopiert sie neben die Schrift). ADR
   0016 bekommt den Absatz „Schriften dürfen auch unter der SIL Open Font License 1.1 stehen: Lizenztext mitliefern,
   nicht einzeln verkaufen, geänderte Fassungen nicht unter dem reservierten Namen.“

## Verträge

**Protobuf** (`contracts/proto/browsergame/v1/game.proto`), abwärtskompatibel. `ServerMessage` 12 ist frei (8 = 015,
9 = 017, 10–11 = 013). Keine Eingabe vom Client: Der Command kommt als `SendChat` (011/015).

```proto
message ServerMessage {
  oneof payload {
    // … 1–11
    // Nur an den Absender von /run nameFont(): nächste Kandidaten-Schrift für Namen (020, nur während der Erprobung).
    NextNameFont next_name_font = 12;
  }
}

// Leer. Der Client wechselt nur seine eigene Anzeige; der Server merkt sich nichts (020 AK-3).
message NextNameFont {}
```

Nach der Wahl (Entscheidung 5): `reserved 12; reserved "next_name_font";` in `ServerMessage`, `NextNameFont` entfällt.

**OpenAPI:** keine Änderung.

## Server

- `nameFont` (0 Parameter) im `CommandRegistry` (015): `context.Reply(new ServerMessage { NextNameFont = new() })`
  (Reply aus 017). Keine Wirkung auf die `World`.
- Nach der Wahl: Command entfernen.

## Client

### Assets
- `client/src/assets/fonts/<id>/<Datei>.ttf` und bei OFL `OFL.txt`; Zeilen in `LICENSES.md` mit Herkunft, Stand,
  Lizenz und „geprüft: A–Z a–z ä ö ü Ä Ö Ü ß“.

### `render/nameFonts.ts` (neu)
- `NAME_FONTS` (Tabelle oben), `nameFontCss(font): string` → `"<size>px <family>, monospace"`.
- `class NameFontChoice { current; next(): NameFont }` – reiner Zustand, ohne DOM.

### `render/render.ts`
- `RenderOptions.nameFont?: NameFont` (fehlt → `8px monospace` wie bisher, damit bestehende Tests bleiben); Namen nach
  Entscheidung 2.
- `fakeContext.ts` (Testhilfe): additiv `measureText(text) → { width: text.length × 5 }` und – falls 017 noch fehlt –
  `fillText` als `text`-Op mit `font`.

### `network/net.ts`, `engine/game.ts`
- `onNextNameFont`; `Game` ruft `NameFontChoice.next()`, gibt die Schrift an `render` und meldet den Hinweis an die
  UI (Ereignis wie `CommandRejected` in 015). Text `nameFontHint` in `i18n.ts` (de/en): „Namensschrift {0}/{1}: {2}“ /
  „Name font {0}/{1}: {2}“.

### `app/App.tsx`
- Entscheidung 3.

## Abdeckung der Akzeptanzkriterien

| AK | Wie erfüllt | Wie getestet |
|---|---|---|
| 1 | Pixel-Schrift im Canvas, ganzzahlig ausgerichtet | Vitest `render-names.test.ts`: `text`-Op je sichtbarer Figur mit `font === nameFontCss(current)`, x und y ganzzahlig, x = `round(x − Breite/2)`; ohne `nameFont` wie bisher; **manuell:** PC und Handy, Namen scharf und lesbar, für jeden Kandidaten |
| 2 | Glyphen aus der Datei geprüft | Vitest `fonts.test.ts`: jede TTF unter `assets/fonts/` hat Glyphen für `A–Z`, `a–z`, `ä ö ü Ä Ö Ü ß`; jeder Eintrag in `NAME_FONTS` zeigt auf eine vorhandene Datei; Font-Stack endet auf `monospace` |
| 3 | Command → NextNameFont → nächste Schrift | xUnit: `nameFont()` antwortet genau einmal, `World` unverändert; Integration (WS): nur der Absender bekommt `NextNameFont`, kein Chat-Echo; Vitest `net`: Nachricht → Handler; Vitest `game-name-font.test.ts`: zweimal → zweiter Kandidat, nach dem letzten der erste, Hinweis mit Nummer und Name, nächstes Bild mit neuer Schrift; Vitest `nameFonts.test.ts`; **manuell:** alle Kandidaten durchschalten |
| 4 | Aufräumen nach der Wahl | Nach dem Aufräum-Task: Vitest `fonts.test.ts` findet genau eine TTF; xUnit: `/run nameFont()` → `CommandRejected(UNKNOWN_COMMAND)`; `buf breaking` grün mit `reserved 12`; Wahl in `tasks.md` festgehalten |
| 5 | Register und Lizenzdatei | Vitest `licenses.test.ts`: jede Datei in `LICENSES.md`; `OFL-1.1` nur unter `assets/fonts/` und nur mit `OFL.txt` daneben; Build-Test: `OFL.txt` liegt in der Ausgabe neben der Schrift |

## Constitution-Check

- [x] **Server ist Autorität:** nicht betroffen (reine Darstellung); der Command bestätigt nur.
- [x] **Typen nur aus `contracts/`:** `NextNameFont` aus dem Proto.
- [x] **Offline-Fortschritt:** nicht betroffen.
- [x] **Keine unnötigen neuen Abhängigkeiten:** kein Paket; Schriftdateien unter CC0 oder OFL (Ausnahme per ADR 0016),
  Releases deutlich älter als 7 Tage.
- [x] **Spielwerte:** keine.
- [x] **KISS:** Umschalten ohne Parameter und ohne Speichern; nach der Wahl bleibt eine Schrift und eine Zeile im
  Canvas.

## Risiken / Abweichungen

- **Kandidaten ungeprüft:** Lizenz, Urheber und Zeichenvorrat der Tabelle stammen aus der Erinnerung und werden in T1
  am Original geprüft. Fehlen Downloads im Netz der Sitzung, stellt der User die Dateien bereit.
- **Native Größe unklar:** Manche Schriften haben mehrere oder keine eindeutige Pixelgröße (Pixelify Sans). Dann wird
  die kleinste Größe gewählt, bei der Glyphen auf ganzen Pixeln liegen; ist sie für Namen zu groß, fliegt der Kandidat
  raus.
- **Umschalt-Code ist Wegwerf-Code:** bewusst klein gehalten (ein Command, eine Nachricht, eine Klasse), damit das
  Aufräumen (AK-4) schnell geht. Wird die Erprobung vergessen, bleibt der Command harmlos stehen.
- **Ausweitung später:** HUD, Chat, Menüs und öffentliche Seiten bekommen die Schrift erst mit einer eigenen Spec,
  wenn sie sich an den Namen bewährt hat.
