#!/usr/bin/env bash
# Erzeugt eine Migration für beide Datenbanken (ADR 0004) und den gewählten Kontext (ADR 0006).
# Aufruf: scripts/add-migration.sh <Name> [game|admin]   (Standard: game)
set -euo pipefail
NAME="${1:?Migrationsname angeben, z. B. AddHouse}"
TARGET="${2:-game}"
case "$TARGET" in
  game) CONTEXT="GameDbContext"; DIR="Migrations" ;;
  admin) CONTEXT="AdminDbContext"; DIR="Migrations/Admin" ;;
  *) echo "Kontext muss game oder admin sein" >&2; exit 1 ;;
esac
cd "$(git rev-parse --show-toplevel)/server"
dotnet tool restore >/dev/null
for provider in Sqlite Postgres; do
  dotnet ef migrations add "$NAME" \
    --project BrowserGame.Data --startup-project BrowserGame.Data \
    --context "${provider}${CONTEXT}" \
    --output-dir "$DIR/$provider"
done
rm -f BrowserGame.Data/design-time.db
