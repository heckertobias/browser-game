# Hinweise für KI-Agenten

Dieses Repo arbeitet Spec-Driven. Lies vor jeder Aufgabe:

1. `specs/constitution.md`
2. `specs/README.md`
3. Die Spec des Features, an dem du arbeitest (`specs/NNN-*/`)

Struktur des Codes (Features, Schichten, Pfadregeln): `docs/project-structure.md`.

Regeln:
- Schreibe keinen Code für ein Feature, dessen `spec.md` nicht im Status `bereit` ist.
- Arbeite `tasks.md` der Reihe nach ab und hake erledigte Tasks ab.
- Wenn die Spec nicht passt oder unklar ist: anhalten und nachfragen, nicht raten.
- Typen nur aus `contracts/` generieren, nie duplizieren.
- Nie direkt auf `main`. Branch-Namen ([ADR 0011](docs/decisions/0011-branch-namen.md)):
  - `spec/NNN-name` – Spec schreiben oder ändern (nur `specs/`), PR mit Squash.
  - `impl/NNN-name` – Spec umsetzen, Draft-PR, Merge-Commit. Erst starten, wenn die Spec auf `main` `bereit` ist.
  - `chore/kurzer-name` – Tooling, CI, Skripte, ADRs, READMEs, PR mit Squash.
