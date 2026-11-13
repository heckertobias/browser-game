# ADR 0014 – oxlint statt ESLint

**Status:** akzeptiert

## Kontext

Der Client lintete mit ESLint, typescript-eslint (`strictTypeChecked`) und eslint-plugin-react-hooks. Diese Kette zog
rund 100 npm-Pakete nach sich. Sicherheitslücken in transitiven Paketen (zuletzt minimatch, GHSA-g3hv-75wg-m249)
blieben offen, bis alle Glieder der Kette nachzogen, und typescript-eslint blockierte das Update auf TypeScript 7.
Sicherheits-Patches sollen nicht auf Upstream warten.

## Entscheidung

1. **oxlint** mit **oxlint-tsgolint** für typgestützte Regeln ersetzt ESLint im Client. Beide sind native Binaries
   ohne npm-Abhängigkeiten.
2. **Konfiguration:** `client/oxlint.config.ts`, steht in `tsconfig.json` und wird mit typgeprüft. Die Regeln
   entsprechen `eslint:recommended`, `strictTypeChecked` und react-hooks `recommended`; die Importgrenzen zwischen
   Features (ADR 0013) werden weiter aus der Feature-Liste erzeugt.
3. **Kommentare:** Ausnahmen heißen `oxlint-disable…` mit Begründung nach `--`.

## Konsequenzen

- Lint dauert etwa eine Sekunde.
- `no-undef`, `no-useless-assignment` und `typescript/no-unnecessary-condition` laufen in oxlint als Nursery-Regeln.
- tsgolint prüft mit TypeScript 7 (typescript-go), auch wenn `tsc` noch eine ältere Version nutzt.
- Abweichung im Randfall: `typescript/no-meaningless-void-operator` meldet `void` vor einer Variablen nicht, nur vor
  Aufrufen mit `void`-Ergebnis und vor `undefined`.
- Dependabot darf TypeScript-Major-Updates wieder vorschlagen.

## Alternativen

- **ESLint behalten und per `overrides` patchen:** wiederkehrender Aufwand, jedes Mal mit Kompatibilitätsrisiko.
- **Biome:** ebenfalls ohne Abhängigkeiten, aber eigene Typ-Inferenz statt TypeScript-Compiler; ein großer Teil von
  `strictTypeChecked` fiele weg.
