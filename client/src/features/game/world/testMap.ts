import { GroundKind } from "@/generated/browsergame/v1/game_pb";
import type { AreaMap } from "./areaMap";

/** Zeichen der Testkarten, wie in meadow.json. */
export const TEST_LEGEND: Readonly<Record<string, GroundKind>> = {
  ".": GroundKind.GRASS,
  "=": GroundKind.PATH,
  "~": GroundKind.WATER,
  "^": GroundKind.MOUNTAIN,
};

/** Karte aus Zeichenzeilen (oben nach unten), tileSize 16; unbekanntes Zeichen → Error. */
export function testMap(
  rows: readonly string[],
  originTx = 0,
  originTy = 0,
  legend = TEST_LEGEND,
): AreaMap {
  const width = rows[0]?.length ?? 0;
  const height = rows.length;
  const ground = new Uint8Array(width * height);
  rows.forEach((row, y) => {
    if (row.length !== width)
      throw new Error(
        `Zeile ${String(y)} hat Länge ${String(row.length)}, erwartet ${String(width)}`,
      );
    row.split("").forEach((char, x) => {
      const kind = legend[char];
      if (kind === undefined)
        throw new Error(`Unbekanntes Zeichen "${char}" in Zeile ${String(y)}`);
      ground[y * width + x] = kind;
    });
  });
  return { tileSize: 16, width, height, originTx, originTy, ground };
}
