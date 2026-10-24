# ADR 0002 – Repo-Regeln und CI/CD

**Status:** akzeptiert

## Kontext

Single-Dev-Projekt, öffentlich auf GitHub. Gewünscht: direkt auf `main` arbeiten, trotzdem
automatische Abhängigkeits-Updates mit CI-Prüfung, und nur Collaborators dürfen Issues und PRs eröffnen.
Ausgerollt wird zunächst als Docker-Container im Homelab.

## Entscheidung

**Branching:** Trunk-Based, alles direkt auf `main`. Es gibt keine Feature-Branches als Pflicht.

**Rulesets auf `main`** (Einrichtung: `scripts/github-setup.sh`, idempotent):

| Ruleset | Regeln | Bypass |
|---|---|---|
| `main-integrity` | kein Löschen, kein Force-Push, lineare Historie, signierte Commits | niemand |
| `main-via-pr-checks` | nur per PR (Squash), Status-Check `ci-ok` grün | Repo-Admin (direkter Push) |

Der Admin pusht direkt und signiert per SSH-Key. Alle anderen, praktisch nur Dependabot, gehen über einen PR mit grüner CI.
Squash-Merges über GitHub signiert GitHub selbst.

**Beteiligung:** PRs nur von Collaborators (`pull_request_creation_policy`), Issues nur von Collaborators
(nur über die Web-UI einstellbar). Wiki, Projects und Discussions sind aus.

**Sicherheit:**
- Dependabot Alerts und Security Updates, Secret Scanning mit Push Protection, Private Vulnerability Reporting,
  CodeQL Default Setup.
- Actions: Erlaubt sind nur GitHub-eigene Actions plus eine Allowlist im Setup-Skript. SHA-Pinning ist erzwungen,
  `GITHUB_TOKEN` ist standardmäßig read-only, und Workflows dürfen keine PRs approven.
- Workflows bekommen Schreibrechte nur pro Job und nur, wo nötig.

**CI:** `ci.yml` läuft bei Push auf `main` und bei PRs. Der Gate-Job `ci-ok` hängt von allen anderen Jobs ab und ist
der einzige Required Check. Neue Jobs werden nur in seinem `needs` eingetragen; das Ruleset bleibt unverändert.
`dependency-review.yml` blockiert PRs, die Abhängigkeiten mit Schwachstellen ab `high` einführen.

**Dependabot:** wöchentlich, Minor- und Patch-Updates gruppiert, mit 3 Tagen Cooldown gegen frisch kompromittierte Releases.
Patch und Minor werden nach grüner CI automatisch gemergt, Major-Updates manuell.

**CD:**
- CI baut ein Docker-Image für **amd64 und arm64** (Testbetrieb auf einem Raspberry Pi) und pusht es auf `main` nach
  `ghcr.io/heckertobias/browser-game` (Tags `sha-<commit>` und `main`, mit Build-Provenance-Attestation).
  arm64 entsteht per Cross-Build ohne Emulation; jede Architektur wird vor dem Push einzeln mit Trivy gescannt.
- **Releases:** Ein signierter Tag `vX.Y.Z` (nur der Admin darf `v*` anlegen; Tags sind unveränderlich, Releases
  per „Immutable Releases“ ebenfalls) veröffentlicht `:X.Y.Z` und `:latest` und legt ein GitHub Release mit Notes an.
  `scripts/release.sh` erzeugt `deploy/compose.yaml` **erst danach** aus `deploy/compose.template.yaml` – sonst würde
  Git Sync ein noch nicht existierendes Image ziehen. Auch Strukturänderungen (z. B. der Admin-Dienst aus 004) stehen
  zuerst nur in der Vorlage. Deployt wird also immer eine fest gepinnte Version, kein wanderndes Tag.
- Zwei Images aus einem Dockerfile: `browser-game` (Spiel) und `browser-game-admin` (ADR 0006), gleiche Tags.
- Das Homelab **zieht** das Image (pull-basiert). GitHub braucht damit keinen eingehenden Zugriff und keine
  Zugangsdaten zum Homelab. Wie das Homelab aktualisiert, wird beim ersten Rollout entschieden.

## Konsequenzen

- Lokale Commits müssen signiert sein, sonst lehnt GitHub den Push ab.
- Merges durch Auto-Merge laufen mit `GITHUB_TOKEN` und lösen deshalb **keinen** neuen Workflow auf `main` aus.
  Lösung: Die CI läuft zusätzlich **nächtlich** (`schedule`) auf `main` und veröffentlicht dann das Image. So sind
  Dependabot-Merges spätestens nach einem Tag im Image, und Sicherheitsfixes der Basis-Images kommen ebenfalls an.
  Ein GitHub-App-Token wäre schneller, kostet aber eine zusätzliche App mit Schreibrechten.
- Neue Third-Party-Actions müssen erst in die Allowlist (`scripts/github-setup.sh`) und per SHA gepinnt werden.
