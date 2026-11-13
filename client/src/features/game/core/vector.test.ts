import { describe, expect, it } from "vitest";
import { facingOf, lerpAngle } from "@/features/game/core/vector";

const MIN = 0.01;
const deg = (d: number) => (d * Math.PI) / 180;

describe("facingOf (016)", () => {
  it("AK-1: zeigt in die Laufrichtung", () => {
    expect(facingOf({ x: 1, y: 0 }, 1, MIN)).toBe(0);
    expect(facingOf({ x: 0, y: -1 }, 0, MIN)).toBe(-Math.PI / 2);
    expect(facingOf({ x: 0.6, y: 0.8 }, 0, MIN)).toBe(Math.atan2(0.8, 0.6));
  });

  it("AK-2: freier Winkel ohne Raster", () => {
    expect(facingOf({ x: Math.cos(deg(37)), y: Math.sin(deg(37)) }, 0, MIN)).toBeCloseTo(deg(37));
    expect(facingOf({ x: -1, y: 0 }, 0, MIN)).toBe(Math.PI);
  });

  it("AK-3: Stillstand behält die Blickrichtung", () => {
    expect(facingOf({ x: 0, y: 0 }, 1.234, MIN)).toBe(1.234);
  });

  it("AK-3: Stillstand behält sie auch bei Schwellwert 0", () => {
    expect(facingOf({ x: 0, y: 0 }, 1.234, 0)).toBe(1.234);
    expect(facingOf({ x: 0.001, y: 0 }, 1.234, 0)).toBe(0);
  });

  it("AK-5: kürzer als der Schwellwert ändert nichts", () => {
    expect(facingOf({ x: 0.005, y: 0 }, 1.234, MIN)).toBe(1.234);
    expect(facingOf({ x: 0, y: MIN }, 0, MIN)).toBe(Math.PI / 2);
  });
});

describe("lerpAngle (016 AK-7)", () => {
  it("interpoliert zwischen zwei Winkeln", () => {
    expect(lerpAngle(0, Math.PI / 2, 0.5)).toBeCloseTo(Math.PI / 4);
    expect(lerpAngle(0.3, 1.2, 0)).toBe(0.3);
  });

  it("läuft über ±π den kürzeren Bogen", () => {
    expect(lerpAngle(deg(170), deg(-170), 0.5)).toBeCloseTo(Math.PI);
    expect(lerpAngle(deg(170), deg(-170), 0.25)).toBeCloseTo(deg(175));
    expect(lerpAngle(deg(-170), deg(170), 0.5)).toBeCloseTo(-Math.PI);
  });

  it("endet bei t = 1 auf derselben Richtung wie das Ziel", () => {
    for (const [a, b] of [
      [deg(170), deg(-170)],
      [deg(-90), deg(180)],
    ] as const) {
      const r = lerpAngle(a, b, 1);
      expect(Math.cos(r)).toBeCloseTo(Math.cos(b));
      expect(Math.sin(r)).toBeCloseTo(Math.sin(b));
    }
  });

  it("dreht nie mehr als einen halben Kreis", () => {
    expect(lerpAngle(0, (3 * Math.PI) / 2, 1)).toBeCloseTo(-Math.PI / 2);
    expect(lerpAngle(0, 4 * Math.PI + 0.5, 1)).toBeCloseTo(0.5);
  });
});
