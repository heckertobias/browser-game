---
name: spec
description: Übersicht über den aktuellen Stand der Specs (Status, Task-Fortschritt, nächster Schritt, Unstimmigkeiten). Nutzen bei "/spec", "wo stehen wir", "was ist als Nächstes dran".
argument-hint: "[NNN]"
allowed-tools: Bash(.claude/skills/spec/status.sh)
---

# Spec-Stand

Rohdaten, eben erhoben:

!`.claude/skills/spec/status.sh`

## Auftrag

Gib eine kurze Übersicht auf Deutsch. Lies dafür keine weiteren Dateien, außer ein Punkt unten verlangt es.

1. **Tabelle** in Roadmap-Reihenfolge: Spec · Status · Tasks (erledigt/gesamt) · nächster offener Task (nur Kennung + Stichwort).
2. **Gerade in Arbeit:** Spec mit Status `in arbeit`, ihr nächster offener Task, Teilstände.
3. **Als Nächstes dran:** Specs im Status `bereit`, deren Voraussetzungen alle `umgesetzt` sind. Fehlt `plan.md` oder `tasks.md`, ist der nächste Schritt Planen bzw. Zerlegen, nicht Coden.
4. **Unstimmigkeiten** – nur nennen, wenn vorhanden:
   - Status in `spec.md` weicht von der Roadmap in `specs/README.md` ab.
   - „Setzt voraus“ in `spec.md` weicht von der Roadmap ab.
   - Alle Tasks erledigt, Status aber nicht `umgesetzt`.
   - Status `bereit` oder weiter, aber offene Fragen > 0.
   - Tasks abgehakt, obwohl der Status `bereit` ist (Arbeit begonnen ohne Statuswechsel).
5. Uncommittete Änderungen unter `specs/` oder im Code kurz erwähnen.

Kurz halten. Keine Wiederholung der Rohdaten.

Wenn ein Argument übergeben wurde (`$ARGUMENTS`), z. B. `003`: zusätzlich `spec.md`, `plan.md` und `tasks.md` dieser Spec lesen und
für sie ausführlicher berichten – welche Akzeptanzkriterien durch erledigte Tasks abgedeckt sind, welche noch offen sind.

Nichts ändern. Der Skill ist nur lesend.
