import { create } from "@bufbuild/protobuf";
import { describe, expect, it } from "vitest";
import { AreaMapSchema, GroundKind } from "@/generated/browsergame/v1/game_pb";
import { areaMapFromProto, kindAt, type AreaMap } from "./areaMap";
import { testMap } from "./testMap";

const VALID = {
  tileSize: 16,
  width: 2,
  height: 1,
  originTx: -1,
  originTy: 0,
  ground: new Uint8Array([1, 2]),
};

describe("areaMapFromProto (018 AK-3)", () => {
  it("liefert null ohne Karte", () => {
    expect(areaMapFromProto(undefined)).toBeNull();
  });

  it("übernimmt eine gültige Karte ohne Kopie von ground", () => {
    const map = areaMapFromProto(create(AreaMapSchema, VALID));
    expect(map).toEqual(VALID);
  });

  it("behält dieselbe Uint8Array-Instanz", () => {
    const message = create(AreaMapSchema, VALID);
    expect(areaMapFromProto(message)?.ground).toBe(message.ground);
  });

  it.each([[new Uint8Array([1])], [new Uint8Array([1, 1, 1])]])(
    "liefert null bei falscher Länge von ground",
    (ground) => {
      expect(
        areaMapFromProto(create(AreaMapSchema, { ...VALID, ground })),
      ).toBeNull();
    },
  );

  it.each([
    { width: 0, ground: new Uint8Array() },
    { height: 0 },
    { tileSize: 0 },
  ])("liefert null bei Maß 0 (%j)", (override) => {
    const base =
      override.height === 0 ? { ...VALID, ground: new Uint8Array() } : VALID;
    expect(
      areaMapFromProto(create(AreaMapSchema, { ...base, ...override })),
    ).toBeNull();
  });
});

describe("kindAt (018 AK-5)", () => {
  const m = testMap(["=.~", "^.="], -1, -1);

  it("liest innerhalb der Karte", () => {
    expect(kindAt(m, -1, -1)).toBe(GroundKind.PATH);
    expect(kindAt(m, 0, -1)).toBe(GroundKind.GRASS);
    expect(kindAt(m, 1, -1)).toBe(GroundKind.WATER);
    expect(kindAt(m, -1, 0)).toBe(GroundKind.MOUNTAIN);
    expect(kindAt(m, 1, 0)).toBe(GroundKind.PATH);
  });

  it("setzt jede Seite mit der äußersten Kachel fort", () => {
    expect(kindAt(m, -10, -1)).toBe(GroundKind.PATH);
    expect(kindAt(m, -10, 0)).toBe(GroundKind.MOUNTAIN);
    expect(kindAt(m, 50, -1)).toBe(GroundKind.WATER);
    expect(kindAt(m, 50, 0)).toBe(GroundKind.PATH);
    expect(kindAt(m, 0, -50)).toBe(GroundKind.GRASS);
    expect(kindAt(m, 1, -50)).toBe(GroundKind.WATER);
    expect(kindAt(m, -1, 50)).toBe(GroundKind.MOUNTAIN);
    expect(kindAt(m, 0, 50)).toBe(GroundKind.GRASS);
  });

  it("setzt jede Ecke fort", () => {
    expect(kindAt(m, -100, -100)).toBe(GroundKind.PATH);
    expect(kindAt(m, 100, -100)).toBe(GroundKind.WATER);
    expect(kindAt(m, -100, 100)).toBe(GroundKind.MOUNTAIN);
    expect(kindAt(m, 100, 100)).toBe(GroundKind.PATH);
  });

  it("verträgt sehr große Koordinaten", () => {
    expect(kindAt(testMap(["^"]), -(2 ** 31), 2 ** 31 - 1)).toBe(
      GroundKind.MOUNTAIN,
    );
  });

  it("behandelt ein unbekanntes Byte als unbestimmt", () => {
    const map: AreaMap = {
      tileSize: 16,
      width: 1,
      height: 1,
      originTx: 0,
      originTy: 0,
      ground: new Uint8Array([9]),
    };
    expect(kindAt(map, 0, 0)).toBe(GroundKind.UNSPECIFIED);
  });
});
