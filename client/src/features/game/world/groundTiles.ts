import type { Vec } from "@/features/game/core/vector";
import { GroundKind } from "@/generated/browsergame/v1/game_pb";
import { kindAt, type AreaMap } from "./areaMap";
import { hash2 } from "./hash";

/** Anteil der Graskacheln mit einer Variante (Blumen, Büschel) in Promille. Darstellung, kein Spielwert (§8). */
export const GRASS_VARIANT_PERMILLE = 150;

/**
 * Übergangsformen von Weg und Wasser (018 Entscheidung 4). Der Name sagt, wo Gras liegt:
 * Kante `n` = Gras im Norden; Außenecke `ne` = Gras im Norden und Osten; Innenecke `inner-ne` = nur diagonal im Nordosten.
 */
export const SHAPES = [
  "center",
  "n",
  "e",
  "s",
  "w",
  "ne",
  "nw",
  "se",
  "sw",
  "inner-ne",
  "inner-nw",
  "inner-se",
  "inner-sw",
] as const;
export type Shape = (typeof SHAPES)[number];

/**
 * Form der Kachel aus ihren acht Nachbarn; null, wenn keine der 13 Formen passt (z. B. ein Kachel breiter Zipfel).
 * Gleich = Nachbar hat dieselbe Art; alles andere zählt als Gras.
 */
export function edgeShape(map: AreaMap, tx: number, ty: number): Shape | null {
  const kind = kindAt(map, tx, ty);
  const same = (dx: number, dy: number): boolean =>
    kindAt(map, tx + dx, ty + dy) === kind;
  const n = same(0, -1);
  const e = same(1, 0);
  const s = same(0, 1);
  const w = same(-1, 0);
  const ne = same(1, -1);
  const nw = same(-1, -1);
  const se = same(1, 1);
  const sw = same(-1, 1);

  if (n && e && s && w) {
    const missing: Shape[] = [];
    if (!ne) missing.push("inner-ne");
    if (!nw) missing.push("inner-nw");
    if (!se) missing.push("inner-se");
    if (!sw) missing.push("inner-sw");
    if (missing.length === 0) return "center";
    return missing.length === 1 ? (missing[0] ?? null) : null;
  }
  if (!n && e && s && w) return se && sw ? "n" : null;
  if (n && e && !s && w) return ne && nw ? "s" : null;
  if (n && !e && s && w) return nw && sw ? "e" : null;
  if (n && e && s && !w) return ne && se ? "w" : null;
  if (!n && !e && s && w) return sw ? "ne" : null;
  if (!n && e && s && !w) return se ? "nw" : null;
  if (n && !e && !s && w) return nw ? "se" : null;
  if (n && e && !s && !w) return ne ? "sw" : null;
  return null;
}

/**
 * Name des Bodenstücks der Weltkachel (Entscheidungen 4, 6). `variants` sind die Gras-Varianten aus dem Atlas,
 * sortiert; gerechnet wird in Weltkacheln.
 */
export function groundTile(
  map: AreaMap,
  tx: number,
  ty: number,
  variants: readonly string[],
): string {
  switch (kindAt(map, tx, ty)) {
    case GroundKind.PATH:
      return `path.${edgeShape(map, tx, ty) ?? "center"}`;
    case GroundKind.WATER:
      return `water.${edgeShape(map, tx, ty) ?? "center"}`;
    case GroundKind.MOUNTAIN:
      return "mountain";
    default:
      if (
        variants.length === 0 ||
        hash2(tx, ty) % 1000 >= GRASS_VARIANT_PERMILLE
      )
        return "grass";
      return variants[hash2(ty, tx) % variants.length] ?? "grass";
  }
}

/** Sichtbare Weltkacheln, beide Grenzen eingeschlossen (Entscheidung 7). */
export interface TileRange {
  tx0: number;
  ty0: number;
  tx1: number;
  ty1: number;
}

export function visibleTiles(
  origin: Vec,
  viewWidth: number,
  viewHeight: number,
  tileSize: number,
): TileRange {
  return {
    tx0: Math.floor(origin.x / tileSize),
    ty0: Math.floor(origin.y / tileSize),
    tx1: Math.floor((origin.x + viewWidth) / tileSize),
    ty1: Math.floor((origin.y + viewHeight) / tileSize),
  };
}

/** Alle Namen, die groundTile ohne Gras-Varianten liefern kann; der Atlas muss sie enthalten (Varianten kommen aus ihm). */
export const GROUND_ENTRIES: readonly string[] = [
  "grass",
  "mountain",
  ...SHAPES.map((shape) => `path.${shape}`),
  ...SHAPES.map((shape) => `water.${shape}`),
];
