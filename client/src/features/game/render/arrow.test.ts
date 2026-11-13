import { describe, expect, it } from "vitest";
import { ARROW_GAP, ARROW_LENGTH, ARROW_WIDTH, PLAYER_SIZE } from "@/features/game/core/constants";
import type { Vec } from "@/features/game/core/vector";
import { arrowPoints } from "./render";

const BASE = PLAYER_SIZE / 2 + ARROW_GAP;
const REACH = BASE + ARROW_LENGTH;
const HALF = ARROW_WIDTH / 2;

const deg = (d: number) => (d * Math.PI) / 180;

function expectPoints(actual: readonly Vec[], expected: readonly Vec[]): void {
  expect(actual).toHaveLength(expected.length);
  expected.forEach((point, i) => {
    expect(actual[i]?.x).toBeCloseTo(point.x, 9);
    expect(actual[i]?.y).toBeCloseTo(point.y, 9);
  });
}

describe("arrowPoints (016)", () => {
  it("AK-6: zeigt bei Blickrichtung 0 nach rechts", () => {
    expectPoints(arrowPoints(100, 50, 0), [
      { x: 100 + REACH, y: 50 },
      { x: 100 + BASE, y: 50 + HALF },
      { x: 100 + BASE, y: 50 - HALF },
    ]);
  });

  it("AK-6: zeigt bei π/2 nach unten", () => {
    expectPoints(arrowPoints(100, 50, Math.PI / 2), [
      { x: 100, y: 50 + REACH },
      { x: 100 - HALF, y: 50 + BASE },
      { x: 100 + HALF, y: 50 + BASE },
    ]);
  });

  it.each([0.001, deg(37), 1.234, -2.5, Math.PI])("AK-7: Spitze liegt stufenlos in Blickrichtung (%f)", (f) => {
    const [tip, a, b] = arrowPoints(10, -20, f);
    expect((tip.x - 10) / REACH).toBeCloseTo(Math.cos(f), 9);
    expect((tip.y + 20) / REACH).toBeCloseTo(Math.sin(f), 9);
    expect((a.x + b.x) / 2).toBeCloseTo(10 + BASE * Math.cos(f), 9);
    expect((a.y + b.y) / 2).toBeCloseTo(-20 + BASE * Math.sin(f), 9);
    expect(Math.hypot(a.x - b.x, a.y - b.y)).toBeCloseTo(ARROW_WIDTH, 9);
    expect(Math.hypot(tip.x - a.x, tip.y - a.y)).toBeCloseTo(Math.hypot(tip.x - b.x, tip.y - b.y), 9);
  });

  it("AK-7: schon eine kleine Drehung bewegt die Spitze (kein Raster)", () => {
    const turned = arrowPoints(0, 0, 0.01)[0].y;
    expect(turned).toBeCloseTo(REACH * Math.sin(0.01), 9);
    expect(turned).toBeGreaterThan(0);
    expect(arrowPoints(0, 0, 0)[0].y).toBeCloseTo(0, 9);
  });

  it("AK-6: alle Ecken liegen außerhalb der Figur", () => {
    for (let i = 0; i < 720; i++) {
      for (const p of arrowPoints(0, 0, deg(i / 2))) {
        expect(Math.max(Math.abs(p.x), Math.abs(p.y))).toBeGreaterThan(PLAYER_SIZE / 2);
      }
    }
  });

  it("eine volle Umdrehung ergibt denselben Pfeil", () => {
    expectPoints(arrowPoints(5, 5, 1 + 2 * Math.PI), arrowPoints(5, 5, 1));
  });
});
