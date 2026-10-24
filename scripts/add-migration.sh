#!/usr/bin/env bash
# Erzeugt eine Migration für beide Datenbanken (ADR 0004).
# Aufruf: scripts/add-migration.sh <Name>
set -euo pipefail
NAME="${1:?Migrationsname angeben, z. B. AddHouse}"
cd "$(git rev-parse --show-toplevel)/server"
dotnet tool restore >/dev/null
for provider in Sqlite Postgres; do
  dotnet ef migrations add "$NAME" \
    --project BrowserGame.Server \
    --context "${provider}GameDbContext" \
    --output-dir "Data/Migrations/$provider"
done
