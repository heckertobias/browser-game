import { describe, expect, it } from "vitest";
import { spriteDirection } from "./direction";

const deg = (d: number) => (d * Math.PI) / 180;

describe("spriteDirection (017)", () => {
  it("AK-4: wählt für die Hauptrichtungen die passende Richtung", () => {
    expect(spriteDirection(0)).toBe("right");
    expect(spriteDirection(deg(90))).toBe("down");
    expect(spriteDirection(deg(180))).toBe("left");
    expect(spriteDirection(deg(-90))).toBe("up");
  });

  it.each([
    [30, "right"],
    [60, "down"],
    [170, "left"],
    [-100, "up"],
    [44, "right"],
    [46, "down"],
    [134, "down"],
    [136, "left"],
    [-46, "up"],
  ] as const)("AK-4: wählt für %f Grad die nächstliegende Richtung", (angle, expected) => {
    expect(spriteDirection(deg(angle))).toBe(expected);
  });

  it("AK-4: kommt mit Winkeln über eine Umdrehung hinaus zurecht", () => {
    expect(spriteDirection(deg(450))).toBe("down");
    expect(spriteDirection(deg(-180))).toBe("left");
  });

  it.each([
    [45, "right"],
    [-45, "right"],
    [135, "left"],
    [-135, "left"],
  ] as const)("AK-5: zeigt bei genau schräg (%f Grad) die Seite", (angle, expected) => {
    expect(spriteDirection(deg(angle))).toBe(expected);
  });

  it.each([
    [45, "right"],
    [-45, "right"],
    [135, "left"],
    [-135, "left"],
  ] as const)("AK-5: zeigt bei genau schräg als float32 (%f Grad) die Seite", (angle, expected) => {
    expect(spriteDirection(Math.fround(deg(angle)))).toBe(expected);
  });

  it("AK-5: die Toleranz für genau schräg ist eng", () => {
    expect(spriteDirection(deg(44.99))).toBe("right");
    expect(spriteDirection(deg(45.01))).toBe("down");
    expect(spriteDirection(deg(134.99))).toBe("down");
    expect(spriteDirection(deg(135.01))).toBe("left");
  });

  it("AK-6: die Standard-Blickrichtung zeigt nach unten", () => {
    expect(spriteDirection(Math.fround(Math.PI / 2))).toBe("down");
  });

  it("wählt die Hauptrichtungen auch als float32", () => {
    expect(spriteDirection(Math.fround(Math.PI))).toBe("left");
    expect(spriteDirection(Math.fround(-Math.PI / 2))).toBe("up");
  });
});
