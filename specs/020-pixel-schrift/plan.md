# 020 – Plan

Gehört zu: [spec.md](spec.md)

> **Vorläufig.** Die Spec hat noch offene Fragen. Der Plan geht von den Empfehlungen unten aus und markiert abhängige
> Stellen mit **⚠ F-n**. Ändert eine Antwort den Umfang, wird der Plan vor `bereit` angepasst.

## Überblick

```
Client  ── client/src/assets/<schrift>.ttf      Pixel-Schrift (CC0) + Zeile in LICENSES.md
        ── render/pixelFont.ts                  Name der Schrift, Größe, geprüfter Zeichenvorrat
        ── render/render.ts                     Namen über Figuren in der Pixel-Schrift, auf ganze Pixel ausgerichtet
        ── styles/global.css                    @font-face; Tailwind-Variable --font-mono → Pixel-Schrift (HUD, Chat, Menü) ⚠ F-1, F-4
        ── app/App.tsx                          Schrift beim Start laden (für das Canvas)
Server  ── nur bei F-2 = ja: landing.css + Kopie der Schrift nach wwwroot/fonts/ (Razor-Seiten)
```

Spiel-UI und Razor-Seiten nutzen heute `font-mono` (Tailwind) als Pixel-Akzent; das Canvas zeichnet Namen in
`8px monospace`. Die Pixel-Schrift ersetzt beides an je einer Stelle.

**Voraussetzung:** 001. Aus 017 kommen ADR 0016 und `client/src/assets/` mit Register und Test; ist 017 noch nicht
umgesetzt, legt T1 hier beides an (wie in 017 T1 beschrieben).

## Offene Fragen der Spec und Empfehlung

| | Frage (spec.md) | Empfehlung | Begründung |
|---|---|---|---|
| F-1 | Wo gilt die Schrift? | **Namen über Figuren und die ganze Spiel-UI** (HUD, Chat, Menüs) | Vision: „Menüs und HUD … mit pixeliger Schrift“; technisch eine Variable (`--font-mono`) |
| F-2 | Auch öffentliche Seiten? | **Ja, nur dort, wo heute `font-mono` steht** (Titel, Spielname, Buttons) | Gleiche Optik wie im Spiel; Admin bleibt sachlich (005) |
| F-3 | Welche Schrift, welche Lizenz? | **Streng CC0**, ADR 0016 bleibt. Kandidaten: die Schriften aus Ninja Adventure (CC0 über das Pack) oder *Pixel Operator* (Jayvee Enaguas, CC0, Stand 2018) | Beide CC0. Gewählt wird die, die ä ö ü Ä Ö Ü ß und alle Zeichen der Texte (006) enthält. OFL nur, wenn keine passt |
| F-4 | Lesbar genug für Chat? | **Ja, in nativer Größe**; die manuelle Prüfung entscheidet | Fällt sie durch, behält nur der Chat-Text die Systemschrift (eine Klasse) |

## Konstanten

Reine Darstellung, Client (`render/pixelFont.ts`).

| Name | Wert | Bedeutung |
|---|---|---|
| `PIXEL_FONT_FAMILY` | `"PixelFont"` | Name der Schrift in `@font-face` und im Canvas |
| `NAME_FONT_SIZE` | native Größe der Schrift in Weltpixeln (z. B. 8) | Namen über Figuren (AK-1); am gewählten Font festlegen |
| `NAME_BASELINE_GAP` | 2 + Unterlänge der Schrift | Abstand Grundlinie → Oberkante der Figur, ganzzahlig |
| `UI_FONT_SIZE` | native Größe oder ein Vielfaches (z. B. 16 CSS-Pixel) | Textgröße in HUD, Chat, Menü (⚠ F-1, F-4) |
| `PIXEL_FONT_CHARSET` | geprüfter Zeichenvorrat | Zeichen, die die Schrift sicher enthält (AK-2) |

## Entschiedene Fragen (Plan)

1. **Eine Schrift, TTF wie geliefert.** Keine Umwandlung nach WOFF2 (das wäre ein Werkzeug mehr); TTF-Pixelschriften
   sind klein. Liegt eine Bold- oder 8-px-Variante bei, kommt nur die genutzte ins Repo.
2. **Canvas** (AK-1): `ctx.font = nameFont()` (`"<NAME_FONT_SIZE>px PixelFont, monospace"`), `textAlign = "left"`,
   `textBaseline = "alphabetic"`. x = `Math.round(player.x − ctx.measureText(name).width / 2)`, y =
   `top − NAME_BASELINE_GAP` (ganzzahlig). Mit ganzzahligem `k` liegen die Pixel der Schrift dann auf Gerätepixeln.
3. **Laden fürs Canvas:** `App` ruft beim Start `document.fonts.load(nameFont())` (Promise mit `void`, Fehler still).
   Bis die Schrift da ist, zeichnet das Canvas mit `monospace` weiter; danach von selbst in der Pixel-Schrift.
4. **Spiel-UI** (⚠ F-1): In `global.css` `@font-face` (`font-display: swap`) und
   `@theme { --font-mono: "PixelFont", ui-monospace, monospace; }`. Alle bestehenden `font-mono`-Klassen in `App.tsx`,
   `Chat.tsx`, `Menu.tsx`, `KeyBindings.tsx` nutzen damit die Pixel-Schrift. Textgrößen dort auf `UI_FONT_SIZE`
   (z. B. `text-sm` → `text-base`), damit die Schrift nicht verschmiert.
5. **Fehlende Zeichen** (AK-2): Browser und Canvas nehmen für ein Zeichen, das die Schrift nicht hat, die nächste
   Schrift der Liste (`monospace`). Damit wird jedes Zeichen dargestellt, auch Chat-Text mit Zeichen außerhalb von
   Deutsch/Englisch. Für alle Texte aus 006 muss die Pixel-Schrift die Zeichen selbst haben (Test).
6. **Razor-Seiten** (⚠ F-2): `build:landing` kopiert die Schrift nach `server/BrowserGame.Server/wwwroot/fonts/`
   (gitignored, wie `css/site.css`); `landing.css` bekommt dasselbe `@font-face` mit `url("/fonts/<schrift>.ttf")` und
   dieselbe `--font-mono`-Variable. Admin (005) bleibt unberührt.

## Verträge

Keine Änderung.

## Server

Keine Logik. Nur bei F-2 = ja: neue gebaute Datei unter `wwwroot/fonts/` (Static Files, CSP `default-src 'self'`
erlaubt sie schon).

## Client

### Assets
- `client/src/assets/<schrift>.ttf` (Name wie geliefert); Zeile in `LICENSES.md` mit Herkunft, Stand, `CC0 1.0` und
  „geprüft: ä ö ü Ä Ö Ü ß … ·“.

### `render/pixelFont.ts` (neu)
- `PIXEL_FONT_FAMILY`, `NAME_FONT_SIZE`, `NAME_BASELINE_GAP`, `PIXEL_FONT_CHARSET` (Tabelle oben).
- `nameFont(): string` → `"<size>px PixelFont, monospace"`.

### `render/render.ts`
- Namen nach Entscheidung 2.
- `fakeContext.ts` (Testhilfe): additiv `measureText(text) → { width: text.length × 5 }` und – falls 017 noch fehlt –
  `fillText` als `text`-Op mit `font`.

### `styles/global.css`, Spiel-UI (⚠ F-1, F-4)
- Entscheidung 4. Größen in den genannten Komponenten anpassen; keine anderen Stiländerungen.

### `app/App.tsx`
- Entscheidung 3.

### Razor-Seiten (⚠ F-2)
- `client/src/styles/landing.css`, `client/package.json` (`build:landing` kopiert die Schrift), `.gitignore`
  (`server/BrowserGame.Server/wwwroot/fonts/`).

## Abdeckung der Akzeptanzkriterien

| AK | Wie erfüllt | Wie getestet |
|---|---|---|
| 1 | Pixel-Schrift im Canvas, ganzzahlig ausgerichtet | Vitest `render-names.test.ts`: `text`-Op je sichtbarer Figur mit `font === nameFont()`, x und y ganzzahlig, x = `round(x − Breite/2)`; **manuell:** PC und Handy, Namen scharf und lesbar |
| 2 | Zeichenvorrat geprüft, Rückfall pro Zeichen | Vitest `pixelFont.test.ts`: jedes Zeichen aller Texte in `dictionaries.de` und `dictionaries.en` (006) liegt in `PIXEL_FONT_CHARSET`; ebenso `A–Z`, `a–z` (Namensregel aus 003); Font-Stack endet auf `monospace`; **manuell:** Sprache Deutsch, Texte mit Umlauten und ß in HUD/Menü, Chat-Nachricht „Größe äöü ÄÖÜ ß“ |

## Constitution-Check

- [x] **Server ist Autorität:** nicht betroffen (reine Darstellung).
- [x] **Typen nur aus `contracts/`:** keine Verträge.
- [x] **Offline-Fortschritt:** nicht betroffen.
- [x] **Keine unnötigen neuen Abhängigkeiten:** kein Paket; eine Schriftdatei (CC0), Release mindestens 7 Tage alt
  (Pixel Operator 2018; Ninja Adventure 2026-03-29).
- [x] **Spielwerte:** keine.
- [x] **KISS:** eine Schrift, eine CSS-Variable, eine Zeile im Canvas.

## Risiken / Abweichungen

- **Zeichenvorrat ungeprüft:** Für keinen Kandidaten ist belegt, dass er Umlaute, ß, `…` und `·` hat. Das wird bei T1
  am Font selbst geprüft (Glyphen-Tabelle, z. B. im Browser). Fehlt ein Zeichen aus den Texten, gilt die Schrift als
  ungeeignet oder der Text wird angepasst (Spec 006).
- **`PIXEL_FONT_CHARSET` ist eine Behauptung:** Der Test prüft die Texte gegen die Liste, nicht gegen die Datei. Die
  Liste wird einmal am Font geprüft und bei einem Font-Wechsel neu.
- **Unschärfe in CSS:** Bei Gerätepixel-Verhältnissen wie 2,625 trifft keine CSS-Größe genau die Pixel der Schrift.
  AK-1 verlangt Schärfe nur für die Namen (Canvas). HUD und Chat sind „gut lesbar“, nicht pixelgenau.
- **Layout:** Größere UI-Schrift (Entscheidung 4) kann HUD-Zeilen umbrechen. Prüfung am Handy hochkant.
- **SIL OFL:** Fällt die Wahl doch auf eine OFL-Schrift, ändert sich ADR 0016 (Ausnahme für Schriften) und
  `licenses.test.ts` (Lizenz `OFL-1.1` zulassen). Die OFL verlangt die Lizenzdatei neben der Schrift.
