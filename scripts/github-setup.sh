#!/usr/bin/env bash
# Repo-Härtung für GitHub – idempotent, kann jederzeit erneut ausgeführt werden.
# Begründung der Einstellungen: docs/decisions/0002-repo-und-ci-cd.md und 0009-spec-branches-rc-und-deploy-per-api.md
#
# Voraussetzung: gh ist eingeloggt (Scope `repo`), ausführende Person ist Repo-Admin.
# Nicht per API setzbar (manuell): Settings → General → Features → Issues →
#   "Creation allowed by: Collaborators only".
set -euo pipefail

REPO="${1:-heckertobias/browser-game}"
api() { gh api -H "X-GitHub-Api-Version: 2022-11-28" "$@"; }

echo "== Features & Merge"
api -X PATCH "repos/$REPO" --silent --input - <<'JSON'
{
  "has_wiki": false,
  "has_projects": false,
  "has_discussions": false,
  "has_issues": true,
  "has_pull_requests": true,
  "pull_request_creation_policy": "collaborators_only",
  "allow_squash_merge": true,
  "allow_merge_commit": true,
  "allow_rebase_merge": false,
  "allow_auto_merge": true,
  "allow_update_branch": true,
  "delete_branch_on_merge": true,
  "squash_merge_commit_title": "PR_TITLE",
  "squash_merge_commit_message": "PR_BODY",
  "merge_commit_title": "PR_TITLE",
  "merge_commit_message": "PR_BODY"
}
JSON

echo "== Security"
api -X PUT "repos/$REPO/vulnerability-alerts" --silent
api -X PUT "repos/$REPO/automated-security-fixes" --silent
api -X PUT "repos/$REPO/private-vulnerability-reporting" --silent
api -X PATCH "repos/$REPO" --silent --input - <<'JSON'
{
  "security_and_analysis": {
    "secret_scanning": { "status": "enabled" },
    "secret_scanning_push_protection": { "status": "enabled" }
  }
}
JSON
# CodeQL Default Setup. Sprachen explizit, weil die Auto-Erkennung bei leerem Repo nichts fand.
CODEQL_LANGUAGES=(actions csharp javascript-typescript)
codeql_args=(-f state=configured -f query_suite=default)
for lang in "${CODEQL_LANGUAGES[@]}"; do codeql_args+=(-f "languages[]=$lang"); done
api -X PATCH "repos/$REPO/code-scanning/default-setup" --silent "${codeql_args[@]}"

# Veröffentlichte Releases (und ihr Tag) lassen sich danach nicht mehr ändern.
api -X PUT "repos/$REPO/immutable-releases" --silent

echo "== Actions"
api -X PUT "repos/$REPO/actions/permissions" --silent --input - <<'JSON'
{ "enabled": true, "allowed_actions": "selected", "sha_pinning_required": true }
JSON
api -X PUT "repos/$REPO/actions/permissions/selected-actions" --silent --input - <<'JSON'
{
  "github_owned_allowed": true,
  "verified_allowed": false,
  "patterns_allowed": [
    "dependabot/fetch-metadata@*",
    "bufbuild/buf-action@*",
    "docker/setup-buildx-action@*",
    "docker/login-action@*",
    "docker/metadata-action@*",
    "docker/build-push-action@*",
    "aquasecurity/trivy-action@*",
    "aquasecurity/setup-trivy@*"
  ]
}
JSON
api -X PUT "repos/$REPO/actions/permissions/workflow" --silent --input - <<'JSON'
{ "default_workflow_permissions": "read", "can_approve_pull_request_reviews": false }
JSON

echo "== Rulesets"
upsert_ruleset() {
  local name="$1" body="$2" id
  id="$(api "repos/$REPO/rulesets" --jq ".[] | select(.name == \"$name\") | .id")"
  if [[ -n "$id" ]]; then
    api -X PUT "repos/$REPO/rulesets/$id" --silent --input - <<<"$body"
  else
    api -X POST "repos/$REPO/rulesets" --silent --input - <<<"$body"
  fi
  echo "   $name ok"
}

# Gilt für alle, auch Admins: main bleibt erhalten und signiert.
upsert_ruleset "main-integrity" '{
  "name": "main-integrity",
  "target": "branch",
  "enforcement": "active",
  "conditions": { "ref_name": { "include": ["~DEFAULT_BRANCH"], "exclude": [] } },
  "bypass_actors": [],
  "rules": [
    { "type": "deletion" },
    { "type": "non_fast_forward" },
    { "type": "required_signatures" }
  ]
}'

# Alle müssen per PR mit grüner CI; der Admin darf den PR-Zwang nur über einen PR umgehen (Bypass „pull_request“).
upsert_ruleset "main-via-pr-checks" '{
  "name": "main-via-pr-checks",
  "target": "branch",
  "enforcement": "active",
  "conditions": { "ref_name": { "include": ["~DEFAULT_BRANCH"], "exclude": [] } },
  "bypass_actors": [
    { "actor_id": 5, "actor_type": "RepositoryRole", "bypass_mode": "pull_request" }
  ],
  "rules": [
    { "type": "pull_request", "parameters": {
        "required_approving_review_count": 0,
        "dismiss_stale_reviews_on_push": false,
        "require_code_owner_review": false,
        "require_last_push_approval": false,
        "required_review_thread_resolution": false,
        "allowed_merge_methods": ["merge", "squash"]
    } },
    { "type": "required_status_checks", "parameters": {
        "strict_required_status_checks_policy": false,
        "do_not_enforce_on_create": false,
        "required_status_checks": [ { "context": "ci-ok", "integration_id": 15368 } ]
    } }
  ]
}'

# Release- und RC-Tags: gilt für alle – einmal gesetzt, nie verschoben oder gelöscht; nur signierte Commits.
upsert_ruleset "release-tags-immutable" '{
  "name": "release-tags-immutable",
  "target": "tag",
  "enforcement": "active",
  "conditions": { "ref_name": { "include": ["refs/tags/v*"], "exclude": [] } },
  "bypass_actors": [],
  "rules": [
    { "type": "update" },
    { "type": "deletion" },
    { "type": "required_signatures" }
  ]
}'

# Release- und RC-Tags anlegen darf nur der Admin (scripts/release.sh).
upsert_ruleset "release-tags-admin-only" '{
  "name": "release-tags-admin-only",
  "target": "tag",
  "enforcement": "active",
  "conditions": { "ref_name": { "include": ["refs/tags/v*"], "exclude": [] } },
  "bypass_actors": [
    { "actor_id": 5, "actor_type": "RepositoryRole", "bypass_mode": "always" }
  ],
  "rules": [
    { "type": "creation" }
  ]
}'

echo "Fertig. Manuell prüfen: Issues → 'Collaborators only' (siehe Kopf dieses Skripts)."
