import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";
import { computeViewport } from "@/features/game/render/viewport";
import { GroundKind } from "@/generated/browsergame/v1/game_pb";
import type { AreaMap } from "./areaMap";
import {
  edgeShape,
  GRASS_VARIANT_PERMILLE,
  GROUND_ENTRIES,
  groundTile,
  SHAPES,
  visibleTiles,
} from "./groundTiles";
import { hash2 } from "./hash";
import { testMap } from "./testMap";

const RING = [
  "..........",
  ".========.",
  ".========.",
  ".========.",
  ".===..===.",
  ".===..===.",
  ".========.",
  ".========.",
  ".========.",
  "..........",
];
const WATER_RING = RING.map((row) => row.replaceAll("=", "~"));

const EXPECTED: readonly (readonly [number, number, string])[] = [
  [2, 2, "center"],
  [1, 1, "nw"],
  [8, 1, "ne"],
  [1, 8, "sw"],
  [8, 8, "se"],
  [4, 1, "n"],
  [1, 4, "w"],
  [8, 4, "e"],
  [4, 8, "s"],
  [3, 3, "inner-se"],
  [6, 3, "inner-sw"],
  [3, 6, "inner-ne"],
  [6, 6, "inner-nw"],
  [4, 3, "s"],
  [5, 6, "n"],
  [3, 4, "e"],
  [6, 5, "w"],
];

const SINGLE_ROWS = ["...", ".=.", "..."];
const LINE_ROWS = [".....", ".===.", "....."];
const VARIANTS = ["grass.1", "grass.2", "grass.3"];

describe("hash2 (018 AK-3)", () => {
  it("liefert feste Werte", () => {
    expect(hash2(0, 0)).toBe(0);
    expect(hash2(1, 0)).toBe(2163570162);
    expect(hash2(0, 1)).toBe(207396645);
    expect(hash2(-1, -1)).toBe(1932841317);
    expect(hash2(200, -90)).toBe(1425901981);
  });

  it("liefert ganze, unsignierte, stabile Werte", () => {
    for (const [a, b] of [
      [3, 4],
      [-7, 12],
      [100000, -100000],
      [-1, 0],
    ] as const) {
      const h = hash2(a, b);
      expect(Number.isInteger(h)).toBe(true);
      expect(h).toBeGreaterThanOrEqual(0);
      expect(h).toBeLessThan(2 ** 32);
      expect(hash2(a, b)).toBe(h);
    }
  });
});

describe("edgeShape (018 AK-1)", () => {
  it.each([
    ["Weg", RING],
    ["Wasser", WATER_RING],
  ])("erkennt alle 13 Formen im %s-Ring", (_name, rows) => {
    const map = testMap(rows);
    const results = EXPECTED.map(([tx, ty]) => edgeShape(map, tx, ty));
    expect(results).toEqual(EXPECTED.map(([, , shape]) => shape));
    expect(new Set(results).size).toBe(13);
  });

  it.each(["~", "^"])(
    "zählt eine andere Nicht-Gras-Art (%s) als Gras",
    (other) => {
      expect(
        edgeShape(testMap(Array<string>(4).fill(`===${other}`)), 2, 1),
      ).toBe("e");
    },
  );

  it("liefert null, wenn keine Form passt", () => {
    expect(edgeShape(testMap(SINGLE_ROWS), 1, 1)).toBeNull();
    expect(edgeShape(testMap(LINE_ROWS), 2, 1)).toBeNull();
  });

  it("setzt den Rand fort (AK-5)", () => {
    const path = testMap(["==", "=="]);
    expect(edgeShape(path, 0, 0)).toBe("center");
    expect(edgeShape(path, -5, -5)).toBe("center");
    const water = testMap(["..~~", "..~~"]);
    expect(edgeShape(water, 2, 0)).toBe("w");
    expect(edgeShape(water, 3, 0)).toBe("center");
    expect(edgeShape(water, 50, 0)).toBe("center");
    expect(edgeShape(water, 50, -50)).toBe("center");
  });
});

describe("groundTile (018 AK-1, AK-3)", () => {
  it.each([
    ["path", RING],
    ["water", WATER_RING],
  ])("wählt das %s-Stück je Form", (prefix, rows) => {
    const map = testMap(rows);
    for (const [tx, ty, shape] of EXPECTED)
      expect(groundTile(map, tx, ty, VARIANTS)).toBe(`${prefix}.${shape}`);
  });

  it("fällt ohne passende Form auf die Mitte zurück", () => {
    expect(groundTile(testMap(SINGLE_ROWS), 1, 1, VARIANTS)).toBe(
      "path.center",
    );
  });

  it("liefert für Berg mountain", () => {
    const map = testMap(["^"]);
    expect(groundTile(map, 0, 0, VARIANTS)).toBe("mountain");
    expect(groundTile(map, -7, 3, VARIANTS)).toBe("mountain");
  });

  it("liefert ohne Varianten nur grass", () => {
    const map = testMap(["."]);
    for (let ty = -50; ty < 50; ty++)
      for (let tx = -50; tx < 50; tx++)
        expect(groundTile(map, tx, ty, [])).toBe("grass");
  });

  it("streut die Varianten im geplanten Anteil", () => {
    const map = testMap(["."]);
    const seen = new Set<string>();
    let variant = 0;
    for (let ty = -50; ty < 50; ty++) {
      for (let tx = -50; tx < 50; tx++) {
        const name = groundTile(map, tx, ty, VARIANTS);
        seen.add(name);
        if (name !== "grass") {
          variant++;
          expect(VARIANTS).toContain(name);
        }
      }
    }
    const share = variant / 10000;
    expect(share).toBeGreaterThanOrEqual(GRASS_VARIANT_PERMILLE / 1000 - 0.03);
    expect(share).toBeLessThanOrEqual(GRASS_VARIANT_PERMILLE / 1000 + 0.03);
    for (const name of VARIANTS) expect(seen.has(name)).toBe(true);
  });

  it("ist deterministisch (AK-3)", () => {
    const a = testMap(RING);
    const b = testMap(RING);
    for (let ty = -3; ty < 13; ty++) {
      for (let tx = -3; tx < 13; tx++) {
        const first = groundTile(a, tx, ty, VARIANTS);
        expect(groundTile(b, tx, ty, VARIANTS)).toBe(first);
        expect(groundTile(a, tx, ty, VARIANTS)).toBe(first);
      }
    }
  });

  it("bindet Varianten an Weltkacheln", () => {
    const a = testMap(["."], 0, 0);
    const b = testMap(["."], 37, -12);
    for (let ty = 0; ty < 20; ty++) {
      for (let tx = 0; tx < 20; tx++)
        expect(groundTile(b, tx, ty, VARIANTS)).toBe(
          groundTile(a, tx, ty, VARIANTS),
        );
    }
  });

  it("behandelt ein unbekanntes Byte wie Gras", () => {
    const map: AreaMap = {
      tileSize: 16,
      width: 1,
      height: 1,
      originTx: 0,
      originTy: 0,
      ground: new Uint8Array([9]),
    };
    for (let tx = 0; tx < 50; tx++) {
      const name = groundTile(map, tx, 0, VARIANTS);
      expect(["grass", ...VARIANTS]).toContain(name);
    }
  });
});

describe("GROUND_ENTRIES (018 AK-1)", () => {
  it("enthält 28 eindeutige Namen", () => {
    expect(GROUND_ENTRIES).toHaveLength(28);
    expect(new Set(GROUND_ENTRIES).size).toBe(28);
    expect(GROUND_ENTRIES).toContain("grass");
    expect(GROUND_ENTRIES).toContain("mountain");
    for (const shape of SHAPES) {
      expect(GROUND_ENTRIES).toContain(`path.${shape}`);
      expect(GROUND_ENTRIES).toContain(`water.${shape}`);
    }
  });

  it("deckt alle Namen von groundTile ohne Varianten ab", () => {
    for (const rows of [RING, WATER_RING, SINGLE_ROWS, ["^"], ["."]]) {
      const map = testMap(rows);
      for (let ty = -2; ty < rows.length + 2; ty++) {
        for (let tx = -2; tx < (rows[0]?.length ?? 0) + 2; tx++) {
          expect(GROUND_ENTRIES).toContain(groundTile(map, tx, ty, []));
        }
      }
    }
  });
});

describe("visibleTiles (018 AK-1)", () => {
  it("rechnet vom Ursprung", () => {
    expect(visibleTiles({ x: 0, y: 0 }, 640, 360, 16)).toEqual({
      tx0: 0,
      ty0: 0,
      tx1: 40,
      ty1: 22,
    });
  });

  it("rundet bei negativem, gebrochenem Ursprung nach unten", () => {
    expect(visibleTiles({ x: -8.5, y: -20 }, 100, 50, 16)).toEqual({
      tx0: -1,
      ty0: -2,
      tx1: 5,
      ty1: 1,
    });
  });

  it.each([
    [1280, 720, 1],
    [1536, 730, 1.25],
    [412, 915, 2.625],
    [3440, 1440, 1],
  ] as const)("deckt das Sichtfeld %ix%i@%f ab", (cssWidth, cssHeight, dpr) => {
    const { viewWidth, viewHeight } = computeViewport(cssWidth, cssHeight, dpr);
    for (const origin of [
      { x: 0, y: 0 },
      { x: -3200.25, y: -1440.5 },
      { x: 17.75, y: -3.125 },
    ]) {
      const r = visibleTiles(origin, viewWidth, viewHeight, 16);
      expect(r.tx0 * 16).toBeLessThanOrEqual(origin.x);
      expect((r.tx1 + 1) * 16).toBeGreaterThanOrEqual(origin.x + viewWidth);
      expect(r.ty0 * 16).toBeLessThanOrEqual(origin.y);
      expect((r.ty1 + 1) * 16).toBeGreaterThanOrEqual(origin.y + viewHeight);
      expect(r.tx1 - r.tx0 + 1).toBeLessThanOrEqual(
        Math.floor(viewWidth / 16) + 2,
      );
      expect(r.ty1 - r.ty0 + 1).toBeLessThanOrEqual(
        Math.floor(viewHeight / 16) + 2,
      );
    }
  });
});

interface MeadowFile {
  origin: { x: number; y: number };
  legend: Record<string, string>;
  rows: string[];
}

describe("echte Karte (018 AK-1)", () => {
  it("passt zu jeder Weg- und Wasserkachel eine der 13 Formen", () => {
    const url = new URL(
      "../../../../../server/BrowserGame.Server/Features/Game/Map/Maps/meadow.json",
      import.meta.url,
    );
    const file = JSON.parse(readFileSync(url, "utf8")) as MeadowFile;
    const kinds: Record<string, GroundKind> = {
      grass: GroundKind.GRASS,
      path: GroundKind.PATH,
      water: GroundKind.WATER,
      mountain: GroundKind.MOUNTAIN,
    };
    const legend: Record<string, GroundKind> = {};
    for (const [char, name] of Object.entries(file.legend)) {
      const kind = kinds[name];
      if (kind === undefined) throw new Error(`Unbekannte Art ${name}`);
      legend[char] = kind;
    }
    const map = testMap(file.rows, -file.origin.x, -file.origin.y, legend);
    const missing: string[] = [];
    let paths = 0;
    let waters = 0;
    for (let my = 0; my < map.height; my++) {
      for (let mx = 0; mx < map.width; mx++) {
        const tx = mx + map.originTx;
        const ty = my + map.originTy;
        const kind = map.ground[my * map.width + mx];
        if (kind !== GroundKind.PATH && kind !== GroundKind.WATER) continue;
        if (kind === GroundKind.PATH) paths++;
        else waters++;
        if (edgeShape(map, tx, ty) === null)
          missing.push(`${String(tx)},${String(ty)}`);
      }
    }
    expect(paths).toBeGreaterThan(0);
    expect(waters).toBeGreaterThan(0);
    expect(missing).toEqual([]);
  });
});
