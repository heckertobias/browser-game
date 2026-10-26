#!/usr/bin/env bash
# Release oder Release Candidate in zwei Schritten (ADR 0002, 0009, 0011, 0012):
#   1. Signierten Tag pushen → CI baut, scannt und veröffentlicht die Images und legt ein GitHub Release an
#        vX.Y.Z      nur von main:  :X.Y.Z + :latest
#        vX.Y.Z-rc.N von jedem Branch außer main (impl-Branch):  nur :X.Y.Z-rc.N, GitHub Release als Vorabversion
#   2. Erst wenn die Images existieren: der lokale Deploy-Hook rollt die Version aus (falls vorhanden)
#
# Aufruf: scripts/release.sh [--dry-run] 0.3.0 | 0.3.0-rc.1
#   --dry-run  alle Prüfungen, dann Hook --dry-run --unreleased; kein Tag, kein Push
#
# Deploy-Hook: ausführbare Datei $BROWSER_GAME_DEPLOY, sonst ~/.config/browser-game/deploy (außerhalb des Repos).
# Aufrufe im Repo-Verzeichnis, mit BROWSER_GAME_REPO gesetzt:
#   HOOK --check                        vor dem Tag; ein Fehler bricht ab
#   HOOK --dry-run --unreleased VERSION beim Trockenlauf
#   HOOK VERSION                        nach CI und GitHub Release
set -euo pipefail

DRY=0
if [[ "${1:-}" == "--dry-run" ]]; then DRY=1; shift; fi
VERSION="${1:-}"
REPO="heckertobias/browser-game"

die() { echo "Fehler: $*" >&2; exit 1; }

[[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-rc\.[0-9]+)?$ ]] || die "Version im Format X.Y.Z oder X.Y.Z-rc.N angeben, z. B. 0.3.0 oder 0.3.0-rc.1"
TAG="v$VERSION"
IS_RC=0
[[ "$VERSION" == *-rc.* ]] && IS_RC=1

HOOK="${BROWSER_GAME_DEPLOY:-${XDG_CONFIG_HOME:-$HOME/.config}/browser-game/deploy}"
[[ "$HOOK" == /* ]] || HOOK="$PWD/$HOOK"
if [[ -x "$HOOK" ]]; then HAS_HOOK=1; else HAS_HOOK=0; fi

# Immer im Repo dieses Skripts arbeiten, nicht im aktuellen Verzeichnis
root="$(git -C "$(dirname "${BASH_SOURCE[0]}")" rev-parse --show-toplevel)" || die "Repo des Skripts nicht gefunden (Aufruf über Symlink?)"
cd "$root"
export BROWSER_GAME_REPO="$root"
BRANCH="$(git branch --show-current)"
[[ -n "$BRANCH" ]] || die "kein Branch ausgecheckt (detached HEAD)"
if [[ "$IS_RC" == 1 ]]; then
  [[ "$BRANCH" != "main" ]] || die "Release Candidates nur von einem Branch außer main (Release: ohne -rc.N)"
else
  [[ "$BRANCH" == "main" ]] || die "Release nur von main aus (impl-Branch: Release Candidate X.Y.Z-rc.N)"
fi
[[ -z "$(git status --porcelain)" ]] || die "Arbeitskopie nicht sauber"
git fetch --quiet origin "$BRANCH" --tags || die "Branch $BRANCH nicht auf origin (gepusht?)"
[[ "$(git rev-parse HEAD)" == "$(git rev-parse "origin/$BRANCH")" ]] || die "$BRANCH ist nicht synchron mit origin/$BRANCH"
git rev-parse -q --verify "refs/tags/$TAG" >/dev/null && die "Tag $TAG existiert bereits"

HEAD_SHA="$(git rev-parse HEAD)"
if [[ "$IS_RC" == 1 ]]; then
  ci="$(gh run list --repo "$REPO" --workflow ci.yml --commit "$HEAD_SHA" --json event,status,conclusion \
    --jq '[.[] | select(.event == "pull_request" or .event == "push")][0] | if . == null then "" elif .status != "completed" then "läuft" else .conclusion end')"
  none="kein CI-Lauf – Draft-PR angelegt?"
else
  # Auto-Merges (GITHUB_TOKEN) lösen keinen Push-Lauf aus; dann zählt der nächtliche (schedule) oder ein manueller Lauf
  ci="$(gh run list --repo "$REPO" --workflow ci.yml --commit "$HEAD_SHA" --json event,status,conclusion \
    --jq '[.[] | select(.event == "push" or .event == "workflow_dispatch" or .event == "schedule")][0] | if . == null then "" elif .status != "completed" then "läuft" else .conclusion end')"
  none="kein Lauf – gh workflow run ci.yml --ref main"
fi
[[ "$ci" != "läuft" ]] || die "CI auf $HEAD_SHA läuft noch – abwarten"
[[ "$ci" == "success" ]] || die "CI auf $HEAD_SHA ist nicht grün (Status: ${ci:-$none})"

if [[ "$IS_RC" == 0 ]]; then
  last_rc="$(git tag --list "v$VERSION-rc.*" --sort=-v:refname | head -n 1)"
  if [[ -z "$last_rc" ]]; then
    echo "Warnung: kein Release Candidate v$VERSION-rc.* getestet" >&2
  elif ! git diff --quiet "$last_rc" HEAD; then
    echo "Warnung: main weicht von $last_rc ab (gemergter Stand ≠ getesteter Stand)" >&2
  fi
fi

if [[ "$DRY" == 1 ]]; then
  echo "Trockenlauf: würde Tag $TAG auf $(git log -1 --format='%h %s') setzen, pushen und CI abwarten."
  [[ "$HAS_HOOK" == 1 ]] || { echo "Kein Deploy-Hook ($HOOK) – es würde nur veröffentlicht."; exit 0; }
  exec "$HOOK" --dry-run --unreleased "$VERSION"
fi

if [[ "$HAS_HOOK" == 1 ]]; then
  "$HOOK" --check >/dev/null || die "Deploy-Hook meldet einen Fehler – erst $HOOK --check"
else
  echo "Hinweis: kein Deploy-Hook ($HOOK) – es wird nur veröffentlicht, nicht ausgerollt." >&2
fi

echo "== 1/2: Tag $TAG auf $(git log -1 --format='%h %s')"
git tag -s "$TAG" -m "$TAG"
git push --quiet origin "$TAG" || { git tag -d "$TAG" >/dev/null; die "Tag $TAG nicht gepusht – lokal wieder entfernt"; }

# Netzwerkfehler beim Abfragen zählen nicht als CI-Fehler; erst 10 Fehlversuche in Folge brechen ab.
echo "   warte auf CI-Lauf für $TAG …"
run_id=""
for _ in $(seq 1 30); do
  run_id="$(gh run list --repo "$REPO" --workflow ci.yml --branch "$TAG" --json databaseId --jq '.[0].databaseId // ""' 2>/dev/null || true)"
  [[ -n "$run_id" ]] && break
  sleep 5
done
[[ -n "$run_id" ]] || die "kein CI-Lauf für $TAG gefunden"
run_url="https://github.com/$REPO/actions/runs/$run_id"
fails=0
while :; do
  if state="$(gh run view "$run_id" --repo "$REPO" --json status,conclusion --jq '.status + " " + .conclusion' 2>/dev/null)"; then
    fails=0
    case "$state" in
      "completed success") break ;;
      completed*) die "CI für $TAG fehlgeschlagen (${state#completed }): $run_url" ;;
    esac
  else
    fails=$((fails + 1))
    (( fails < 10 )) || die "CI-Status für $TAG seit 10 Versuchen nicht abfragbar – Lauf prüfen: $run_url"
  fi
  sleep 15
done

if [[ "$HAS_HOOK" == 1 ]]; then
  echo "== 2/2: $VERSION ausrollen ($HOOK)"
  "$HOOK" "$VERSION" || die "Ausrollen fehlgeschlagen; $TAG ist veröffentlicht – erneut: $HOOK $VERSION"
fi
echo "Fertig: https://github.com/$REPO/releases/tag/$TAG"
