# ADR 0009 – Spec-Branches, Release Candidates und Deploy per Arcane-API

**Status:** akzeptiert

## Kontext

[ADR 0002](0002-repo-und-ci-cd.md) setzte auf Trunk-Based Development und einen Deploy per Git-Commit: `release.sh`
veröffentlichte Images und schrieb danach die Version in `deploy/compose.yaml`, Arcane zog sie per Git Sync.
Das hat zwei Schwächen. Ein Feature lässt sich erst nach dem Merge auf dem Pi testen; ist es kaputt, steckt es schon in
`main`. Und der Deploy erzeugt Versions-Commits direkt auf `main` (`chore(deploy)`), die nur Rauschen sind.

## Entscheidung

1. **Pro Spec ein Branch** `spec/NNN-name` mit Draft-PR gegen `main`. Gemergt wird mit **Merge-Commit**, damit die
   Task-Commits der Spec erhalten bleiben. Tooling- und Doku-PRs sowie Dependabot werden weiter **gesquasht**.
2. **Rulesets:** `main` nimmt Änderungen nur noch per PR mit grünem `ci-ok`. Die Pflicht zur linearen Historie entfällt
   (Merge-Commits), signierte Commits bleiben Pflicht. Der Admin-Bypass gilt nur noch für PRs
   (`bypass_mode: "pull_request"`), kein direkter Push mehr auf `main`.
3. **Release Candidates:** Ein signierter Tag `vX.Y.Z-rc.N` kann von jedem Branch außer `main` gesetzt werden
   (`scripts/release.sh X.Y.Z-rc.N`). Die CI veröffentlicht `:X.Y.Z-rc.N` für beide Images, aber **kein** `:latest`; das
   GitHub Release ist eine Vorabversion. Ein Fehler führt zum nächsten RC (nur `N` steigt). Die Release-Notes eines Tags
   reichen bis zum letzten echten Release, RC-Tags zählen nicht als Vorgänger.
4. **Release** `vX.Y.Z` nur von `main` nach dem Merge. Das Image wird neu gebaut (nicht der RC umgetaggt); `release.sh`
   warnt, wenn kein RC existiert oder `main` vom letzten RC abweicht.
5. **Deploy per Arcane-API:** `scripts/deploy.sh VERSION` läuft lokal, lädt die Compose des Tags und die `.env` des
   Projekts hoch (nur `GAME_VERSION` wird ersetzt) und startet per Redeploy neu. `release.sh` ruft es nach dem
   Veröffentlichen der Images auf; ein Rollback ist `scripts/deploy.sh <ältere Version>`.
6. **Zugang:** Der API-Key liegt nur in `~/.config/browser-game/arcane.env` (Rechte 600, außerhalb des Repos) oder in
   Umgebungsvariablen, nie im Repo, nie in GitHub-Secrets. Er braucht nur `projects:read`, `projects:update` und
   `projects:deploy`.
7. **Compose:** `deploy/compose.yaml` verwendet `${GAME_VERSION}` in den `image:`-Zeilen. Die Vorlage
   `compose.template.yaml` entfällt. **Git Sync ist im Arcane-Projekt abgeschaltet.**

## Konsequenzen

- Ein RC mit Migrationen verändert die Datenbank auf dem Pi. Der Pi ist die Testumgebung; die Datenbank darf notfalls
  verworfen werden.
- RC-Tags und ihre Images bleiben bestehen (Tags sind unveränderlich).
- Ein Rollback ändert nur die Version, nicht die Datenbank: Es gibt kein Downgrade der Migrationen.
- Änderungen an der Compose in der Arcane-Oberfläche überschreibt der nächste Deploy.
- Versionen bis 0.2.3 haben eine feste Version in ihrer Compose; `deploy.sh` nimmt dafür die aktuelle `deploy/compose.yaml`.
- `main` ändert sich nur noch über PRs; die Merge-Methode wählt man pro PR.

## Alternativen

- **Git Sync behalten:** bräuchte weiter einen Versions-Commit auf `main` und kennt keine RCs von Branches.
- **Webhook von GitHub zu Arcane:** bräuchte eingehenden Zugriff auf das Homelab (ADR 0002: pull-basiert) oder einen
  Key in GitHub.
- **Versions-Commit auf `main` wie bisher:** nicht mit „nur per PR“ vereinbar und für RCs ungeeignet.
