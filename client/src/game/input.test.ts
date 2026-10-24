import { describe, expect, it } from "vitest";
import { activeActions, combinedDirection, DEFAULT_BINDINGS, keyboardDirection, pointerDirection } from "./input";
import { clampDirection } from "./vector";

const keys = (...codes: string[]) => activeActions(new Set(codes), DEFAULT_BINDINGS);

describe("Tastatur (AK-2)", () => {
  it.each([
    ["KeyW", 0, -1],
    ["KeyS", 0, 1],
    ["KeyA", -1, 0],
    ["KeyD", 1, 0],
  ])("%s bewegt in Richtung (%i, %i)", (code, x, y) => {
    expect(keyboardDirection(keys(code))).toEqual({ x, y });
  });

  it("diagonal ist genauso schnell wie geradeaus", () => {
    const dir = keyboardDirection(keys("KeyW", "KeyD"));
    expect(Math.hypot(dir.x, dir.y)).toBeCloseTo(1);
    expect(dir.x).toBeCloseTo(Math.SQRT1_2);
    expect(dir.y).toBeCloseTo(-Math.SQRT1_2);
  });

  it("gegensätzliche Tasten heben sich auf", () => {
    expect(keyboardDirection(keys("KeyA", "KeyD"))).toEqual({ x: 0, y: 0 });
  });

  it("ignoriert nicht belegte Tasten", () => {
    expect(keys("KeyQ", "ArrowUp").size).toBe(0);
  });

  it("nutzt die übergebene Belegung (Grundlage für 002)", () => {
    const custom = { ...DEFAULT_BINDINGS, moveUp: ["ArrowUp"] };
    expect(activeActions(new Set(["ArrowUp"]), custom)).toEqual(new Set(["moveUp"]));
  });
});

describe("Halten-und-Folgen (AK-2b)", () => {
  it("läuft mit voller Geschwindigkeit Richtung Zeiger", () => {
    const dir = pointerDirection({ x: 30, y: 40 }, { x: 0, y: 0 });
    expect(dir.x).toBeCloseTo(0.6);
    expect(dir.y).toBeCloseTo(0.8);
  });

  it("bleibt in der Totzone stehen", () => {
    expect(pointerDirection({ x: 2, y: 2 }, { x: 0, y: 0 })).toEqual({ x: 0, y: 0 });
  });

  it("ohne gedrückten Zeiger steht die Figur", () => {
    expect(combinedDirection(new Set(), null, { x: 0, y: 0 })).toEqual({ x: 0, y: 0 });
  });

  it("Tastatur hat Vorrang vor dem Zeiger", () => {
    expect(combinedDirection(keys("KeyA"), { x: 100, y: 0 }, { x: 0, y: 0 })).toEqual({ x: -1, y: 0 });
  });
});

describe("clampDirection", () => {
  it("kürzt auf Länge 1 und behandelt ungültige Werte wie der Server", () => {
    expect(clampDirection(10, 0)).toEqual({ x: 1, y: 0 });
    expect(clampDirection(0.5, 0)).toEqual({ x: 0.5, y: 0 });
    expect(clampDirection(Number.NaN, 1)).toEqual({ x: 0, y: 0 });
  });
});
