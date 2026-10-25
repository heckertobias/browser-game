#!/usr/bin/env bash
# Release oder Release Candidate in zwei Schritten (ADR 0002, 0009):
#   1. Signierten Tag pushen → CI baut, scannt und veröffentlicht die Images und legt ein GitHub Release an
#        vX.Y.Z      nur von main:  :X.Y.Z + :latest
#        vX.Y.Z-rc.N von jedem Branch außer main (Spec-Branch):  nur :X.Y.Z-rc.N, GitHub Release als Vorabversion
#   2. Erst wenn die Images existieren: scripts/deploy.sh rollt die Version über die Arcane-API aus
#
# Aufruf: scripts/release.sh [--dry-run] 0.3.0 | 0.3.0-rc.1
#   --dry-run  alle Prüfungen, dann scripts/deploy.sh --dry-run --unreleased; kein Tag, kein Push, kein API-Aufruf
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

# Immer im Repo dieses Skripts arbeiten, nicht im aktuellen Verzeichnis
# Relative Pfade aus der Umgebung beziehen sich auf das Aufrufverzeichnis, daher vor dem cd auflösen (deploy.sh erbt sie)
[[ -z "${ARCANE_CONFIG:-}" || "$ARCANE_CONFIG" == /* ]] || ARCANE_CONFIG="$PWD/$ARCANE_CONFIG"
[[ -z "${ARCANE_CA_CERT:-}" || "$ARCANE_CA_CERT" == /* ]] || ARCANE_CA_CERT="$PWD/$ARCANE_CA_CERT"
root="$(git -C "$(dirname "${BASH_SOURCE[0]}")" rev-parse --show-toplevel)" || die "Repo des Skripts nicht gefunden (Aufruf über Symlink?)"
cd "$root"
BRANCH="$(git branch --show-current)"
[[ -n "$BRANCH" ]] || die "kein Branch ausgecheckt (detached HEAD)"
if [[ "$IS_RC" == 1 ]]; then
  [[ "$BRANCH" != "main" ]] || die "Release Candidates nur von einem Branch außer main (Release: ohne -rc.N)"
else
  [[ "$BRANCH" == "main" ]] || die "Release nur von main aus (Spec-Branch: Release Candidate X.Y.Z-rc.N)"
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
  ci="$(gh run list --repo "$REPO" --workflow ci.yml --commit "$HEAD_SHA" --event push --json status,conclusion \
    --jq '.[0] | if . == null then "" elif .status != "completed" then "läuft" else .conclusion end')"
  none="keiner"
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
  exec scripts/deploy.sh --dry-run --unreleased "$VERSION"
fi

scripts/deploy.sh --check >/dev/null || die "Arcane-Konfiguration fehlt oder ungültig – erst scripts/deploy.sh --check"

echo "== 1/2: Tag $TAG auf $(git log -1 --format='%h %s')"
git tag -s "$TAG" -m "$TAG"
git push --quiet origin "$TAG"

echo "   warte auf CI-Lauf für $TAG …"
run_id=""
for _ in $(seq 1 30); do
  run_id="$(gh run list --repo "$REPO" --workflow ci.yml --branch "$TAG" --json databaseId --jq '.[0].databaseId // ""')"
  [[ -n "$run_id" ]] && break
  sleep 5
done
[[ -n "$run_id" ]] || die "kein CI-Lauf für $TAG gefunden"
gh run watch "$run_id" --repo "$REPO" --exit-status --interval 15 >/dev/null || die "CI für $TAG fehlgeschlagen: https://github.com/$REPO/actions/runs/$run_id"

echo "== 2/2: $VERSION über Arcane ausrollen"
scripts/deploy.sh "$VERSION"
echo "Fertig: https://github.com/$REPO/releases/tag/$TAG"
