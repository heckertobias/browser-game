// Karte des Gebiets aus dem Welcome (018), reine Logik.

import {
  GroundKind,
  type AreaMap as AreaMapMessage,
} from "@/generated/browsergame/v1/game_pb";

/** Felder der Karte aus dem Vertrag (Constitution §3: nicht nachgebaut, nur ausgewählt). */
export type AreaMap = Readonly<
  Pick<
    AreaMapMessage,
    "tileSize" | "width" | "height" | "originTx" | "originTy" | "ground"
  >
>;

/** Karte aus dem Welcome; null, wenn sie fehlt oder nicht zu ihren Maßen passt (dann bleibt das Gitter). */
export function areaMapFromProto(
  area: AreaMapMessage | undefined,
): AreaMap | null {
  if (area === undefined) return null;
  const { tileSize, width, height, originTx, originTy, ground } = area;
  if (
    tileSize === 0 ||
    width === 0 ||
    height === 0 ||
    ground.length !== width * height
  )
    return null;
  return { tileSize, width, height, originTx, originTy, ground };
}

/** Index = Wert im Vertrag. */
const KIND_BY_BYTE: readonly GroundKind[] = [
  GroundKind.UNSPECIFIED,
  GroundKind.GRASS,
  GroundKind.PATH,
  GroundKind.WATER,
  GroundKind.MOUNTAIN,
];

/** Bodenart der Weltkachel; außerhalb der Karte die nächste Randkachel (AK-5). */
export function kindAt(map: AreaMap, tx: number, ty: number): GroundKind {
  const mx = Math.min(Math.max(tx - map.originTx, 0), map.width - 1);
  const my = Math.min(Math.max(ty - map.originTy, 0), map.height - 1);
  return (
    KIND_BY_BYTE[map.ground[my * map.width + mx] ?? 0] ?? GroundKind.UNSPECIFIED
  );
}
