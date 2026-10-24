#!/usr/bin/env bash
# Release-Notes aus den Commits seit dem vorherigen v*-Tag, gruppiert nach Conventional-Commit-Typ.
# Aufruf: scripts/release-notes.sh v0.2.0   (gibt Markdown auf stdout aus; braucht die volle Git-Historie)
# Läuft auch mit dem Bash 3.2 von macOS (keine assoziativen Arrays).
set -euo pipefail

TAG="${1:?Tag angeben, z. B. v0.2.0}"
VERSION="${TAG#v}"
REPO_URL="https://github.com/heckertobias/browser-game"

previous="$(git describe --tags --abbrev=0 --match 'v*' "$TAG^" 2>/dev/null || true)"
range="${previous:+$previous..}$TAG"

title() {
  case "$1" in
    feat) echo "Neu" ;; fix) echo "Fehlerbehebungen" ;; perf) echo "Performance" ;; refactor) echo "Umbau" ;;
    docs) echo "Dokumentation & Specs" ;; test) echo "Tests" ;; ci) echo "CI/CD" ;; build) echo "Build" ;;
    deps) echo "Abhängigkeiten" ;; *) echo "Sonstiges" ;;
  esac
}

# Jede Zeile: <typ>\t<hash>\t<betreff>
classified="$(git log --no-merges --reverse --format=$'%h\t%s' "$range" | while IFS=$'\t' read -r hash subject; do
  # chore(deploy): Versions-Bumps aus release.sh gehören nicht in die Notes
  [[ "$subject" =~ ^chore\(deploy\): ]] && continue
  type="other"
  if [[ "$subject" =~ ^chore\(deps\) ]]; then
    type="deps"
  elif [[ "$subject" =~ ^(feat|fix|perf|refactor|docs|test|ci|build)(\([^\)]*\))?!?:\  ]]; then
    type="${BASH_REMATCH[1]}"
  fi
  printf '%s\t%s\t%s\n' "$type" "$hash" "$subject"
done)"

echo "Image: \`ghcr.io/heckertobias/browser-game:${VERSION}\` (amd64, arm64)"
for type in feat fix perf refactor docs test ci build deps other; do
  lines="$(printf '%s\n' "$classified" | awk -F'\t' -v t="$type" -v url="$REPO_URL" \
    '$1 == t { printf "- %s ([`%s`](%s/commit/%s))\n", $3, $2, url, $2 }')"
  [[ -n "$lines" ]] || continue
  printf '\n## %s\n\n%s\n' "$(title "$type")" "$lines"
done
if [[ -n "$previous" ]]; then
  printf '\n**Alle Änderungen:** %s/compare/%s...%s\n' "$REPO_URL" "$previous" "$TAG"
fi
