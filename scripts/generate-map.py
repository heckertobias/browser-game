#!/usr/bin/env python3
"""Erzeugt die Karte des ersten Gebiets (018, Plan-Entscheidung 11) einmal nach Schema. Nur Standardbibliothek.

Aufruf aus dem Repo-Wurzelverzeichnis: scripts/generate-map.py

Danach ist meadow.json die Wahrheit. Wer die Datei von Hand ändert, ruft dieses Skript nicht mehr auf: Es überschreibt
die Datei ohne Rückfrage.
"""

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUTPUT = ROOT / "server/BrowserGame.Server/Features/Game/Map/Maps/meadow.json"

TILE_SIZE = 16
# 10 × 8 Bildschirme zu 640 × 360 Weltpixeln (ADR 0010).
WIDTH = 10 * 640 // TILE_SIZE
HEIGHT = 8 * 360 // TILE_SIZE
# Startpunkt (Welt 0, 0) in der Mitte; er liegt auf der Kachelecke, um die das Wegekreuz zentriert ist.
ORIGIN = (WIDTH // 2, HEIGHT // 2)
MOUNTAIN_BAND = 4
PATH_WIDTH = 2
# Graskacheln zwischen Wegende und Berg bzw. See (Plan-Entscheidung 5 verlangt mindestens eine).
MOUNTAIN_GAP = 2
LAKE_GAP = 1
LAKE_CENTER = (300, 45)
LAKE_RADII = (36, 18)

GRASS, PATH, WATER, MOUNTAIN = ".", "=", "~", "^"
LEGEND = {GRASS: "grass", PATH: "path", WATER: "water", MOUNTAIN: "mountain"}


def fill(grid: list[list[str]], x0: int, y0: int, x1: int, y1: int, kind: str) -> None:
    """Rechteck [x0, x1) × [y0, y1)."""
    for y in range(y0, y1):
        for x in range(x0, x1):
            grid[y][x] = kind


def lake(grid: list[list[str]]) -> None:
    """Ellipse auf einem Raster aus 2×2-Blöcken; ganzzahlig gerechnet (Mittelpunkt des Blocks in der Ellipse)."""
    (cx, cy), (rx, ry) = LAKE_CENTER, LAKE_RADII
    for by in range(0, HEIGHT, 2):
        for bx in range(0, WIDTH, 2):
            dx, dy = bx + 1 - cx, by + 1 - cy
            if dx * dx * ry * ry + dy * dy * rx * rx <= rx * rx * ry * ry:
                fill(grid, bx, by, bx + 2, by + 2, WATER)


def paths(grid: list[list[str]]) -> None:
    ox, oy = ORIGIN
    half = PATH_WIDTH // 2
    start = MOUNTAIN_BAND + MOUNTAIN_GAP
    fill(grid, start, oy - half, WIDTH - start, oy - half + PATH_WIDTH, PATH)
    fill(grid, ox - half, start, ox - half + PATH_WIDTH, HEIGHT - start, PATH)

    # Vom Ost–West-Weg nach Norden bis ans Seeufer.
    x0 = LAKE_CENTER[0] - half
    x1 = x0 + PATH_WIDTH
    shore = max(y for y in range(HEIGHT) for x in range(x0 - LAKE_GAP, x1 + LAKE_GAP) if grid[y][x] == WATER)
    fill(grid, x0, shore + LAKE_GAP + 1, x1, oy - half, PATH)


def main() -> int:
    grid = [[MOUNTAIN] * WIDTH for _ in range(HEIGHT)]
    fill(grid, MOUNTAIN_BAND, MOUNTAIN_BAND, WIDTH - MOUNTAIN_BAND, HEIGHT - MOUNTAIN_BAND, GRASS)
    lake(grid)
    paths(grid)

    data = {
        "format": 1,
        "name": "meadow",
        "tileSize": TILE_SIZE,
        "origin": {"x": ORIGIN[0], "y": ORIGIN[1]},
        "legend": LEGEND,
        "rows": ["".join(row) for row in grid],
    }
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    print(f"{OUTPUT.relative_to(ROOT)}: {WIDTH} × {HEIGHT} Kacheln")
    return 0


if __name__ == "__main__":
    sys.exit(main())
