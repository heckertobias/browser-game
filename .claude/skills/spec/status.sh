#!/usr/bin/env bash
# Rohdaten für /spec: je Spec Status, Abhängigkeiten, offene Fragen, Task-Fortschritt.
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"

for dir in specs/[0-9][0-9][0-9]-*/; do
  spec="${dir}spec.md"
  tasks="${dir}tasks.md"
  [ -f "$spec" ] || continue

  title=$(head -1 "$spec" | sed 's/^# *//')
  status=$(grep -m1 '^\*\*Status:\*\*' "$spec" | sed -E 's/^\*\*Status:\*\* *//; s/ *<!--.*//')
  requires=$(grep -m1 '^Setzt voraus:' "$spec" | sed -E 's/^Setzt voraus: *//; s/\]\([^)]*\)/]/g; s/ · Voraussetzung für:.*//' || true)
  questions=$(awk '/^## Offene Fragen/{f=1;next} /^## /{f=0} f && /^[-*] /' "$spec" | wc -l | tr -d ' ')

  echo "## $title"
  echo "Status: $status"
  echo "Setzt voraus: ${requires:-–}"
  echo "Offene Fragen: $questions"
  echo "Plan: $([ -f "${dir}plan.md" ] && echo ja || echo nein)"

  if [ -f "$tasks" ]; then
    done=$(grep -cE '^\s*- \[[xX]\]' "$tasks" || true)
    total=$(grep -cE '^\s*- \[[ xX]\]' "$tasks" || true)
    echo "Tasks: $done/$total erledigt"
    grep -E '^\s*- \[ \]' "$tasks" | sed -E 's/^\s*- \[ \] */  offen: /' | cut -c1-160 || true
  else
    echo "Tasks: keine tasks.md"
  fi
  echo
done

echo "## Roadmap (specs/README.md)"
awk '/^## Roadmap/{f=1;next} /^## /{f=0} f && /^\|/' specs/README.md
echo
echo "## Letzte Commits"
git log --oneline -8
echo
echo "## Arbeitsbaum"
git status --short
