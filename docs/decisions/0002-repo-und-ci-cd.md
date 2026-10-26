# ADR 0002 – Repo-Regeln und CI/CD

**Status:** akzeptiert – Branching, Merge-Regeln und Deploy ersetzt durch [ADR 0009](0009-spec-branches-rc-und-deploy-per-api.md) (2026-10-25)

## Kontext

Single-Dev-Projekt, öffentlich auf GitHub. Gewünscht: direkt auf `main` arbeiten, trotzdem
automatische Abhängigkeits-Updates mit CI-Prüfung, und nur Collaborators dürfen Issues und PRs eröffnen.
Ausgerollt wird zunächst als Docker-Container in einer privaten Testumgebung.

## Entscheidung

**Branching:** Trunk-Based, alles direkt auf `main`. Es gibt keine Feature-Branches als Pflicht.
_(Ersetzt durch ADR 0009: Spec-Branches mit Draft-PR.)_

**Rulesets auf `main`** (Einrichtung: `scripts/github-setup.sh`, idempotent):

| Ruleset | Regeln | Bypass |
|---|---|---|
| `main-integrity` | kein Löschen, kein Force-Push, lineare Historie, signierte Commits | niemand |
| `main-via-pr-checks` | nur per PR (Squash), Status-Check `ci-ok` grün | Repo-Admin (direkter Push) |

Der Admin pusht direkt und signiert per SSH-Key. Alle anderen, praktisch nur Dependabot, gehen über einen PR mit grüner CI.
Squash-Merges über GitHub signiert GitHub selbst.
_(Seit ADR 0009: Merge-Commit und Squash erlaubt, keine lineare Historie, Admin-Bypass nur per PR.)_

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

**Dependabot:** wöchentlich, Minor- und Patch-Updates gruppiert, mit 7 Tagen Cooldown gegen frisch kompromittierte
Releases (geändert 2026-10-25, vorher 3 Tage). Sicherheitsupdates sind davon ausgenommen und kommen sofort. Auch das
.NET SDK kommt nur so: `global.json` nutzt `rollForward: "patch"`, damit die CI genau die angegebene Version
installiert und nicht an Dependabot vorbei den neuesten Patch zieht. Für npm gilt dasselbe auch bei Installationen von Hand:
`client/.npmrc` setzt `min-release-age=7` (wirksam ab npm 11.10).
Patch und Minor werden nach grüner CI automatisch gemergt, Major-Updates manuell.
Der Cooldown greift nicht, wenn die Registry kein Veröffentlichungsdatum liefert (z. B. MCR; Dependabot schreibt dann
„Cooldown could not be applied“ in den PR). Das trifft vor allem Digest-Updates ohne `update-type`, aber auch Tag-Updates
dieser Registries; Docker Hub (`node`) hat Cooldown. Solche PRs und Gruppen mit einem solchen Eintrag bekommen das Label
`wartet-7-tage`; `dependabot-wartet.yml` aktiviert täglich den Auto-Merge, sobald der letzte Commit 7 Tage alt ist.
Sicherheitsupdates (Label `security`, außer Major) werden sofort gemergt. Nur ausdrücklich erkannte Minor- und Patch-Updates mergen sofort.

**CD:**
- CI baut die Docker-Images für **amd64 und arm64** (z. B. Raspberry Pi) bei jedem Push, PR und
  nächtlich und scannt jede Architektur mit Trivy. arm64 entsteht per Cross-Build ohne Emulation.
- **Veröffentlicht wird nur bei Release-Tags** (geändert 2026-10-24): Ohne Tag landet nichts in GHCR, also auch kein
  wanderndes `:main`. Mit Tag entstehen `:X.Y.Z` und `:latest`, jeweils mit Build-Provenance-Attestation.
- **Releases:** Ein signierter Tag `vX.Y.Z` (nur der Admin darf `v*` anlegen; Tags sind unveränderlich, Releases
  per „Immutable Releases“ ebenfalls) veröffentlicht `:X.Y.Z` und `:latest` und legt ein GitHub Release mit Notes an.
  Deployt wird immer eine fest gepinnte Version, kein wanderndes Tag.
  _(Ersetzt durch ADR 0009 und 0012: Deploy über einen lokalen Hook; Vorlage und Git Sync entfallen. Dort auch RC-Tags `vX.Y.Z-rc.N`, die kein `:latest` bekommen.)_
- Zwei Images aus einem Dockerfile: `browser-game` (Spiel) und `browser-game-admin` (ADR 0006), gleiche Tags.
- Die Testumgebung **zieht** das Image (pull-basiert). GitHub braucht damit keinen eingehenden Zugriff und keine
  Zugangsdaten zur Testumgebung.

## Konsequenzen

- Lokale Commits müssen signiert sein, sonst lehnt GitHub den Push ab.
- Merges durch Auto-Merge laufen mit `GITHUB_TOKEN` und lösen deshalb **keinen** neuen Workflow auf `main` aus.
  Die CI läuft deshalb zusätzlich **nächtlich** (`schedule`) auf `main` und prüft den Stand inklusive Trivy-Scan.
  `scripts/release.sh` akzeptiert für `main` deshalb auch Läufe per `schedule` und `workflow_dispatch`
  (`gh workflow run ci.yml --ref main`), nicht nur `push`.
- Updates (auch Dependabot- und Basis-Image-Sicherheitsfixes) erreichen den Betrieb **nur über ein Release**. Meldet der
  nächtliche Scan eine neue Schwachstelle oder kommt ein wichtiges Update, ist ein Patch-Release (`X.Y.Z+1`) fällig.
- Neue Third-Party-Actions müssen erst in die Allowlist (`scripts/github-setup.sh`) und per SHA gepinnt werden.
