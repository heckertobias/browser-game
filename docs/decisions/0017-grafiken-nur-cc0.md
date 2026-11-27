# ADR 0017 – Grafiken nur CC0 oder selbst erstellt

**Status:** akzeptiert (2026-11-27)

## Kontext

Mit [017](../../specs/017-spielfigur/spec.md) bis [020](../../specs/020-pixel-schrift/spec.md) kommen fremde
Grafiken und Schriften ins Spiel, zuerst aus dem Asset-Pack Ninja Adventure. Viele freie Lizenzen haben Auflagen:
Namensnennung (CC-BY), Weitergabe unter gleicher Lizenz (CC-BY-SA), Lizenztext neben der Datei (SIL OFL). Jede Auflage
muss dauerhaft erfüllt werden, im Spiel, in der Auslieferung oder im Repo. Ohne Register weiß später niemand mehr,
woher eine Datei stammt und was an ihr geändert wurde.

## Entscheidung

1. **Nur CC0 1.0 oder selbst erstellt.** Grafiken und Schriften des Spiels stehen unter CC0 1.0 oder sind selbst
   erstellt.
2. **Abgeleitete Werke sind erlaubt.** CC0 erlaubt Änderungen. Was geändert wurde, steht im Register.
3. **Ein Ort, ein Register.** Grafiken und Schriften des Spiels liegen unter `client/src/assets/`. Das Register
   [`client/src/assets/LICENSES.md`](../../client/src/assets/LICENSES.md) nennt jede Datei mit Herkunft, Stand,
   Lizenz und Änderungen.
4. **Automatische Prüfung.** Der Vitest `client/src/assets/licenses.test.ts` prüft: Jede Grafik- oder Schriftdatei im
   Ordner (`png`, `jpg`, `jpeg`, `gif`, `webp`, `svg`, `ttf`, `otf`, `woff`, `woff2`) hat genau eine Zeile im
   Register, mit der Lizenz `CC0 1.0` oder `eigen`. Jede Zeile nennt eine vorhandene Datei.
5. **Keine Credits im Spiel.** CC0 verlangt keine Namensnennung; Lizenzhinweise im Spiel sind nicht nötig.

Die Regel gilt nur für Grafiken und Schriften des Spiels unter `client/src/assets/`. Nicht gemeint sind Abhängigkeiten
aus npm oder NuGet samt mitgelieferter Icons und Schriften (z. B. Bootstrap Icons im Admin); für sie gilt die Lizenz
des Pakets. Die Platzhalter-Icons der Web-App (007) sind selbst erstellt und
entstehen per Skript (`client/scripts/icons.mjs`); sie bleiben unter `server/BrowserGame.Server/wwwroot/icons/`.

## Konsequenzen

- Eine neue Grafik oder Schrift kommt mit ihrer Zeile im Register im selben PR, sonst schlägt der Test fehl.
- Die Auswahl an Quellen ist kleiner: Pakete unter CC-BY oder OFL scheiden aus, solange diese ADR gilt.
- Herkunft und Änderungen stehen an einer Stelle; eine spätere Prüfung braucht nur das Register.

## Alternativen

- **CC-BY erlauben:** größere Auswahl, aber Namensnennung im Spiel nötig (Credits sind in 017 Out of Scope).
- **SIL OFL für Schriften:** viele Pixel-Schriften stehen unter OFL; die Lizenzdatei müsste mit ausgeliefert werden.
  Offen in [020](../../specs/020-pixel-schrift/spec.md); eine Ausnahme ergänzt dann diese ADR.
- **Register ohne automatische Prüfung:** einfacher, aber neue Dateien ohne Zeile fallen niemandem auf.
