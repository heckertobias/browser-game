#!/usr/bin/env python3
"""Prüft Markdown-Links und die Regeln für specs/ (specs/README.md). Nur Standardbibliothek.

Aufruf aus dem Repo-Wurzelverzeichnis: scripts/check-docs.py
"""

import os
import re
import subprocess
import sys
import unicodedata
from pathlib import Path
from urllib.parse import unquote

ROOT = Path(__file__).resolve().parent.parent
STATUSES = ["entwurf", "bereit", "in arbeit", "umgesetzt"]

errors: list[str] = []


def error(path: Path, line: int | None, message: str) -> None:
    location = str(path.relative_to(ROOT)) + (f":{line}" if line else "")
    errors.append(f"{location}: {message}")


def markdown_files() -> list[Path]:
    out = subprocess.run(["git", "ls-files", "-z", "*.md"], cwd=ROOT, capture_output=True, check=True, text=True)
    return [ROOT / p for p in out.stdout.split("\0") if p]


def strip_code(text: str) -> list[str]:
    """Zeilen ohne Codeblöcke und Inline-Code; Zeilennummern bleiben erhalten."""
    lines, fence = [], None
    for line in text.splitlines():
        match = re.match(r"\s*(`{3,}|~{3,})", line)
        if fence:
            if match and match.group(1)[0] == fence[0] and len(match.group(1)) >= len(fence):
                fence = None
            lines.append("")
        elif match:
            fence = match.group(1)
            lines.append("")
        else:
            lines.append(re.sub(r"(`+).*?\1", "", line))
    return lines


def github_slug(heading: str) -> str:
    text = re.sub(r"<[^>]+>", "", heading)
    text = re.sub(r"\[([^\]]*)\]\([^)]*\)", r"\1", text)
    text = text.strip().lower()
    text = "".join(c for c in text if c in "-_ " or unicodedata.category(c)[0] in "LN")
    return text.replace(" ", "-")


def anchors(path: Path, cache: dict[Path, set[str]]) -> set[str]:
    if path not in cache:
        seen: dict[str, int] = {}
        result = set()
        for line in strip_code(path.read_text(encoding="utf-8")):
            match = re.match(r"#{1,6}\s+(.*?)\s*#*\s*$", line)
            if not match:
                continue
            slug = github_slug(match.group(1))
            count = seen.get(slug, 0)
            seen[slug] = count + 1
            result.add(slug if count == 0 else f"{slug}-{count}")
        cache[path] = result
    return cache[path]


LINK = re.compile(r"!?\[[^\]]*\]\(\s*<?([^)\s>]+)>?(?:\s+\"[^\"]*\")?\s*\)")


def check_links(files: list[Path]) -> None:
    cache: dict[Path, set[str]] = {}
    for path in files:
        for number, line in enumerate(strip_code(path.read_text(encoding="utf-8")), start=1):
            for target in LINK.findall(line):
                if re.match(r"[a-z][a-z0-9+.-]*:", target, re.IGNORECASE):
                    continue
                file_part, _, anchor = target.partition("#")
                resolved = (path.parent / unquote(file_part)).resolve() if file_part else path
                if not resolved.exists():
                    error(path, number, f"Link ins Leere: {target}")
                elif anchor and resolved.suffix == ".md" and unquote(anchor).lower() not in anchors(resolved, cache):
                    error(path, number, f"Anker fehlt: {target}")


def section(text: str, heading_pattern: str) -> str | None:
    match = re.search(rf"^## {heading_pattern}.*?$(.*?)(?=^## |\Z)", text, re.MULTILINE | re.DOTALL)
    return match.group(1) if match else None


def expand_ids(text: str) -> set[str]:
    """`AK-1, AK-3–5, 4b` → {"1", "3", "4", "5", "4b"}."""
    ids = set()
    for start, end in re.findall(r"(?:AK-)?(\d+[a-z]?)(?:\s*[–-]\s*(?:AK-)?(\d+[a-z]?))?", text):
        if end and start.isdigit() and end.isdigit():
            ids.update(str(n) for n in range(int(start), int(end) + 1))
        else:
            ids.update(i for i in (start, end) if i)
    return ids


def spec_numbers(text: str) -> set[str]:
    # „003 bis T7“ ist nur ein Teil einer Spec und steht in spec.md nicht unter „Setzt voraus“.
    return set(re.findall(r"\b(\d{3})\b(?!\s+bis\s+T)", text))


def roadmap() -> dict[str, tuple[str, set[str], int]]:
    path = ROOT / "specs/README.md"
    rows = {}
    for number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), start=1):
        match = re.match(r"\|[^|]*\|\s*\[(\d{3})[^\]]*\]\(([^)]+)\)\s*\|([^|]*)\|([^|]*)\|", line)
        if match:
            spec, _, status, prereqs = match.groups()
            rows[spec] = (status.strip(), spec_numbers(prereqs), number)
    return rows


def check_spec(directory: Path, rows: dict[str, tuple[str, set[str], int]]) -> None:
    number = directory.name[:3]
    spec_path = directory / "spec.md"
    if not spec_path.exists():
        error(directory, None, "spec.md fehlt")
        return
    spec = spec_path.read_text(encoding="utf-8")

    match = re.search(r"^\*\*Status:\*\*\s*([^<\n]+?)\s*(?:<!--.*)?$", spec, re.MULTILINE)
    status = match.group(1).strip() if match else None
    if status not in STATUSES:
        error(spec_path, None, f"Status fehlt oder ungültig: {status!r} (erlaubt: {', '.join(STATUSES)})")
        return
    level = STATUSES.index(status)

    readme = ROOT / "specs/README.md"
    if number not in rows:
        error(readme, None, f"{number} fehlt in der Roadmap")
    else:
        roadmap_status, roadmap_prereqs, line = rows[number]
        if not roadmap_status.startswith(status):
            error(readme, line, f"{number}: Status in der Roadmap „{roadmap_status}“, in spec.md „{status}“")
        prereq_match = re.search(r"^(?:Setzt voraus|Baut auf):(.*?)(?=\n\s*\n|Voraussetzung für:|Berührt:|\Z)", spec, re.MULTILINE | re.DOTALL)
        # Eine Voraussetzung ist ein Link am Anfang eines „·“-Abschnitts; Links mitten im Satz sind nur Hinweise.
        segments = prereq_match.group(1).split("·") if prereq_match else []
        prereqs = {m.group(1) for s in segments if (m := re.match(r"\s*\[[^\]]*\]\(\.\./(\d{3})-", s))}
        if prereq_match and prereqs != roadmap_prereqs - {number}:
            error(readme, line, f"{number}: „Setzt voraus“ in der Roadmap {sorted(roadmap_prereqs)}, in spec.md {sorted(prereqs)}")

    criteria = set(re.findall(r"^\s*-\s*\*\*AK-(\d+[a-z]?):\*\*", spec, re.MULTILINE))
    questions = section(spec, "Offene Fragen")
    if level >= STATUSES.index("bereit"):
        if questions is None:
            error(spec_path, None, "Abschnitt „Offene Fragen“ fehlt")
        elif re.search(r"^\s*[-*]\s", questions, re.MULTILINE):
            error(spec_path, None, f"Status „{status}“, aber noch offene Fragen")
        if not criteria:
            error(spec_path, None, f"Status „{status}“, aber keine Akzeptanzkriterien (`- **AK-n:**`)")

    plan_path, tasks_path = directory / "plan.md", directory / "tasks.md"
    if level >= STATUSES.index("in arbeit"):
        for required in (plan_path, tasks_path):
            if not required.exists():
                error(directory, None, f"Status „{status}“, aber {required.name} fehlt")

    if plan_path.exists():
        coverage = section(plan_path.read_text(encoding="utf-8"), r".*Abdeckung")
        if coverage is None:
            error(plan_path, None, "Abschnitt mit der AK-Abdeckung fehlt")
        else:
            covered = set()
            for row in re.findall(r"^\|([^|\n]*)\|", coverage, re.MULTILINE):
                covered |= expand_ids(row)
            for ak in sorted(criteria - covered, key=lambda a: (int(re.sub(r"\D", "", a)), a)):
                error(plan_path, None, f"AK-{ak} fehlt in der Abdeckung")
            for ak in sorted(covered - criteria):
                error(plan_path, None, f"AK-{ak} steht in der Abdeckung, gibt es in spec.md aber nicht")

    if not tasks_path.exists():
        return
    if not plan_path.exists():
        error(tasks_path, None, "tasks.md ohne plan.md")
    tasks = []
    for line_number, line in enumerate(tasks_path.read_text(encoding="utf-8").splitlines(), start=1):
        match = re.match(r"\s*-\s*\[([ xX])\]\s*\*\*T(\d+)\*\*\s*(\(([^)]*)\))?", line)
        if not match:
            continue
        done, task, _, refs = match.groups()
        tasks.append((int(task), done != " ", line_number))
        for ak in expand_ids(refs or "") - criteria:
            error(tasks_path, line_number, f"T{task} verweist auf AK-{ak}, das es in spec.md nicht gibt")
    if not tasks:
        error(tasks_path, None, "keine Tasks (`- [ ] **Tn** …`)")
        return
    ids = [task for task, _, _ in tasks]
    if ids != list(range(1, len(ids) + 1)):
        error(tasks_path, None, f"Tasks nicht fortlaufend ab T1: {', '.join(f'T{i}' for i in ids)}")
    done = sum(1 for _, d, _ in tasks if d)
    if status == "umgesetzt" and done < len(tasks):
        error(tasks_path, None, f"Status „umgesetzt“, aber {len(tasks) - done} Tasks offen")
    if status != "umgesetzt" and done == len(tasks):
        error(tasks_path, None, f"alle Tasks erledigt, Status aber „{status}“")
    if level <= STATUSES.index("bereit") and done:
        error(tasks_path, None, f"Status „{status}“, aber {done} Tasks abgehakt (Status auf „in arbeit“ setzen)")


def main() -> int:
    check_links(markdown_files())
    rows = roadmap()
    directories = sorted(p for p in (ROOT / "specs").iterdir() if p.is_dir() and re.match(r"\d{3}-", p.name))
    for directory in directories:
        check_spec(directory, rows)
    known = {d.name[:3] for d in directories}
    for number, (_, _, line) in rows.items():
        if number not in known:
            error(ROOT / "specs/README.md", line, f"Roadmap nennt {number}, den Ordner gibt es nicht")

    for message in errors:
        print(f"::error::{message}" if "GITHUB_ACTIONS" in os.environ else message)
    print(f"{len(errors)} Fehler" if errors else "Doku in Ordnung")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
