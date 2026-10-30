import { describe, expect, it } from "vitest";
import { activeActions, combinedDirection, DEFAULT_BINDINGS, keyboardDirection, POINTER_RELEASE_EVENTS, PointerHold, pointerDirection } from "./input";
import { clampDirection } from "../engine/vector";

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

describe("PointerHold (007 AK-7)", () => {
  const p = (pointerId: number, x = 0, y = 0, isPrimary = true, button = 0) => ({ pointerId, clientX: x, clientY: y, isPrimary, button });

  it("primärer Zeiger mit Haupttaste startet das Halten", () => {
    const hold = new PointerHold();
    expect(hold.down(p(1, 10, 20))).toBe(true);
    expect(hold.position()).toEqual({ x: 10, y: 20 });
  });

  it("Bewegung desselben Zeigers wird verfolgt", () => {
    const hold = new PointerHold();
    hold.down(p(1, 10, 20));
    hold.move(p(1, 30, 40));
    expect(hold.position()).toEqual({ x: 30, y: 40 });
  });

  it("Bewegung ohne Halten bewirkt nichts", () => {
    const hold = new PointerHold();
    hold.move(p(1, 30, 40));
    expect(hold.position()).toBeNull();
  });

  it("nicht primärer Zeiger startet nicht", () => {
    const hold = new PointerHold();
    expect(hold.down(p(1, 0, 0, false))).toBe(false);
    expect(hold.position()).toBeNull();
  });

  it.each([1, 2])("Taste %i startet nicht", (button) => {
    const hold = new PointerHold();
    expect(hold.down(p(1, 0, 0, true, button))).toBe(false);
    expect(hold.position()).toBeNull();
  });

  it("zweiter Finger wird ignoriert", () => {
    const hold = new PointerHold();
    hold.down(p(1, 10, 20));
    expect(hold.down(p(2, 50, 50, false))).toBe(false);
    hold.move(p(2, 60, 60));
    expect(hold.position()).toEqual({ x: 10, y: 20 });
  });

  it("zweites primäres down mit anderer ID während des Haltens startet nicht", () => {
    const hold = new PointerHold();
    hold.down(p(1, 10, 20));
    expect(hold.down(p(2, 50, 50))).toBe(false);
    expect(hold.position()).toEqual({ x: 10, y: 20 });
  });

  it("Release mit fremder ID bewirkt nichts", () => {
    const hold = new PointerHold();
    hold.down(p(1, 10, 20));
    hold.release(2);
    expect(hold.position()).toEqual({ x: 10, y: 20 });
  });

  it("Loslassen des ersten Fingers beendet das Halten, auch wenn der zweite noch liegt", () => {
    const hold = new PointerHold();
    hold.down(p(1, 10, 20));
    hold.down(p(2, 50, 50, false));
    hold.release(1);
    hold.move(p(2, 60, 60, false));
    expect(hold.position()).toBeNull();
  });

  it("nach Release kann neu gestartet werden", () => {
    const hold = new PointerHold();
    hold.down(p(1, 10, 20));
    hold.release(1);
    expect(hold.down(p(3, 5, 6))).toBe(true);
    expect(hold.position()).toEqual({ x: 5, y: 6 });
  });

  it("reset beendet das Halten", () => {
    const hold = new PointerHold();
    hold.down(p(1, 10, 20));
    hold.reset();
    hold.move(p(1, 30, 40));
    expect(hold.position()).toBeNull();
  });

  it("verlorenes Capture beendet das Halten wie pointerup", () => {
    expect(POINTER_RELEASE_EVENTS).toEqual(expect.arrayContaining(["pointerup", "pointercancel", "lostpointercapture"]));
  });
});
