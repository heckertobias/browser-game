#!/usr/bin/env bash
# Rollt eine veröffentlichte Version über die Arcane-API aus (ADR 0009):
#   Compose des Tags + .env (nur GAME_VERSION ersetzt) hochladen, dann Projekt neu starten (pull + up).
#   Git Sync ist im Arcane-Projekt abgeschaltet.
#
# Aufruf: scripts/deploy.sh [--dry-run|--check] VERSION     (z. B. 0.3.0 oder 0.3.0-rc.1; Rollback: ältere Version)
#   --dry-run  ohne Key und ohne API-Aufruf: prüft Image und Compose, zeigt die geplanten Anfragen
#              (--dry-run --unreleased: nur für release.sh; überspringt Image und Tag, nimmt die Compose aus HEAD)
#   --check    nur lesend: zeigt Projekt, Dienste und heutige GAME_VERSION (ohne ARCANE_PROJECT_ID: listet die
#              Projekte und endet mit Fehler)
#
# Konfiguration (Umgebung hat Vorrang, sonst die Datei ${ARCANE_CONFIG:-~/.config/browser-game/arcane.env}, chmod 600):
#   ARCANE_URL         z. B. https://arcane.example.internal
#   ARCANE_API_KEY     API-Key mit projects:read, projects:update, projects:deploy
#   ARCANE_ENV_ID      Arcane-Umgebung (Standard 0)
#   ARCANE_PROJECT_ID  ID des Projekts (--check ohne ID listet die Projekte)
#   ARCANE_CA_CERT     optional: CA-Zertifikat für eine interne CA
# Die Datei wird zeilenweise als NAME=WERT gelesen (optional mit export davor, Wert optional in "…" oder '…'),
# nicht ausgeführt. ARCANE_CA_CERT aus der Datei muss ein absoluter Pfad sein (aus der Umgebung auch relativ zum
# Aufrufverzeichnis, ebenso ARCANE_CONFIG). Ein Key mit " oder \ wird abgelehnt.
set -euo pipefail

IMAGE_REPOS=("heckertobias/browser-game" "heckertobias/browser-game-admin")
COMPOSE="deploy/compose.yaml"

die() { echo "Fehler: $*" >&2; exit 1; }
warn() { echo "Warnung: $*" >&2; }

MODE="deploy"
UNRELEASED=0
case "${1:-}" in
  --dry-run)
    MODE="dry-run"; shift
    if [[ "${1:-}" == "--unreleased" ]]; then UNRELEASED=1; shift; fi ;;
  --check) MODE="check"; shift ;;
esac
VERSION="${1:-}"
if [[ "$MODE" == "check" && -z "$VERSION" ]]; then
  VERSION="-"
else
  [[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-rc\.[0-9]+)?$ ]] || die "Aufruf: scripts/deploy.sh [--dry-run|--check] X.Y.Z[-rc.N]"
fi
TAG="v$VERSION"

# Immer im Repo dieses Skripts arbeiten, nicht im aktuellen Verzeichnis
# Relative Pfade aus der Umgebung beziehen sich auf das Aufrufverzeichnis, daher vor dem cd auflösen
[[ -z "${ARCANE_CONFIG:-}" || "$ARCANE_CONFIG" == /* ]] || ARCANE_CONFIG="$PWD/$ARCANE_CONFIG"
[[ -z "${ARCANE_CA_CERT:-}" || "$ARCANE_CA_CERT" == /* ]] || ARCANE_CA_CERT="$PWD/$ARCANE_CA_CERT"
root="$(git -C "$(dirname "${BASH_SOURCE[0]}")" rev-parse --show-toplevel)" || die "Repo des Skripts nicht gefunden (Aufruf über Symlink?)"
cd "$root"

# --- Konfiguration -----------------------------------------------------------
CONFIG_FILE="${ARCANE_CONFIG:-${XDG_CONFIG_HOME:-$HOME/.config}/browser-game/arcane.env}"
if [[ -f "$CONFIG_FILE" ]]; then
  if [[ "$(uname)" == "Darwin" ]]; then mode="$(stat -f %Lp "$CONFIG_FILE")"; else mode="$(stat -c %a "$CONFIG_FILE")"; fi
  [[ "$mode" == "600" ]] || warn "$CONFIG_FILE hat Rechte $mode, erwartet 600 (chmod 600)"
  while IFS= read -r line || [[ -n "$line" ]]; do
    line="${line%$'\r'}"
    [[ "$line" =~ ^[[:space:]]*(export[[:space:]]+)?(ARCANE_URL|ARCANE_API_KEY|ARCANE_ENV_ID|ARCANE_PROJECT_ID|ARCANE_CA_CERT)=(.*)$ ]] || continue
    name="${BASH_REMATCH[2]}"
    value="${BASH_REMATCH[3]}"
    case "$value" in
      \"*\") value="${value#\"}"; value="${value%\"}" ;;
      \'*\') value="${value#\'}"; value="${value%\'}" ;;
    esac
    [[ -n "${!name:-}" ]] || printf -v "$name" '%s' "$value"
  done < "$CONFIG_FILE"
fi
ARCANE_URL="${ARCANE_URL:-}"
ARCANE_API_KEY="${ARCANE_API_KEY:-}"
ARCANE_ENV_ID="${ARCANE_ENV_ID:-0}"
ARCANE_PROJECT_ID="${ARCANE_PROJECT_ID:-}"
ARCANE_CA_CERT="${ARCANE_CA_CERT:-}"
[[ -z "$ARCANE_CA_CERT" || "$ARCANE_CA_CERT" == /* ]] || die "ARCANE_CA_CERT in der Datei muss ein absoluter Pfad sein"
case "$ARCANE_URL" in ""|http://*|https://*) ;; *) ARCANE_URL="https://$ARCANE_URL" ;; esac
ARCANE_URL="${ARCANE_URL%/}"
case "$ARCANE_API_KEY" in
  *[\"\\]*|*$'\n'*|*$'\r'*) die "ARCANE_API_KEY enthält \", \\ oder einen Zeilenumbruch – nicht unterstützt" ;;
esac

missing=""
[[ -n "$ARCANE_URL" ]] || missing="$missing ARCANE_URL"
[[ -n "$ARCANE_API_KEY" ]] || missing="$missing ARCANE_API_KEY"
[[ -n "$ARCANE_PROJECT_ID" || "$MODE" == "check" ]] || missing="$missing ARCANE_PROJECT_ID"
if [[ -n "$missing" ]]; then
  if [[ "$MODE" == "dry-run" ]]; then
    warn "Konfiguration unvollständig (fehlt:$missing) – für den echten Lauf nötig, siehe Kopf von scripts/deploy.sh"
  else
    die "Konfiguration unvollständig (fehlt:$missing), siehe Kopf von scripts/deploy.sh"
  fi
fi

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
BASE="${ARCANE_URL:-<ARCANE_URL>}/api/environments/$ARCANE_ENV_ID/projects"

# Der Key steht nur in einer Config, die curl über einen Dateideskriptor liest, nie in der Argumentliste.
# $1 Methode, $2 Pfad ab /projects, weitere Argumente gehen an curl. Antwort in $WORK/body, Status auf stdout.
api() {
  local method="$1" path="$2"; shift 2
  local extra=()
  [[ -z "$ARCANE_CA_CERT" ]] || extra=(--cacert "$ARCANE_CA_CERT")
  curl -sS -o "$WORK/body" -w '%{http_code}' -X "$method" ${extra[@]+"${extra[@]}"} \
    --config <(printf 'header = "X-Api-Key: %s"\n' "$ARCANE_API_KEY") "$@" "$BASE$path"
}
need_2xx() { # $1 Status, $2 Beschreibung
  [[ "$1" =~ ^2[0-9][0-9]$ ]] || die "$2 fehlgeschlagen (HTTP $1): $(head -c 500 "$WORK/body")"
}

# --- --check: nur lesen ------------------------------------------------------
if [[ "$MODE" == "check" ]]; then
  if [[ -z "$ARCANE_PROJECT_ID" ]]; then
    status="$(api GET "")"; need_2xx "$status" "Projekte lesen"
    echo "Projekte in Umgebung $ARCANE_ENV_ID (ARCANE_PROJECT_ID setzen):"
    jq -r '(.data // .) | (if type == "array" then . else (.items // .data // []) end) | .[] | "  \(.id)\t\(.name)"' "$WORK/body"
    die "ARCANE_PROJECT_ID setzen (siehe Liste oben)"
  fi
  status="$(api GET "/$ARCANE_PROJECT_ID")"; need_2xx "$status" "Projekt lesen"
  jq -r '(.data // .) | "Projekt: \(.name // "?")  Status: \(.status // "?")"' "$WORK/body"
  status="$(api GET "/$ARCANE_PROJECT_ID/runtime" || true)"
  if [[ "$status" =~ ^2[0-9][0-9]$ ]]; then
    jq -r '(.data // .) | "Dienste: \([.runtimeServices // [] | .[] | "\(.name // "?") (\(.status // "?")/\(.health // "?"))"] | join(", "))"' "$WORK/body" || echo "Dienste: (nicht lesbar)"
  else
    echo "Dienste: (Laufzeit nicht lesbar, HTTP ${status:-keine Antwort})"
  fi
  status="$(api GET "/$ARCANE_PROJECT_ID/compose")"; need_2xx "$status" "Compose lesen"
  jq -e '(.data // .) | has("envContent")' "$WORK/body" >/dev/null || die "Antwort ohne envContent: $(head -c 300 "$WORK/body")"
  jq -j '(.data // .) | .envContent // ""' "$WORK/body" > "$WORK/env.current"
  current="$(sed -n 's/^GAME_VERSION=//p' "$WORK/env.current" | tail -n 1)"
  echo "GAME_VERSION in der .env: ${current:-(nicht gesetzt)}"
  echo "Key und Verbindung sind in Ordnung."
  exit 0
fi

# --- 1. Image prüfen ---------------------------------------------------------
# Anonym (öffentliche Pakete), unabhängig von Docker.
if [[ "$UNRELEASED" == 1 ]]; then
  echo "   Images entstehen erst beim Release – GHCR-Prüfung übersprungen"
else
for repo in "${IMAGE_REPOS[@]}"; do
  token="$(curl -fsSL "https://ghcr.io/token?scope=repository:$repo:pull" | python3 -I -c 'import json,sys; print(json.load(sys.stdin)["token"])')"
  curl -fsS -o /dev/null -I -H "Authorization: Bearer $token" \
    -H "Accept: application/vnd.oci.image.index.v1+json" \
    "https://ghcr.io/v2/$repo/manifests/$VERSION" || die "Image ghcr.io/$repo:$VERSION nicht in GHCR gefunden"
  echo "   Image ghcr.io/$repo:$VERSION ist veröffentlicht"
done
fi

# --- 2. Compose der Version ---------------------------------------------------
# shellcheck disable=SC2016  # ${GAME_VERSION} ist hier Suchtext
has_var() { [[ "$(grep -c '^[[:space:]]*image: .*\${GAME_VERSION' "$1")" -ge 2 ]]; }
if [[ "$UNRELEASED" == 1 ]]; then
  git show "HEAD:$COMPOSE" > "$WORK/compose.yaml" 2>/dev/null || die "$COMPOSE fehlt in HEAD"
  has_var "$WORK/compose.yaml" || die "$COMPOSE in HEAD hat keine \${GAME_VERSION} in den image-Zeilen"
  echo "   Compose aus HEAD (Version noch nicht getaggt)"
else
  git rev-parse -q --verify "refs/tags/$TAG" >/dev/null || die "Tag $TAG fehlt lokal (git fetch --tags)"
  git show "$TAG:$COMPOSE" > "$WORK/compose.yaml" 2>/dev/null || die "$COMPOSE fehlt in $TAG"
  if has_var "$WORK/compose.yaml"; then
    echo "   Compose aus $TAG"
  else
    warn "$TAG hat eine feste Version in $COMPOSE (vor ADR 0009) – nehme stattdessen $COMPOSE aus HEAD"
    git show "HEAD:$COMPOSE" > "$WORK/compose.yaml" 2>/dev/null || die "$COMPOSE fehlt in HEAD"
    has_var "$WORK/compose.yaml" || die "auch $COMPOSE in HEAD hat keine \${GAME_VERSION} in den image-Zeilen"
  fi
fi

# --- 3. .env ------------------------------------------------------------------
set_version() { # .env auf stdin → stdout: GAME_VERSION ersetzen oder anhängen, Rest unverändert
  awk -v v="GAME_VERSION=$VERSION" '
    /^GAME_VERSION=/ { if (!done) print v; done = 1; next }
    { print }
    END { if (!done) print v }'
}
if [[ "$MODE" == "dry-run" ]]; then
  printf 'VIRTUAL_HOST=a.invalid\nADMIN_VIRTUAL_HOST=b.invalid\n' > "$WORK/env.current"
  echo "   (Trockenlauf: Platzhalter-.env statt der aus Arcane)"
else
  status="$(api GET "/$ARCANE_PROJECT_ID/compose")"; need_2xx "$status" "Compose/.env lesen"
  jq -e '(.data // .) | has("envContent")' "$WORK/body" >/dev/null || die "Antwort ohne envContent: $(head -c 300 "$WORK/body")"
  jq -j '(.data // .) | .envContent // ""' "$WORK/body" > "$WORK/env.current"
fi
set_version < "$WORK/env.current" > "$WORK/env.new"

# --- Trockenlauf -------------------------------------------------------------
if [[ "$MODE" == "dry-run" ]]; then
  if command -v docker >/dev/null 2>&1 && docker compose version >/dev/null 2>&1; then
    if env GAME_VERSION="$VERSION" VIRTUAL_HOST=a.invalid ADMIN_VIRTUAL_HOST=b.invalid docker compose -f - config -q < "$WORK/compose.yaml"; then
      echo "   docker compose config: ok"
    else
      die "docker compose config meldet Fehler"
    fi
  else
    echo "   (docker compose nicht verfügbar – Compose nicht validiert)"
  fi
  echo "Geplante Anfragen (Key: ***):"
  echo "  GET  $BASE/${ARCANE_PROJECT_ID:-<ARCANE_PROJECT_ID>}/compose"
  echo "  PUT  $BASE/${ARCANE_PROJECT_ID:-<ARCANE_PROJECT_ID>}   composeContent ($(wc -l < "$WORK/compose.yaml" | tr -d ' ') Zeilen), envContent (GAME_VERSION=$VERSION)"
  echo "  POST $BASE/${ARCANE_PROJECT_ID:-<ARCANE_PROJECT_ID>}/redeploy"
  echo "Trockenlauf beendet, nichts geändert."
  exit 0
fi

# --- 4. Hochladen -------------------------------------------------------------
for name in VIRTUAL_HOST ADMIN_VIRTUAL_HOST; do
  grep -q "^$name=" "$WORK/env.new" || die "$name fehlt in der .env aus Arcane – nichts hochgeladen"
done
echo "== Compose und .env hochladen (GAME_VERSION=$VERSION)"
jq -n --rawfile c "$WORK/compose.yaml" --rawfile e "$WORK/env.new" '{composeContent: $c, envContent: $e}' > "$WORK/put.json"
status="$(api PUT "/$ARCANE_PROJECT_ID" -H 'Content-Type: application/json' --data-binary "@$WORK/put.json")"
need_2xx "$status" "Projekt aktualisieren"
[[ "$(jq -r 'if has("success") then .success else true end' "$WORK/body")" == "true" ]] || die "Projekt aktualisieren: $(head -c 500 "$WORK/body")"

# --- 5. Neu starten -----------------------------------------------------------
echo "== Neu starten (pull + up)"
extra=()
[[ -z "$ARCANE_CA_CERT" ]] || extra=(--cacert "$ARCANE_CA_CERT")
finished=0
http=""
while IFS= read -r line; do
  case "$line" in
    __HTTP__*) http="${line#__HTTP__}"; continue ;;
    "") continue ;;
  esac
  line="${line#data:}"
  parsed="$(printf '%s' "$line" | jq -r 'if (.error // "") != "" then "E\t\(.error | tostring)" elif .done == true then "D" else "L\t\(.log // . | tostring)" end' 2>/dev/null)" || parsed="L	$line"
  case "$parsed" in
    E*) die "Redeploy: ${parsed#E	}" ;;
    D) finished=1 ;;
    L*) echo "   ${parsed#L	}" ;;
  esac
done < <(curl -sS -N -X POST ${extra[@]+"${extra[@]}"} \
  --config <(printf 'header = "X-Api-Key: %s"\n' "$ARCANE_API_KEY") \
  -w '\n__HTTP__%{http_code}\n' "$BASE/$ARCANE_PROJECT_ID/redeploy")
[[ "$http" =~ ^2[0-9][0-9]$ ]] || die "Redeploy fehlgeschlagen (HTTP ${http:-keine Antwort})"
[[ "$finished" == 1 ]] || die "Redeploy endete ohne Abschlussmeldung (done)"

# --- 6. Status (best effort) ---------------------------------------------------
status="$(api GET "/$ARCANE_PROJECT_ID/runtime" || true)"
if [[ "$status" =~ ^2[0-9][0-9]$ ]]; then
  jq -r '(.data // .) | "Laufzeit: \(. | tostring | .[0:300])"' "$WORK/body" 2>/dev/null || true
fi
echo "Fertig: $VERSION ausgerollt."
