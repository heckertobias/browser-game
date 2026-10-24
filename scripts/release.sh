#!/usr/bin/env bash
# Release in zwei Schritten (ADR 0002):
#   1. Signierten Tag vX.Y.Z pushen → CI baut, scannt und veröffentlicht ghcr.io/…:X.Y.Z + :latest, legt GitHub Release an
#   2. Erst wenn das Image existiert: Version in deploy/compose.yaml setzen, committen, pushen → Arcane Git Sync deployt
#
# Aufruf: scripts/release.sh 0.2.0
set -euo pipefail

VERSION="${1:-}"
REPO="heckertobias/browser-game"
IMAGE_REPO="heckertobias/browser-game"
COMPOSE="deploy/compose.yaml"

die() { echo "Fehler: $*" >&2; exit 1; }

[[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || die "Version im Format X.Y.Z angeben, z. B. 0.2.0"
TAG="v$VERSION"

cd "$(git rev-parse --show-toplevel)"
[[ "$(git branch --show-current)" == "main" ]] || die "nur von main aus"
[[ -z "$(git status --porcelain)" ]] || die "Arbeitskopie nicht sauber"
git fetch --quiet origin main --tags
[[ "$(git rev-parse HEAD)" == "$(git rev-parse origin/main)" ]] || die "main ist nicht synchron mit origin/main"
git rev-parse -q --verify "refs/tags/$TAG" >/dev/null && die "Tag $TAG existiert bereits"

HEAD_SHA="$(git rev-parse HEAD)"
ci="$(gh run list --repo "$REPO" --workflow ci.yml --commit "$HEAD_SHA" --event push --json conclusion --jq '.[0].conclusion // ""')"
[[ "$ci" == "success" ]] || die "CI auf $HEAD_SHA ist nicht grün (Status: ${ci:-keiner})"

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

# Image anonym prüfen (öffentliches Paket), unabhängig von Docker.
token="$(curl -fsSL "https://ghcr.io/token?scope=repository:$IMAGE_REPO:pull" | python3 -I -c 'import json,sys; print(json.load(sys.stdin)["token"])')"
curl -fsS -o /dev/null -I -H "Authorization: Bearer $token" \
  -H "Accept: application/vnd.oci.image.index.v1+json" \
  "https://ghcr.io/v2/$IMAGE_REPO/manifests/$VERSION" || die "Image :$VERSION nicht in GHCR gefunden"
echo "   Image ghcr.io/$IMAGE_REPO:$VERSION ist veröffentlicht"

echo "== 2/2: $COMPOSE auf $VERSION setzen"
perl -pi -e "s{(image: ghcr\.io/\Q$IMAGE_REPO\E:)\S+}{\${1}$VERSION}" "$COMPOSE"
grep -q "image: ghcr.io/$IMAGE_REPO:$VERSION" "$COMPOSE" || die "Image-Zeile in $COMPOSE nicht gefunden"
git add "$COMPOSE"
git commit --quiet -m "chore(deploy): browser-game $VERSION ausrollen"
git push --quiet origin main

echo "Fertig: https://github.com/$REPO/releases/tag/$TAG – Arcane übernimmt beim nächsten Git Sync."
