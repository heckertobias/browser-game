import { describe, expect, it } from "vitest";
import { group, parseAtlas, type Atlas } from "./atlas";

function valid(): Record<string, unknown> {
  return {
    image: "ground.png",
    entries: {
      grass: { x: 0, y: 0, width: 16, height: 16 },
      "grass.2": { x: 16, y: 0, width: 16, height: 16 },
      "grass.10": { x: 32, y: 0, width: 16, height: 16 },
      "path.n": { x: 0, y: 16, width: 16, height: 16 },
      tree: { x: 16, y: 16, width: 32, height: 48, origin: { x: 16, y: 44 } },
    },
  };
}
const tile = { x: 0, y: 0, width: 16, height: 16 };
const tree = { x: 16, y: 16, width: 32, height: 48, origin: { x: 16, y: 44 } };

function withEntry(name: string, entry: unknown): Record<string, unknown> {
  return { ...valid(), entries: { [name]: entry } };
}

function expectFailure(input: unknown, path: string): void {
  const escaped = path.replace(/[.*+?^${}()|[\]\\/]/g, String.raw`\$&`);
  expect(() => parseAtlas(input)).toThrow(new RegExp(`^${escaped}:`));
}

const atlasOf = (names: string[]): Atlas =>
  parseAtlas({ image: "a.png", entries: Object.fromEntries(names.map((n) => [n, tile])) });

describe("parseAtlas (018)", () => {
  it("AK-1: nimmt einen Atlas an und füllt origin mit 0, 0", () => {
    const atlas = parseAtlas(valid());
    expect(atlas.image).toBe("ground.png");
    expect(atlas.entries).toBeInstanceOf(Map);
    expect([...atlas.entries.keys()]).toEqual(["grass", "grass.2", "grass.10", "path.n", "tree"]);
    expect(atlas.entries.get("grass")).toEqual({ ...tile, origin: { x: 0, y: 0 } });
    expect(atlas.entries.get("path.n")).toEqual({ x: 0, y: 16, width: 16, height: 16, origin: { x: 0, y: 0 } });
    expect(atlas.entries.get("tree")).toEqual(tree);
  });

  it("AK-1: nimmt Grenzwerte an", () => {
    expect(() => parseAtlas(withEntry("a", { x: 0, y: 0, width: 1, height: 1 }))).not.toThrow();
    expect(() =>
      parseAtlas(withEntry("a", { x: 0, y: 0, width: 32, height: 48, origin: { x: 31, y: 47 } })),
    ).not.toThrow();
  });

  it("ignoriert unbekannte Felder", () => {
    expect(parseAtlas({ ...valid(), comment: "x" })).toEqual(parseAtlas(valid()));
    expect(parseAtlas(withEntry("a", { ...tile, note: "x" }))).toEqual(parseAtlas(withEntry("a", tile)));
  });

  it.each([[null], [[]], ["atlas"], [42]])("AK-1: meldet %j statt eines Objekts bei atlas", (value) => {
    expectFailure(value, "atlas");
  });

  it.each([[undefined], [""], [3]])("AK-1: meldet image = %j", (image) => {
    expectFailure({ ...valid(), image }, "image");
  });

  it.each([[undefined], [[]], ["grass"], [{}]])("AK-1: meldet entries = %j", (entries) => {
    expectFailure({ ...valid(), entries }, "entries");
  });

  it.each([[16], [[]], [null]])("AK-1: meldet einen Eintrag %j, der kein Objekt ist", (value) => {
    expectFailure(withEntry("grass", value), "entries.grass");
  });

  it.each([[undefined], [-1], [1.5], ["0"]])("AK-1: meldet x = %j", (x) => {
    expectFailure(withEntry("grass", { ...tile, x }), "entries.grass.x");
  });

  it("AK-1: meldet y = -1", () => {
    expectFailure(withEntry("grass", { ...tile, y: -1 }), "entries.grass.y");
  });

  it.each([[undefined], [0], [-16], [1.5]])("AK-1: meldet width = %j", (width) => {
    expectFailure(withEntry("grass", { ...tile, width }), "entries.grass.width");
  });

  it("AK-1: meldet height = 0", () => {
    expectFailure(withEntry("grass", { ...tile, height: 0 }), "entries.grass.height");
  });

  it.each([[[16, 44]], [16], [null]])("AK-1: meldet origin = %j", (origin) => {
    expectFailure(withEntry("tree", { ...tree, origin }), "entries.tree.origin");
  });

  it.each([[-1], [32], [1.5]])("AK-1: meldet origin.x = %j", (x) => {
    expectFailure(withEntry("tree", { ...tree, origin: { x, y: 44 } }), "entries.tree.origin.x");
  });

  it.each([[-1], [48]])("AK-1: meldet origin.y = %j", (y) => {
    expectFailure(withEntry("tree", { ...tree, origin: { x: 16, y } }), "entries.tree.origin.y");
  });

  it("nennt den Namen mit Punkten in der Fundstelle", () => {
    expectFailure(withEntry("grass.2", { ...tile, width: 0 }), "entries.grass.2.width");
  });
});

describe("group (018)", () => {
  it("AK-1: liefert die Einträge name.<…> sortiert nach Codeeinheiten", () => {
    expect(group(parseAtlas(valid()), "grass")).toEqual(["grass.10", "grass.2"]);
  });

  it("AK-1: hängt nicht von der Reihenfolge in der Datei ab", () => {
    const input = valid();
    const reversed = {
      ...input,
      entries: Object.fromEntries(Object.entries(input["entries"] as Record<string, unknown>).reverse()),
    };
    expect(group(parseAtlas(reversed), "grass")).toEqual(["grass.10", "grass.2"]);
  });

  it("zählt nur Namen mit Punkt nach dem Gruppennamen", () => {
    expect(group(atlasOf(["grass", "grassy", "grassland.1", "grass.a"]), "grass")).toEqual(["grass.a"]);
  });

  it("liefert für eine unbekannte Gruppe eine leere Liste", () => {
    expect(group(parseAtlas(valid()), "water")).toEqual([]);
    expect(group(parseAtlas(valid()), "path")).toEqual(["path.n"]);
  });
});
