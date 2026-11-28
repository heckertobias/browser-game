import { describe, expect, it } from "vitest";
import { frameRect, parseRig, sheetSize, type Rig } from "./rig";

// Wie assets/sprites/humanoid/rig.json (017 plan.md), als frisches Objekt je Aufruf.
function valid(): Record<string, unknown> {
  return {
    cell: { width: 48, height: 48 },
    origin: { x: 24, y: 24 },
    directions: ["down", "up", "left", "right"],
    animations: {
      idle: { firstRow: 0, frames: 1 },
      walk: { firstRow: 0, frames: 4, frameMs: 100 },
    },
    slots: ["body", "clothing"],
    parts: {
      "body/default": { slot: "body", image: "body-default.png" },
      "clothing/ninja": { slot: "clothing", image: "clothing-ninja.png", tint: true },
    },
  };
}
const rig = (changes: Record<string, unknown> = {}): Rig => parseRig({ ...valid(), ...changes });

const walk = { firstRow: 0, frames: 4, frameMs: 100 };
const idle = { firstRow: 0, frames: 1 };
const body = { slot: "body", image: "body-default.png" };

function expectFailure(input: unknown, path: string): void {
  const escaped = path.replace(/[.*+?^${}()|[\]\\/]/g, String.raw`\$&`);
  expect(() => parseRig(input)).toThrow(new RegExp(`^${escaped}:`));
}

describe("parseRig (017)", () => {
  it("AK-19: nimmt die Beschreibung aus rig.json an", () => {
    expect(parseRig(valid())).toEqual(valid());
  });

  it("AK-19: nimmt ein weiteres Teil und einen weiteren Slot an", () => {
    const slots = ["body", "clothing", "hat"];
    const parts = {
      ...(valid()["parts"] as Record<string, unknown>),
      "clothing/robe": { slot: "clothing", image: "clothing-robe.png", tint: true },
      "hat/straw": { slot: "hat", image: "hat-straw.png" },
    };
    const parsed = rig({ slots, parts });
    expect(parsed.slots).toEqual(slots);
    expect(parsed.parts).toEqual(parts);
  });

  it("nimmt Animationen ohne frameMs bei einem Bild und überlappende Bereiche an", () => {
    expect(() => rig({ animations: { idle, walk, run: { firstRow: 2, frames: 2, frameMs: 80 } } })).not.toThrow();
  });

  it("ignoriert unbekannte Felder", () => {
    expect(parseRig({ ...valid(), comment: "x" })).toEqual(valid());
  });

  it.each([[null], [[]], ["rig"], [42]])("AK-19: meldet %j statt eines Objekts bei rig", (input) => {
    expectFailure(input, "rig");
  });

  it.each([[undefined], [48]])("AK-19: meldet cell = %j", (cell) => {
    expectFailure({ ...valid(), cell }, "cell");
  });

  it.each([[0], [-48], [1.5], ["48"]])("AK-19: meldet cell.width = %j", (width) => {
    expectFailure({ ...valid(), cell: { width, height: 48 } }, "cell.width");
  });

  it("AK-19: meldet cell.height = 0", () => {
    expectFailure({ ...valid(), cell: { width: 48, height: 0 } }, "cell.height");
  });

  it("AK-19: meldet origin als Liste", () => {
    expectFailure({ ...valid(), origin: [24, 24] }, "origin");
  });

  it.each([[-1], [48], [1.5]])("AK-19: meldet origin.x = %j", (x) => {
    expectFailure({ ...valid(), origin: { x, y: 24 } }, "origin.x");
  });

  it("AK-19: meldet origin.y = 48", () => {
    expectFailure({ ...valid(), origin: { x: 24, y: 48 } }, "origin.y");
  });

  it.each([["down"], [[]]])("AK-19: meldet directions = %j", (directions) => {
    expectFailure({ ...valid(), directions }, "directions");
  });

  it.each([[["down", "north"]], [["down", "down"]]])("AK-19: meldet directions = %j", (directions) => {
    expectFailure({ ...valid(), directions }, "directions.1");
  });

  it.each([[[]], [{}]])("AK-19: meldet animations = %j", (animations) => {
    expectFailure({ ...valid(), animations }, "animations");
  });

  it("AK-19: meldet eine Animation, die kein Objekt ist", () => {
    expectFailure({ ...valid(), animations: { walk: 4 } }, "animations.walk");
  });

  it.each([[-1], [0.5]])("AK-19: meldet firstRow = %j", (firstRow) => {
    expectFailure({ ...valid(), animations: { walk: { ...walk, firstRow } } }, "animations.walk.firstRow");
  });

  it.each([[0], [1.5]])("AK-19: meldet frames = %j", (frames) => {
    expectFailure({ ...valid(), animations: { walk: { ...walk, frames } } }, "animations.walk.frames");
  });

  it.each([[undefined], [0], [-100], ["100"]])("AK-19: meldet walk.frameMs = %j", (frameMs) => {
    expectFailure({ ...valid(), animations: { walk: { ...walk, frameMs } } }, "animations.walk.frameMs");
  });

  it("AK-19: meldet frameMs = 0 auch bei einem Bild", () => {
    expectFailure({ ...valid(), animations: { idle: { ...idle, frameMs: 0 } } }, "animations.idle.frameMs");
  });

  it("AK-19: meldet slots als Text", () => {
    expectFailure({ ...valid(), slots: "body" }, "slots");
  });

  it.each([[["body", "body"]], [["body", ""]], [["body", 3]]])("AK-19: meldet slots = %j", (slots) => {
    expectFailure({ ...valid(), slots }, "slots.1");
  });

  it("AK-19: meldet parts als Liste", () => {
    expectFailure({ ...valid(), parts: [] }, "parts");
  });

  it("AK-19: meldet ein Teil, das kein Objekt ist", () => {
    expectFailure({ ...valid(), parts: { "body/default": "body-default.png" } }, "parts.body/default");
  });

  it.each([[{ ...body, slot: "hat" }], [{ image: "body-default.png" }]])("AK-19: meldet Teil %j wegen slot", (part) => {
    expectFailure({ ...valid(), parts: { "body/default": part } }, "parts.body/default.slot");
  });

  it.each([[{ slot: "body" }], [{ ...body, image: "" }]])("AK-19: meldet Teil %j wegen image", (part) => {
    expectFailure({ ...valid(), parts: { "body/default": part } }, "parts.body/default.image");
  });

  it("AK-19: meldet tint = yes", () => {
    expectFailure({ ...valid(), parts: { "body/default": { ...body, tint: "yes" } } }, "parts.body/default.tint");
  });
});

describe("sheetSize (017)", () => {
  it("ergibt das Blatt aus Richtungen und Zeilen (Format-Regel 1)", () => {
    expect(sheetSize(rig())).toEqual({ width: 192, height: 192 });
  });

  it("bestimmt die Höhe über das größte firstRow + frames", () => {
    const r = rig({ directions: ["left", "right"], animations: { idle, walk: { firstRow: 1, frames: 4, frameMs: 100 } } });
    expect(sheetSize(r)).toEqual({ width: 96, height: 240 });
  });
});

describe("frameRect (017)", () => {
  const columns = { down: 0, up: 48, left: 96, right: 144 } as const;

  it.each([0, 99, 100, 12_345])("AK-8: idle ist bei t=%d für jede Richtung dasselbe Bild", (t) => {
    const r = rig();
    for (const [dir, sx] of Object.entries(columns)) {
      expect(frameRect(r, "idle", dir as keyof typeof columns, t)).toEqual({ sx, sy: 0 });
    }
  });

  it.each([
    [0, 0],
    [99, 0],
    [100, 48],
    [250, 96],
    [399, 144],
    [400, 0],
    [1_000_050, 0],
  ])("AK-7: walk bei t=%d liegt in Zeile sy=%d", (t, sy) => {
    expect(frameRect(rig(), "walk", "down", t)).toEqual({ sx: 0, sy });
  });

  it("AK-7: walk bleibt in der Spalte der Richtung", () => {
    expect(frameRect(rig(), "walk", "left", 250)).toEqual({ sx: 96, sy: 96 });
  });

  it("verschiebt die Zeilen um firstRow", () => {
    const r = rig({ animations: { idle, walk: { firstRow: 2, frames: 4, frameMs: 100 } } });
    expect(frameRect(r, "walk", "down", 100).sy).toBe(144);
  });

  it("folgt mit den Spalten der Reihenfolge im Rig", () => {
    expect(frameRect(rig({ directions: ["left", "right"] }), "idle", "right", 0).sx).toBe(48);
  });

  it.each([0, 99, 100, 250, 399, 400, 1_000_050])("liefert bei t=%d ganzzahlige Werte", (t) => {
    const { sx, sy } = frameRect(rig(), "walk", "down", t);
    expect(Number.isInteger(sx)).toBe(true);
    expect(Number.isInteger(sy)).toBe(true);
  });

  it.each(["run", "toString"])("wirft bei unbekannter Animation %s", (name) => {
    expect(() => frameRect(rig(), name, "down", 0)).toThrow(new RegExp(`^animations\\.${name}:`));
  });

  it("wirft bei einer Richtung, die das Rig nicht hat", () => {
    expect(() => frameRect(rig({ directions: ["down", "up"] }), "idle", "left", 0)).toThrow(/^directions:/);
  });
});
