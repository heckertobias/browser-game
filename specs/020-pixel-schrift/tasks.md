# 020 – Tasks

Gehört zu: [plan.md](plan.md)

Reihenfolge: Doku/Assets → Verträge → Server → Client → Prüfung und Wahl → Aufräumen. Jeder Task bringt seine Tests
mit. Erst beginnen, wenn 015 umgesetzt ist.

## Doku / Assets
- [ ] **T1** (AK-2, AK-5): Kandidaten am Original prüfen (Lizenz, Urheber, Stand, Glyphen, native Größe; Plan-Abschnitt
  „Kandidaten“); mindestens fünf nach `client/src/assets/fonts/<id>/` mit `OFL.txt` bei OFL; Zeilen in `LICENSES.md`;
  ADR 0017 um die OFL-Ausnahme ergänzen und `licenses.test.ts` anpassen (fehlen ADR und Register aus 017 noch, hier
  anlegen); Testhilfe `cmap`-Leser und Vitest `fonts.test.ts`.

## Verträge
- [ ] **T2** (AK-3): `game.proto`: `NextNameFont` (`ServerMessage` Feld 12); `buf lint` und `buf breaking` grün.

## Server
- [ ] **T3** (AK-3): Command `nameFont` im `CommandRegistry` mit `context.Reply` (fehlt `Reply` aus 017 noch, hier
  anlegen wie in 017 T3). Unit-Test (Reply genau einmal, `World` unverändert) und Integrationstest (WS: nur der
  Absender bekommt die Nachricht, kein Chat-Echo).

## Client
- [ ] **T4** (AK-1, AK-2): `render/nameFonts.ts` (`NAME_FONTS`, `nameFontCss`, `NameFontChoice`); Namen im Canvas nach
  Plan-Entscheidung 2 mit `RenderOptions.nameFont`; `fakeContext.ts` additiv (`measureText`); Laden aller Kandidaten im
  `App`; Vitest `nameFonts.test.ts`, `render-names.test.ts`.
- [ ] **T5** (AK-3): `net.ts` (`onNextNameFont`); `Game` wechselt die Schrift und meldet den Hinweis; Text
  `nameFontHint` (de/en); Hinweis über dem Chat-Feld; Vitest `net`, `game-name-font.test.ts`.

## Prüfung und Wahl
- [ ] **T6** (AK-1, AK-3): Manuelle Prüfung in der Testumgebung (PC mit DPR 1 und 2, Handy hochkant/quer, mehrere
  Figuren mit kurzen und langen Namen): `/run nameFont()` schaltet durch alle Kandidaten, Hinweis zeigt Nummer und
  Namen; jede Schrift scharf. **Der User wählt eine Schrift**; Wahl und Begründung hier eintragen: _…_

## Aufräumen
- [ ] **T7** (AK-4, AK-5): Nach Plan-Entscheidung 5: übrige Schriften, Lizenzdateien und Zeilen in `LICENSES.md`
  entfernen; `NAME_FONTS` → `NAME_FONT`; Command `nameFont`, `NextNameFont` (`reserved 12`), `onNextNameFont`,
  `NameFontChoice` und `nameFontHint` entfernen; Tests anpassen (eine TTF, `/run nameFont()` unbekannt).

## Tests / Prüfung
- [ ] **T8**: Abschlussprüfung in der Testumgebung: Namen in der gewählten Schrift, scharf und lesbar auf PC und
  Handy; `/run nameFont()` ist unbekannt; HUD, Chat, Menüs und öffentliche Seiten unverändert.

  Danach alle AKs abhaken, Spec-Status auf `umgesetzt`, Roadmap aktualisieren.
