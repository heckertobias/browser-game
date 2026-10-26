# ADR 0011 – Branch-Namen: Spec und Umsetzung getrennt

**Status:** akzeptiert – ersetzt Punkt 1 von [ADR 0009](0009-spec-branches-rc-und-deploy-per-api.md)

## Kontext

ADR 0009 sah pro Spec genau einen Branch `spec/NNN-name` vor, auf dem Spec, Plan, Tasks und Code entstehen. In der
Praxis wird eine Spec fast immer erst geschrieben und gemergt; umgesetzt wird sie oft viel später, wenn ihre
Voraussetzungen fertig sind. Am Branch-Namen soll man sehen, ob es um das Schreiben einer Spec oder um ihre
Umsetzung geht.

## Entscheidung

| Präfix | Wofür | PR | Merge |
|---|---|---|---|
| `spec/NNN-name` | Spec schreiben oder ändern: `spec.md`, `plan.md`, `tasks.md`, Roadmap, Vision – nur `specs/`, kein Code | normaler PR | Squash |
| `impl/NNN-name` | Spec umsetzen: Code, Tests, Tasks abhaken, Status `in arbeit` → `umgesetzt`; Release Candidates | Draft-PR, am Ende „Ready“ | Merge-Commit |
| `chore/kurzer-name` | Tooling, CI, Skripte, ADRs, READMEs ohne Bezug zu einer Spec | normaler PR | Squash |

- `NNN-name` ist bei `spec/` und `impl/` derselbe wie der Ordner unter `specs/` (z. B. `spec/011-chat`,
  `impl/011-chat`).
- Ein `impl/`-Branch startet von `main`, nachdem die Spec dort im Status `bereit` liegt.
- Muss die Spec während der Umsetzung angepasst werden, passiert das auf dem `impl/`-Branch (erst Spec, dann Code).
- Mehrere Specs in einem Doku-PR (wie bei 007–010) laufen über einen `spec/`-Branch mit sprechendem Namen ohne
  Nummer, z. B. `spec/handy-push-dotnet`.
- Dependabot behält seine eigenen Branch-Namen.

Alles andere aus ADR 0009 (Rulesets, Release Candidates von jedem Branch außer `main`, Deploy) bleibt.

## Konsequenzen

- Die Spec liegt früh auf `main`. Andere Specs und die Roadmap können sich auf sie beziehen, bevor eine Zeile Code
  existiert.
- Pro umgesetzter Spec gibt es zwei PRs: einen kleinen (Squash) für die Spec und einen großen (Merge-Commit) für die
  Umsetzung.
- Ältere Spec-Branches (`spec/006-sprachen`) enthielten noch beides; das bleibt so in der Historie.

## Alternativen

- **Ein Branch wie bisher:** Die Spec bliebe bis zur Umsetzung im Draft-PR liegen, oft wochenlang, und andere Specs
  sähen sie nicht auf `main`.
- **`docs/`-Präfix für Specs:** vermischt Specs mit allgemeiner Doku; `spec/` ist eindeutiger.
- **`feat/` statt `impl/`:** „Feature“ ist im Repo schon das Wort für die Spec selbst; `impl/` sagt klarer, dass
  gebaut wird.
